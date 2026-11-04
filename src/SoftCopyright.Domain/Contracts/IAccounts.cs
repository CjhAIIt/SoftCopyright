using SoftCopyright.Domain;

namespace SoftCopyright.Domain.Contracts;

/// <summary>账号相关的用例契约。</summary>
public interface IAccountService
{
    /// <summary>注册一个新账号。用户名重复时返回 false。</summary>
    Task<(bool Success, string? Error, AccountProfile? Profile)> RegisterAsync(
        string userName,
        string displayName,
        string password,
        CancellationToken ct);

    /// <summary>校验用户名与密码。</summary>
    Task<(bool Success, string? Error, AccountProfile? Profile)> LoginAsync(
        string userName,
        string password,
        CancellationToken ct);

    /// <summary>按 ID 取账号。</summary>
    Task<AccountProfile?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>按用户名取账号。</summary>
    Task<AccountProfile?> GetByUserNameAsync(string userName, CancellationToken ct);

    /// <summary>记录登录时间。</summary>
    Task TouchLoginAsync(Guid id, CancellationToken ct);

    /// <summary>标记引导流程已完成。</summary>
    Task CompleteOnboardingAsync(Guid id, CancellationToken ct);

    /// <summary>保存模型接入设置（密钥加密后存储）。</summary>
    Task SaveAiSettingsAsync(Guid id, string? baseUrl, string? model, string? apiKey, CancellationToken ct);

    /// <summary>改为完全离线模式（不保存任何模型配置）。</summary>
    Task DisableAiAssistantAsync(Guid id, CancellationToken ct);

    /// <summary>取出账号的模型接入设置（明文，仅在内存中使用）。</summary>
    Task<AiAccountSettings?> GetAiSettingsAsync(Guid id, CancellationToken ct);
}

/// <summary>
/// 模型接入设置来源。
///
/// 优先取当前账号在引导流程中配置的个人设置；
/// 没有配置时回退到全局环境变量配置（用于无人值守场景）。
/// </summary>
public interface IAiSettingsProvider
{
    /// <summary>取当前生效的模型接入设置；未启用时返回 null。</summary>
    Task<AiAccountSettings?> GetCurrentAsync(CancellationToken ct);
}

/// <summary>当前请求（或当前后台任务）所属的账号。</summary>
public interface ICurrentAccountContext
{
    /// <summary>当前账号 ID；未登录时为 null。</summary>
    Guid? UserId { get; }

    /// <summary>
    /// 显式指定当前账号。后台任务没有 HttpContext，
    /// 由工作流按案例归属账号设置后再调用模型。
    /// </summary>
    void Use(Guid? userId);
}

/// <summary>密码派生与校验（实现位于基础设施层，调用方只依赖本契约）。</summary>
public interface IPasswordHasher
{
    /// <summary>生成密码派生值。</summary>
    string Hash(string password);

    /// <summary>校验密码是否与派生值匹配。</summary>
    bool Verify(string password, string? storedHash);
}

/// <summary>敏感配置加解密（本地密钥文件 + AES-256-GCM）。</summary>
public interface ISecretProtector
{
    string Protect(string plaintext);

    string? Unprotect(string? ciphertext);
}

/// <summary>账号对外可见的信息（不含密码与密钥）。</summary>
public sealed record AccountProfile(
    Guid Id,
    string UserName,
    string DisplayName,
    bool OnboardingCompleted,
    bool AiAssistantEnabled,
    bool HasAiConfiguration,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

/// <summary>账号的模型接入设置（明文，仅在内存中使用）。</summary>
public sealed record AiAccountSettings(string BaseUrl, string Model, string ApiKey);
