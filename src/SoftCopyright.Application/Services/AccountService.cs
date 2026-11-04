using Microsoft.Extensions.Logging;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Application.Services;

/// <summary>本地账号服务：注册、登录、引导状态、管理员管理与模型接入设置。</summary>
public sealed class AccountService(
    IRepository<ApplicationUser> users,
    IRepository<ApplicationCase> cases,
    IRepository<GeneratedArtifact> artifacts,
    IRepository<BackgroundJob> jobs,
    IRepository<ProjectFile> files,
    IPasswordHasher passwordHasher,
    ISecretProtector protector,
    ILogger<AccountService> logger) : IAccountService
{
    /// <summary>内置管理员的登录名。</summary>
    public const string AdminUserName = "admin";

    /// <summary>
    /// 内置管理员的默认密码哈希（PBKDF2-SHA256）。
    /// 仓库中只有哈希、没有明文；可用环境变量
    /// <c>SOFTCOPYRIGHT_ADMIN_PASSWORD</c> 在首次启动时改用自定义密码。
    /// </summary>
    public const string AdminPasswordHash =
        "PBKDF2-SHA256$210000$smMkIpZIvhGsK+sw6VAWgw==$/3DwLAz7LfZEMvBv9Gmos1oci6/KheDa5+KB+V3WSzk=";

    /// <summary>可覆盖内置管理员密码的环境变量名。</summary>
    public const string AdminPasswordEnvironmentVariable = "SOFTCOPYRIGHT_ADMIN_PASSWORD";

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

        // 用户名按不区分大小写比较：统一用 ToLower（EF 可翻译为 SQL LOWER），
        // 不依赖数据库排序规则，避免不同实例上行为不一致。
        var existing = await users.ListAsync(u => u.UserName.ToLower() == normalized.ToLower(), ct);
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
        var list = await users.ListAsync(u => u.UserName.ToLower() == normalized.ToLower(), ct);
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

    /// <summary>
    /// 幂等创建内置管理员账号。
    /// 已存在时只补齐管理员标记与引导状态，**不重置密码**，
    /// 以免每次启动都把管理员改回默认密码。
    /// </summary>
    public async Task EnsureAdminAccountAsync(CancellationToken ct)
    {
        var existing = await users.ListAsync(u => u.UserName.ToLower() == AdminUserName.ToLower(), ct);
        var entity = existing.FirstOrDefault();

        if (entity is null)
        {
            var overridePassword = Environment.GetEnvironmentVariable(AdminPasswordEnvironmentVariable);
            var hash = string.IsNullOrWhiteSpace(overridePassword)
                ? AdminPasswordHash
                : passwordHasher.Hash(overridePassword);

            entity = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = AdminUserName,
                DisplayName = "系统管理员",
                PasswordHash = hash,
                IsAdmin = true,
                OnboardingCompleted = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            await users.AddAsync(entity, ct);
            await users.SaveChangesAsync(ct);

            logger.LogInformation(
                "已创建内置管理员账号 {UserName}（{Mode}）",
                AdminUserName,
                string.IsNullOrWhiteSpace(overridePassword) ? "默认密码" : "自定义密码");

            return;
        }

        var changed = false;
        if (!entity.IsAdmin)
        {
            entity.IsAdmin = true;
            changed = true;
        }

        if (!entity.OnboardingCompleted)
        {
            entity.OnboardingCompleted = true;
            changed = true;
        }

        if (changed)
        {
            await users.SaveChangesAsync(ct);
        }
    }

    /// <summary>列出全部账号，供管理端查看。</summary>
    public async Task<IReadOnlyList<AdminAccountView>> ListAccountsAsync(CancellationToken ct)
    {
        var all = await users.ListAsync(null, ct);
        var allCases = await cases.ListAsync(c => !c.IsDeleted, ct);

        var views = new List<AdminAccountView>();
        foreach (var entity in all.OrderBy(u => u.CreatedAt))
        {
            var profile = ToProfile(entity);
            var caseCount = allCases.Count(c => c.UserId == entity.Id);
            var mode = entity.AiAssistantEnabled && !string.IsNullOrWhiteSpace(entity.EncryptedAiApiKey)
                ? "已配置密钥"
                : "离线（免费）";

            views.Add(new AdminAccountView(profile, entity.IsAdmin, caseCount, mode));
        }

        return views;
    }

    /// <summary>删除账号。管理员不能删除自己，也不能删除最后一个管理员。</summary>
    public async Task<(bool Success, string? Error)> DeleteAccountAsync(Guid id, Guid requesterId, CancellationToken ct)
    {
        var entity = await users.FindAsync(id, ct);
        if (entity is null)
        {
            return (false, "账号不存在。");
        }

        if (entity.Id == requesterId)
        {
            return (false, "不能删除当前登录的管理员账号。");
        }

        if (entity.IsAdmin)
        {
            var admins = await users.ListAsync(u => u.IsAdmin, ct);
            if (admins.Count <= 1)
            {
                return (false, "系统至少需要保留一个管理员账号。");
            }
        }

        // 连同该账号名下的案例一起清理，避免留下无人可见的孤儿数据。
        var owned = await cases.ListAsync(c => c.UserId == entity.Id, ct);
        foreach (var ownedCase in owned)
        {
            ownedCase.IsDeleted = true;
            ownedCase.DeletedAt = DateTimeOffset.UtcNow;
        }

        users.Remove(entity);
        await users.SaveChangesAsync(ct);

        logger.LogInformation("管理员已删除账号 {UserName}（连带 {Cases} 个案例）", entity.UserName, owned.Count);
        return (true, null);
    }

    /// <summary>数据总览，供管理端查看。</summary>
    public async Task<AdminOverview> GetOverviewAsync(CancellationToken ct)
    {
        return new AdminOverview(
            await users.CountAsync(null, ct),
            await users.CountAsync(u => u.IsAdmin, ct),
            await users.CountAsync(u => u.AiAssistantEnabled && u.EncryptedAiApiKey != null, ct),
            await cases.CountAsync(c => !c.IsDeleted, ct),
            await cases.CountAsync(c => !c.IsDeleted && c.MaterialKind == MaterialKind.SoftwareCopyright, ct),
            await cases.CountAsync(c => !c.IsDeleted && c.MaterialKind == MaterialKind.Patent, ct),
            await artifacts.CountAsync(null, ct),
            await jobs.CountAsync(j => j.Status == JobStatus.Pending || j.Status == JobStatus.Running, ct),
            await jobs.CountAsync(j => j.Status == JobStatus.Failed, ct),
            await files.CountAsync(f => f.SecretRisk == SecretRisk.Suspected, ct));
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
        entity.IsAdmin,
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
