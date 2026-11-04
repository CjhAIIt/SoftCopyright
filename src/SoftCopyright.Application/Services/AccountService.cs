using Microsoft.Extensions.Logging;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Application.Services;

/// <summary>本地账号服务：注册、登录、引导状态与模型接入设置。</summary>
public sealed class AccountService(
    IRepository<ApplicationUser> users,
    IRepository<ApplicationCase> cases,
    IPasswordHasher passwordHasher,
    ISecretProtector protector,
    ILogger<AccountService> logger) : IAccountService
{
    public async Task<(bool Success, string? Error, AccountProfile? Profile)> RegisterAsync(
        string userName,
        string displayName,
        string password,
        CancellationToken ct)
    {
        var normalized = userName.Trim();
        var validationError = ValidateCredentials(normalized, displayName, password);
        if (validationError is not null)
        {
            return (false, validationError, null);
        }

        var existing = await users.ListAsync(u => u.UserName == normalized, ct);
        if (existing.Count > 0)
        {
            return (false, "该登录名已被占用，请换一个。", null);
        }

        var entity = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = normalized,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? normalized : displayName.Trim(),
            PasswordHash = passwordHasher.Hash(password),
            OnboardingCompleted = false,
            AiAssistantEnabled = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await users.AddAsync(entity, ct);
        await users.SaveChangesAsync(ct);

        // 第一个注册的账号接管历史遗留的无归属案例，避免升级后案例"消失"。
        var orphans = await cases.ListAsync(c => !c.IsDeleted && c.UserId == Guid.Empty, ct);
        if (orphans.Count > 0)
        {
            foreach (var orphan in orphans)
            {
                orphan.UserId = entity.Id;
            }

            await cases.SaveChangesAsync(ct);
            logger.LogInformation("账号 {UserName} 已接管 {Count} 个历史案例", entity.UserName, orphans.Count);
        }

        logger.LogInformation("已注册账号 {UserName}", entity.UserName);

        return (true, null, ToProfile(entity));
    }

    public async Task<(bool Success, string? Error, AccountProfile? Profile)> LoginAsync(
        string userName,
        string password,
        CancellationToken ct)
    {
        var normalized = userName.Trim();
        var list = await users.ListAsync(u => u.UserName == normalized, ct);
        var entity = list.FirstOrDefault();

        // 用户名不存在时也走一遍派生，避免通过响应时间探测用户名是否存在。
        var stored = entity?.PasswordHash ?? passwordHasher.Hash("__absent__");
        var matched = passwordHasher.Verify(password, stored);

        if (entity is null || !matched)
        {
            return (false, "登录名或密码不正确。", null);
        }

        entity.LastLoginAt = DateTimeOffset.UtcNow;
        await users.SaveChangesAsync(ct);

        return (true, null, ToProfile(entity));
    }

    public async Task<AccountProfile?> GetAsync(Guid id, CancellationToken ct)
    {
        var entity = await users.FindAsync(id, ct);
        return entity is null ? null : ToProfile(entity);
    }

    public async Task<AccountProfile?> GetByUserNameAsync(string userName, CancellationToken ct)
    {
        var list = await users.ListAsync(u => u.UserName == userName.Trim(), ct);
        var entity = list.FirstOrDefault();
        return entity is null ? null : ToProfile(entity);
    }

    public async Task TouchLoginAsync(Guid id, CancellationToken ct)
    {
        var entity = await users.FindAsync(id, ct);
        if (entity is null)
        {
            return;
        }

        entity.LastLoginAt = DateTimeOffset.UtcNow;
        await users.SaveChangesAsync(ct);
    }

    public async Task CompleteOnboardingAsync(Guid id, CancellationToken ct)
    {
        var entity = await users.FindAsync(id, ct);
        if (entity is null)
        {
            return;
        }

        entity.OnboardingCompleted = true;
        await users.SaveChangesAsync(ct);
    }

    public async Task SaveAiSettingsAsync(Guid id, string? baseUrl, string? model, string? apiKey, CancellationToken ct)
    {
        var entity = await users.FindAsync(id, ct);
        if (entity is null)
        {
            return;
        }

        entity.EncryptedAiBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? null : protector.Protect(baseUrl.Trim());
        entity.EncryptedAiModel = string.IsNullOrWhiteSpace(model) ? null : protector.Protect(model.Trim());
        entity.EncryptedAiApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : protector.Protect(apiKey.Trim());
        entity.AiAssistantEnabled = !string.IsNullOrWhiteSpace(entity.EncryptedAiApiKey);
        entity.OnboardingCompleted = true;

        await users.SaveChangesAsync(ct);

        logger.LogInformation("账号 {UserName} 已更新模型接入设置（启用={Enabled}）", entity.UserName, entity.AiAssistantEnabled);
    }

    public async Task DisableAiAssistantAsync(Guid id, CancellationToken ct)
    {
        var entity = await users.FindAsync(id, ct);
        if (entity is null)
        {
            return;
        }

        entity.AiAssistantEnabled = false;
        entity.EncryptedAiBaseUrl = null;
        entity.EncryptedAiModel = null;
        entity.EncryptedAiApiKey = null;
        entity.OnboardingCompleted = true;

        await users.SaveChangesAsync(ct);

        logger.LogInformation("账号 {UserName} 已切换为完全离线模式", entity.UserName);
    }

    /// <summary>取出账号的模型接入设置（明文，仅在内存中使用）。</summary>
    public async Task<AiAccountSettings?> GetAiSettingsAsync(Guid id, CancellationToken ct)
    {
        var entity = await users.FindAsync(id, ct);
        if (entity is null || !entity.AiAssistantEnabled)
        {
            return null;
        }

        var baseUrl = protector.Unprotect(entity.EncryptedAiBaseUrl);
        var model = protector.Unprotect(entity.EncryptedAiModel);
        var apiKey = protector.Unprotect(entity.EncryptedAiApiKey);

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        return new AiAccountSettings(baseUrl, model, apiKey);
    }

    private static AccountProfile ToProfile(ApplicationUser entity) => new(
        entity.Id,
        entity.UserName,
        entity.DisplayName,
        entity.OnboardingCompleted,
        entity.AiAssistantEnabled,
        !string.IsNullOrWhiteSpace(entity.EncryptedAiApiKey),
        entity.CreatedAt,
        entity.LastLoginAt);

    private static string? ValidateCredentials(string userName, string displayName, string password)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return "请填写登录名。";
        }

        if (userName.Length < 3 || userName.Length > 40)
        {
            return "登录名长度需在 3 到 40 个字符之间。";
        }

        if (userName.Any(char.IsWhiteSpace))
        {
            return "登录名不能包含空格。";
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return "请填写密码。";
        }

        if (password.Length < 8)
        {
            return "密码至少 8 位。";
        }

        if (password.Length > 200)
        {
            return "密码过长。";
        }

        return null;
    }
}
