using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Application.Services;

/// <summary>
/// 当前账号上下文（环境作用域）。
///
/// Web 请求由 API 层的中间件在认证后写入；后台任务没有请求上下文，
/// 由工作流按案例归属账号显式指定（<see cref="Use"/>）。
/// </summary>
public sealed class CurrentAccountContext : ICurrentAccountContext
{
    private Guid? _userId;

    public Guid? UserId => _userId;

    public void Use(Guid? userId) => _userId = userId;
}
