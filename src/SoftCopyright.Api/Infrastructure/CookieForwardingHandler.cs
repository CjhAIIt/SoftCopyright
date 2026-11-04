using Microsoft.AspNetCore.Http;

namespace SoftCopyright.Api.Infrastructure;

/// <summary>
/// 认证 Cookie 转发处理器。
///
/// Blazor 界面在服务端渲染时会回调自己的 API；此时必须把浏览器的登录 Cookie
/// 一起带过去，否则自调用会被当成未登录请求。
/// </summary>
public sealed class CookieForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var context = httpContextAccessor.HttpContext;
        if (context is not null && context.Request.Headers.TryGetValue("Cookie", out var cookies))
        {
            request.Headers.TryAddWithoutValidation("Cookie", (string?)cookies.ToString());
        }

        return base.SendAsync(request, cancellationToken);
    }
}
