using Microsoft.Extensions.Options;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Domain.Options;

namespace SoftCopyright.AI.Patent;

/// <summary>按配置选择专利草稿生成器：默认规则版，配置了模型时用模型润色。</summary>
public sealed class PatentDraftGeneratorRouter(
    RuleBasedPatentDraftGenerator ruleBased,
    HttpPatentDraftGenerator http,
    IOptions<AiOptions> options) : IPatentDraftGenerator
{
    public string Id => "router";

    public Task<PatentDraftResult> DraftAsync(
        EvidencePack pack,
        InventionDisclosure disclosure,
        CancellationToken ct)
    {
        var provider = options.Value.Provider?.Trim().ToLowerInvariant() ?? "rule-based";

        return provider switch
        {
            "http" => http.DraftAsync(pack, disclosure, ct),
            _ => ruleBased.DraftAsync(pack, disclosure, ct)
        };
    }
}
