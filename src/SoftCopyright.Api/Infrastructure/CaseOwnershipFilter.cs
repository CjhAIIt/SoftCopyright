using Microsoft.AspNetCore.Mvc;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Api.Infrastructure;

/// <summary>
/// 案例归属守卫。
///
/// 案例按账号隔离，因此凡是带案例 ID 的端点都必须先确认"该案例属于当前账号"。
/// 各端点若各自判断很容易漏掉，这里统一在过滤器里拦截：
/// 案例不可见时返回 404（不泄露"存在但无权访问"）。
/// </summary>
public sealed class CaseOwnershipFilter(ICaseService cases) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var routeValues = context.HttpContext.Request.RouteValues;

        // 只处理以 {id:guid} 表示案例 ID 的端点；其他路由（如 artifactId）另行处理。
        if (!routeValues.TryGetValue("id", out var raw) || !TryParseGuid(raw, out var caseId))
        {
            return await next(context);
        }

        var entity = await cases.GetAsync(caseId, context.HttpContext.RequestAborted);
        if (entity is null)
        {
            return Results.NotFound(new { error = new { code = "not_found", message = "申请案例不存在。" } });
        }

        return await next(context);
    }

    /// <summary>
    /// 解析路由值里的案例 ID。
    /// 路由值在 RouteData 中是**原始字符串**（强类型转换发生在模型绑定阶段），
    /// 因此这里必须同时接受 Guid 与 string，否则过滤器会静默失效。
    /// </summary>
    private static bool TryParseGuid(object? value, out Guid id)
    {
        switch (value)
        {
            case Guid guid:
                id = guid;
                return true;
            case string text when Guid.TryParse(text, out var parsed):
                id = parsed;
                return true;
            default:
                id = Guid.Empty;
                return false;
        }
    }
}
