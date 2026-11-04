using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Application.Services;

/// <summary>
/// 模型接入设置来源：优先当前账号的个人配置，其次全局环境变量配置。
/// </summary>
public sealed class AiSettingsProvider(
    ICurrentAccountContext currentAccount,
    IAccountService accounts) : IAiSettingsProvider
{
    public async Task<AiAccountSettings?> GetCurrentAsync(CancellationToken ct)
    {
        var userId = currentAccount.UserId;
        if (userId is not null)
        {
            var settings = await accounts.GetAiSettingsAsync(userId.Value, ct);
            if (settings is not null)
            {
                return settings;
            }
        }

        // 无人值守场景（后台任务未指定账号、或未登录）回退到全局配置。
        return null;
    }
}
