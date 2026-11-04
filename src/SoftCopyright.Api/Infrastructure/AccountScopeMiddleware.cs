using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Api.Infrastructure;

/// <summary>
/// 账号作用域中间件：认证通过后把当前账号写入环境作用域。
///
/// Web 请求由这里注入账号；后台任务没有请求上下文，
/// 由工作流按案例归属账号显式指定。
/// </summary>
public sealed class AccountScopeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICurrentAccountContext accountScope)
    {
        var principal = context.User;
        if (principal?.Identity?.IsAuthenticated == true)
        {
            var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(value, out var userId))
            {
                accountScope.Use(userId);
            }
        }

        await next(context);
    }
}
