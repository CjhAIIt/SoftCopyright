using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using SoftCopyright.Api.Endpoints;
using SoftCopyright.Api.Infrastructure;
using SoftCopyright.Application;
using SoftCopyright.Application.Api;
using SoftCopyright.Application.Services;
using SoftCopyright.AI;
using SoftCopyright.Documents;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Domain.Options;
using SoftCopyright.Infrastructure;
using SoftCopyright.ProjectScan;
using SoftCopyright.Web;
using SoftCopyright.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSoftCopyrightApplication();
builder.Services.AddSoftCopyrightProjectScan();
builder.Services.AddSoftCopyrightAi();
builder.Services.AddSoftCopyrightDocuments();
builder.Services.AddSoftCopyrightInfrastructure(builder.Configuration);

builder.Services.AddHealthChecks();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// 枚举以字符串输出，便于 API 自描述与前端解析。
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<SoftCopyright.Api.Infrastructure.SelfBaseAddressHandler>();
builder.Services.AddTransient<SoftCopyright.Api.Infrastructure.CookieForwardingHandler>();
builder.Services.AddHttpClient<ICaseApiClient, CaseApiClient>(client =>
{
    // 使用相对地址，由 SelfBaseAddressHandler 按当前请求来源补全。
    client.BaseAddress = new Uri("http://localhost");
    client.Timeout = TimeSpan.FromMinutes(2);
})
.AddHttpMessageHandler<SoftCopyright.Api.Infrastructure.SelfBaseAddressHandler>()
.AddHttpMessageHandler<SoftCopyright.Api.Infrastructure.CookieForwardingHandler>();

// 本地单用户应用：使用 Cookie 认证，账号只存在本机数据库中。
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = AccountEndpoints.CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";

        // API 未登录时返回 401/403，而不是跳转到登录页（跳转对接口调用没有意义）。
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

// 初始化数据库（MVP 使用 EnsureCreated，避免依赖额外的 dotnet-ef 全局工具）。
using (var scope = app.Services.CreateScope())
{
    var provider = InfrastructureServiceExtensions.ResolveProvider(builder.Configuration);
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    logger.LogInformation("数据库提供程序：{Provider}", provider);

    if (provider.Equals("MySql", StringComparison.OrdinalIgnoreCase))
    {
        var connectionString = InfrastructureServiceExtensions.ResolveMySqlConnectionString(builder.Configuration);
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            await InfrastructureServiceExtensions.EnsureMySqlDatabaseAsync(connectionString);
        }
    }

    var db = scope.ServiceProvider.GetRequiredService<SoftCopyright.Infrastructure.Data.SoftCopyrightDbContext>();
    await SoftCopyright.Infrastructure.Data.DatabaseInitializer.InitializeAsync(db, logger);

    // 幂等创建内置管理员账号（仓库中只存密码哈希，不存明文）。
    var accountService = scope.ServiceProvider.GetRequiredService<SoftCopyright.Domain.Contracts.IAccountService>();
    await accountService.EnsureAdminAccountAsync(CancellationToken.None);
}

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("UnhandledException");

    var (status, code, message) = exception switch
    {
        NotFoundException => (StatusCodes.Status404NotFound, "not_found", exception.Message),
        ConflictException => (StatusCodes.Status409Conflict, "conflict", exception.Message),
        InputValidationException => (StatusCodes.Status400BadRequest, "validation", exception.Message),
        ArchiveRejectedException => (StatusCodes.Status400BadRequest, "validation", exception.Message),
        _ => (StatusCodes.Status500InternalServerError, "internal_error", "服务内部错误，请查看服务端日志。")
    };

    if (status >= 500)
    {
        logger.LogError(exception, "未处理的服务端异常");
    }
    else
    {
        logger.LogWarning("请求被拒绝：{Message}", exception?.Message);
    }

    context.Response.StatusCode = status;
    context.Response.ContentType = "application/json; charset=utf-8";
    await context.Response.WriteAsync(JsonSerializer.Serialize(new
    {
        error = new { code, message }
    }));
}));

app.MapHealthChecks("/health");
app.MapAuthApi();
app.MapAccountApi();
app.MapAdminApi();
app.MapSoftCopyrightApi();
app.MapArtifactEndpoints();

// 反向代理（Nginx / IIS / 网关）后面部署时，需要识别原始协议与客户端地址，
// 否则登录 Cookie 的 Secure 标记与重定向地址会按 http 计算。
// 默认只信任回环网卡上的代理；代理不在本机时用 App:ForwardedHeadersKnownProxies 显式指定。
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
    ForwardLimit = 1
};

var knownProxies = builder.Configuration.GetSection("App:ForwardedHeadersKnownProxies").Get<string[]>();
if (knownProxies is { Length: > 0 })
{
    forwardedOptions.KnownProxies.Clear();
    foreach (var proxy in knownProxies)
    {
        if (System.Net.IPAddress.TryParse(proxy.Trim(), out var address))
        {
            forwardedOptions.KnownProxies.Add(address);
        }
        else
        {
            app.Logger.LogWarning("App:ForwardedHeadersKnownProxies 中的 {Proxy} 不是合法 IP，已忽略", proxy);
        }
    }
}

app.UseForwardedHeaders(forwardedOptions);

// 必须提供静态资源：blazor.web.js 缺失会导致页面只做 SSR 渲染、交互全部失效。
app.MapStaticAssets();

// 认证与账号作用域：必须在端点与静态资源之后、业务处理之前。
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AccountScopeMiddleware>();

// Blazor Web App 的表单与交互需要防伪中间件。
app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();

/// <summary>供集成测试引用的入口标记。</summary>
public partial class Program;
