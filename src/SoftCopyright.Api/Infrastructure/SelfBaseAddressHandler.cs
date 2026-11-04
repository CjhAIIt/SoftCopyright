using Microsoft.AspNetCore.Http;

namespace SoftCopyright.Api.Infrastructure;

/// <summary>
/// 自调用地址处理器：Blazor 界面在服务端渲染时需要回调自己的 API。
/// 优先使用当前请求的来源（协议 + 主机 + 路径基），避免把地址写死在配置里；
/// 交互式事件阶段没有 HttpContext，回退到配置的 App:ApiBaseAddress。
/// </summary>
public sealed class SelfBaseAddressHandler(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // HttpClient 在进入处理器链之前就已经把相对地址与 BaseAddress 合成，
        // 因此这里不能只判断 IsAbsoluteUri —— 必须按"当前请求来源"统一重写。
        var origin = ResolveOrigin();
        if (!string.IsNullOrWhiteSpace(origin))
        {
            var uri = request.RequestUri!;
            request.RequestUri = new UriBuilder(origin)
            {
                Path = uri.AbsolutePath,
                Query = uri.Query
            }.Uri;
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private string? ResolveOrigin()
    {
        var context = httpContextAccessor.HttpContext;
        if (context is not null)
        {
            return $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}";
        }

        return configuration["App:ApiBaseAddress"] ?? "http://localhost:5080";
    }
}
