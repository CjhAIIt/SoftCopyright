using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;
using SoftCopyright.Api.Endpoints;
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
builder.Services.AddHttpClient<ICaseApiClient, CaseApiClient>(client =>
{
    // 使用相对地址，由 SelfBaseAddressHandler 按当前请求来源补全。
    client.BaseAddress = new Uri("http://localhost");
    client.Timeout = TimeSpan.FromMinutes(2);
}).AddHttpMessageHandler<SoftCopyright.Api.Infrastructure.SelfBaseAddressHandler>();

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
    await db.Database.EnsureCreatedAsync();
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
app.MapSoftCopyrightApi();
app.MapArtifactEndpoints();

// 必须提供静态资源：blazor.web.js 缺失会导致页面只做 SSR 渲染、交互全部失效。
app.MapStaticAssets();

// Blazor Web App 的表单与交互需要防伪中间件。
app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();

/// <summary>供集成测试引用的入口标记。</summary>
public partial class Program;
