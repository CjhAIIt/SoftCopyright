using System.Text;
using System.Text.RegularExpressions;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.AI.Patent;

/// <summary>
/// 规则化专利草稿生成器。
///
/// 设计要点（借鉴开源社区经验并加强）：
/// - **技术特征只从可回溯证据来**：候选要素必须能在证据包中找到出处，否则标记为"待确认"；
/// - **权利要求结构由规则生成**：独立权利要求 + 逐条从属权利要求，引用关系确定可校验；
/// - **至少 3 个实施例**，不足时明确提示补充，避免"公开不充分"；
/// - **摘要限 300 字**。
/// 模型可以润色措辞，但**不得新增技术特征**。
/// </summary>
public sealed partial class RuleBasedPatentDraftGenerator : IPatentDraftGenerator
{
    public string Id => "rule-based-patent";

    private const int AbstractMaxLength = 300;

    /// <summary>技术特征描述的最大长度：只保留可读片段，避免把整段源码写进申请文件。</summary>
    private const int MaxFeatureDescriptionLength = 300;

    /// <summary>实施例描述的最大长度。</summary>
    private const int MaxEmbodimentDescriptionLength = 600;

    public Task<PatentDraftResult> DraftAsync(
        EvidencePack pack,
        InventionDisclosure disclosure,
        CancellationToken ct)
    {
        var warnings = new List<string>();
        var features = ExtractFeatures(pack, disclosure, warnings);
        var embodiments = ExtractEmbodiments(pack, features, warnings);
        var claims = BuildClaims(disclosure, features);
        var abstractText = BuildAbstract(disclosure, features, warnings);

        return Task.FromResult(new PatentDraftResult(abstractText, features, embodiments, claims, warnings));
    }

    /// <summary>从交底书的技术方案中拆出候选技术特征，并逐条绑定证据。</summary>
    private static List<TechnicalFeature> ExtractFeatures(
        EvidencePack pack,
        InventionDisclosure disclosure,
        List<string> warnings)
    {
        var features = new List<TechnicalFeature>();
        var candidates = SplitCandidates(disclosure.TechnicalSolution);

        if (!candidates.Any())
        {
            warnings.Add("技术交底书的「技术方案」为空，未能提取任何技术特征，请先补充。");
            return features;
        }

        foreach (var candidate in candidates)
        {
            var evidence = MatchEvidence(pack, candidate);
            if (evidence.Count == 0)
            {
                warnings.Add($"技术特征「{Truncate(candidate)}」没有找到可回溯的代码证据，已标记为待确认。");
            }

            features.Add(new TechnicalFeature(
                Truncate(candidate, 60),
                candidate,
                evidence,
                evidence.Count > 0));
        }

        // 补充项目层面的实现特征：只取项目标题与描述。
        // README 章节标题属于文档结构（"三、技术栈"之类），不是技术特征，放进权利要求会造成语病；
        // 它们改在实施例中作为项目背景引用。
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var implementationFiles = pack.Items
            .Where(i => i.EvidenceType is "README 标题" or "README 描述")
            .Take(3)
            .ToList();

        foreach (var item in implementationFiles)
        {
            var description = Snippet(item.Content, MaxFeatureDescriptionLength);
            if (!seen.Add(description))
            {
                continue;
            }

            features.Add(new TechnicalFeature(
                $"{Path.GetFileName(item.RelativePath)} 相关实现",
                description,
                [new EvidenceRef(item.RelativePath, item.StartLine, item.EndLine, item.EvidenceType, item.Sha256)],
                true));
        }

        return features;
    }

    /// <summary>
    /// 实施例：优先引用真实代码实现（入口点候选），其次是 README 功能条目，
    /// 最后用已确认的技术特征兜底；不足 3 个时明确提示补充。
    /// </summary>
    private static List<Embodiment> ExtractEmbodiments(
        EvidencePack pack,
        IReadOnlyList<TechnicalFeature> features,
        List<string> warnings)
    {
        var embodiments = new List<Embodiment>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var candidates = pack.Items
            .Where(i => i.EvidenceType is "入口点候选" or "功能条目" or "README 标题")
            .OrderBy(i => i.EvidenceType == "入口点候选" ? 0 : 1)
            .ToList();

        foreach (var item in candidates)
        {
            if (embodiments.Count >= 6)
            {
                break;
            }

            var description = Snippet(item.Content, MaxEmbodimentDescriptionLength);

            // 源码类证据在实施例中要说明"以哪个文件为实现"，否则读者无法理解这段代码的作用。
            if (item.EvidenceType == "入口点候选")
            {
                description = $"以 {item.RelativePath} 为入口的具体实现：\n{description}";
            }

            if (!seen.Add(description))
            {
                continue;
            }

            embodiments.Add(new Embodiment(
                item.EvidenceType == "入口点候选"
                    ? $"实施例 {embodiments.Count + 1}：{item.RelativePath} 的实现"
                    : $"实施例 {embodiments.Count + 1}：{Truncate(item.Content.Trim(), 40)}",
                description,
                [new EvidenceRef(item.RelativePath, item.StartLine, item.EndLine, item.EvidenceType, item.Sha256)],
                true));
        }

        foreach (var feature in features.Where(f => f.Confirmed).Take(3))
        {
            if (embodiments.Count >= 6)
            {
                break;
            }

            if (!seen.Add(feature.Description))
            {
                continue;
            }

            embodiments.Add(new Embodiment(
                $"实施例 {embodiments.Count + 1}：{feature.Title}",
                feature.Description,
                feature.Evidence.ToList(),
                true));
        }

        if (embodiments.Count < 3)
        {
            warnings.Add(
                $"当前仅能生成 {embodiments.Count} 个实施例，少于建议的 3 个。" +
                "请在技术交底书中补充具体实施方式，以保证说明书充分公开。");
        }

        return embodiments;
    }

    /// <summary>权利要求：1 条独立权利要求 + 逐条从属权利要求。</summary>
    private static List<PatentClaimDraft> BuildClaims(
        InventionDisclosure disclosure,
        IReadOnlyList<TechnicalFeature> features)
    {
        var claims = new List<PatentClaimDraft>();
        var title = string.IsNullOrWhiteSpace(disclosure.Title) ? "一种技术方案" : disclosure.Title;

        var core = features.Where(f => f.Confirmed).Select(f => f.Description).ToList();
        var pending = features.Where(f => !f.Confirmed).Select(f => f.Description).ToList();

        var builder = new StringBuilder();
        builder.Append($"{title}，其特征在于");
        if (core.Count > 0)
        {
            builder.Append("，包括：\n");
            for (var i = 0; i < core.Count; i++)
            {
                builder.Append($"    {i + 1}）{core[i]}；\n");
            }

            builder.Append("    其余步骤由权利要求1的特征组合实现。");
        }        else
        {
            builder.Append("，所述方案的具体特征为：\n");
            builder.Append("    【待申请人补充可回溯的技术特征】。");
        }

        claims.Add(new PatentClaimDraft(1, ClaimType.Independent, null, builder.ToString(), features));

        var number = 2;
        foreach (var feature in features)
        {
            // 权利要求正文不写死编号：编号由文档生成器统一输出，避免正文与编号表不一致。
            var text = feature.Confirmed
                ? $"根据权利要求1所述的{title}，其特征在于，所述特征还包括：{feature.Description}。"
                : $"根据权利要求1所述的{title}，其特征在于，【待申请人补充并核对证据】：{feature.Description}。";

            claims.Add(new PatentClaimDraft(number, ClaimType.Dependent, 1, text, [feature]));
            number++;
        }

        if (pending.Count > 0)
        {
            // 提示由调用方通过 warnings 呈现，这里不重复写入。
        }

        return claims;
    }

    /// <summary>摘要：技术领域 + 技术问题 + 技术方案首句 + 有益效果，限 300 字。</summary>
    private static string BuildAbstract(
        InventionDisclosure disclosure,
        IReadOnlyList<TechnicalFeature> features,
        List<string> warnings)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(disclosure.TechnicalField))
        {
            parts.Add(FieldPhrase(disclosure.TechnicalField));
        }

        if (!string.IsNullOrWhiteSpace(disclosure.ProblemStatement))
        {
            parts.Add($"针对{disclosure.ProblemStatement}的问题");
        }

        var solution = features.FirstOrDefault(f => f.Confirmed)?.Description ?? disclosure.TechnicalSolution;
        if (!string.IsNullOrWhiteSpace(solution))
        {
            parts.Add($"提出一种技术方案：{solution}");
        }

        if (!string.IsNullOrWhiteSpace(disclosure.BeneficialEffects))
        {
            parts.Add($"其有益效果是{disclosure.BeneficialEffects}");
        }

        var text = string.Join("，", parts.Where(p => !string.IsNullOrWhiteSpace(p))) + "。";
        if (text.Length > AbstractMaxLength)
        {
            text = text[..AbstractMaxLength];
            warnings.Add($"摘要已按 300 字上限截断，请人工补全语义。");
        }

        return text;
    }

    /// <summary>
    /// 技术领域措辞：申请人往往已经写了"本申请涉及……领域"，
    /// 再套一层模板会得到"本申请涉及本申请涉及……领域领域"这种重复表述。
    /// </summary>
    private static string FieldPhrase(string field)
    {
        var text = field.Trim();
        if (text.Contains("本申请涉及", StringComparison.Ordinal) || text.Contains("领域", StringComparison.Ordinal))
        {
            return text;
        }

        return $"本申请涉及{text}领域";
    }

    private static IEnumerable<string> SplitCandidates(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var segments = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split(['；', ';', '\n'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            var trimmed = segment.Trim(' ', '。', '.', '、', '，');
            if (trimmed.Length >= 4)
            {
                yield return trimmed;
            }
        }
    }

    /// <summary>在证据包中为候选要素寻找出处：按关键词命中率匹配。</summary>
    private static List<EvidenceRef> MatchEvidence(EvidencePack pack, string candidate)
    {
        var keywords = ExtractKeywords(candidate);

        if (keywords.Count == 0)
        {
            return [];
        }

        // 中文候选要素切出的是二元组，命中率天然偏低，因此阈值取绝对命中数而不是比例。
        var threshold = keywords.Count <= 2 ? keywords.Count : Math.Max(3, keywords.Count / 8);

        var matches = new List<EvidenceRef>();
        foreach (var item in pack.Items)
        {
            var content = item.Content;
            var hits = keywords.Count(k => content.Contains(k, StringComparison.OrdinalIgnoreCase));
            if (hits >= threshold)
            {
                matches.Add(new EvidenceRef(item.RelativePath, item.StartLine, item.EndLine, item.EvidenceType, item.Sha256));
            }

            if (matches.Count >= 3)
            {
                break;
            }
        }

        return matches;
    }

    /// <summary>
    /// 抽取候选要素的检索关键词。
    /// 中文没有空格分词，按标点切分会把整句当成一个词，导致永远匹配不到证据；
    /// 因此中文按二元组切分（覆盖常见双字术语），英文与数字按词切分。
    /// </summary>
    private static List<string> ExtractKeywords(string candidate)
    {
        var keywords = new List<string>();

        foreach (var token in candidate.Split(
                     [' ', '\t', '\n', '，', ',', '；', ';', '（', '）', '(', ')', '/', '、', '。', '.', '"', '“', '”'],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var text = token.Trim();
            if (text.Length < 2 || !text.Any(char.IsLetter))
            {
                continue;
            }

            // 纯中文串交给二元组处理，避免把整句当成一个词。
            if (text.All(IsCjk))
            {
                continue;
            }

            keywords.Add(text);
        }

        foreach (Match run in ChineseRunPattern().Matches(candidate))
        {
            var value = run.Value;
            for (var i = 0; i + 1 < value.Length; i++)
            {
                keywords.Add(value.Substring(i, 2));
            }
        }

        return keywords
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(32)
            .ToList();
    }

    [GeneratedRegex(@"[\u4e00-\u9fff]{2,}")]
    private static partial Regex ChineseRunPattern();

    /// <summary>是否为中日韩统一表意文字。</summary>
    private static bool IsCjk(char c) => c >= '\u4e00' && c <= '\u9fff';

    private static string Truncate(string text, int max = 60) =>
        text.Length <= max ? text : text[..max] + "…";

    /// <summary>
    /// 取证据内容的可读片段。原文可能长达数十 KB，直接写入申请文件既无意义，
    /// 也会撑爆数据库列，因此截断并显式标注，完整内容以证据出处为准。
    /// </summary>
    private static string Snippet(string content, int max)
    {
        var text = content.Trim();
        if (text.Length <= max)
        {
            return text;
        }

        return text[..max].TrimEnd() + $"…（内容已截断，完整内容见证据出处）";
    }
}
