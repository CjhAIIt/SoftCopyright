namespace SoftCopyright.Domain;

/// <summary>
/// 本地账号。
///
/// 账号只存在于本机数据库中：密码只保存 PBKDF2 派生值，
/// 模型 API 密钥等敏感配置以密文保存（密钥文件在数据目录，不在仓库内）。
/// </summary>
public sealed class ApplicationUser
{
    public Guid Id { get; set; }

    /// <summary>登录名，全库唯一（大小写不敏感比较）。</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>显示名称。</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>密码派生值，格式 <c>PBKDF2-SHA256$迭代次数$盐$派生值</c>。</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>是否已走过引导流程。</summary>
    public bool OnboardingCompleted { get; set; }

    /// <summary>是否为管理员。管理员可以查看账号列表、删除账号并查看总体数据。</summary>
    public bool IsAdmin { get; set; }

    /// <summary>是否启用模型辅助。false 表示只用完全离线的免费功能。</summary>
    public bool AiAssistantEnabled { get; set; }

    /// <summary>模型接口地址（密文）。</summary>
    public string? EncryptedAiBaseUrl { get; set; }

    /// <summary>模型名称（密文）。</summary>
    public string? EncryptedAiModel { get; set; }

    /// <summary>模型 API 密钥（密文）。</summary>
    public string? EncryptedAiApiKey { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }
}
