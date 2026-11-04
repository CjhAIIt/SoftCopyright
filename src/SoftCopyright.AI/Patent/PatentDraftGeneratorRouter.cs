using Microsoft.Extensions.Options;
using SoftCopyright.AI.Patent;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Domain.Options;

namespace SoftCopyright.AI.Patent;

/// <summary>
/// 专利草稿生成器路由。
///
/// 优先级：
/// 1. 当前账号在引导流程中配置了模型密钥 → 用模型润色（仍受"不得新增技术特征"的强约束）；
/// 2. 全局配置为 mock → 用内置模拟；
/// 3. 其余情况 → 用完全离线的规则版本。
/// </summary>
public sealed class PatentDraftGeneratorRouter(
    RuleBasedPatentDraftGenerator ruleBased,
    HttpPatentDraftGenerator http,
    IAiProvider provider,
    IOptions<AiOptions> options) : IPatentDraftGenerator
{
    public string Id => "router";

    public async Task<PatentDraftResult> DraftAsync(
        EvidencePack pack,
        InventionDisclosure disclosure,
        CancellationToken ct)
    {
        var globalProvider = options.Value.Provider?.Trim().ToLowerInvariant() ?? "rule-based";

        if (globalProvider == "mock")
        {
            return await ruleBased.DraftAsync(pack, disclosure, ct);
        }

        // 账号配置优先：引导流程里用户明确选择了使用模型。
        if (await provider.ResolveAsync(ct) is not null)
        {
            return await http.DraftAsync(pack, disclosure, ct);
        }

        return await ruleBased.DraftAsync(pack, disclosure, ct);
    }
}
