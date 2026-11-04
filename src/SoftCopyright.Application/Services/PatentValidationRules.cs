using System.Text.Json;
using SoftCopyright.Domain;

namespace SoftCopyright.Application.Services;

/// <summary>
/// 专利材料校验规则（对应开源社区的"质量检查清单"）。
/// 只做结构性检查，不对新颖性、创造性作任何判断。
/// </summary>
public static class PatentValidationRules
{
    public const int AbstractMaxLength = 300;
    public const int RecommendedEmbodimentCount = 3;

    public static IReadOnlyList<ValidationResult> Evaluate(
        InventionDisclosure? disclosure,
        IReadOnlyList<PatentClaim> claims)
    {
        var results = new List<ValidationResult>();

        if (disclosure is null)
        {
            results.Add(new ValidationResult(
                "P-DISC-01", ValidationLevel.ActionRequired, "技术交底书",
                "尚未填写技术交底书。", "PatentDisclosure", "请在「专利交底书」页填写技术领域、技术问题、技术方案与有益效果。"));
            return results;
        }

        results.Add(CheckClaimNumbering(claims));
        results.Add(CheckClaimDependencies(claims));
        results.Add(CheckClaimFeatures(claims));
        results.Add(CheckSpecificationSections(disclosure));
        results.Add(CheckEmbodiments(disclosure));
        results.Add(CheckAbstract(disclosure));
        results.Add(CheckFeatureEvidence(disclosure));
        results.Add(CheckPriorArt(disclosure));
        results.Add(CheckAgentReview());

        return results;
    }

    /// <summary>
    /// 常驻声明：本工具只产出申请文件初稿，不对保护范围、新颖性、创造性作判断，
    /// 也不代为检索现有技术，最终结论必须由专利代理人给出。
    /// </summary>
    private static ValidationResult CheckAgentReview() =>
        new("P-REVIEW-01", ValidationLevel.Pass, "人工复核声明",
            "本工具仅生成专利申请文件初稿；权利要求的保护范围、新颖性与创造性必须由专利代理人复核，本工具不代为判断，也不代为检索现有技术。",
            "AgentReview",
            "提交申请前请交由专利代理人审阅权利要求与说明书。");

    private static ValidationResult CheckClaimNumbering(IReadOnlyList<PatentClaim> claims)
    {
        if (claims.Count == 0)
        {
            return new ValidationResult(
                "P-CLAIM-01", ValidationLevel.ActionRequired, "权利要求编号",
                "尚未生成权利要求书。", "PatentClaims", "先填写技术交底书，再生成专利草稿。");
        }

        var numbers = claims.Select(c => c.Number).OrderBy(n => n).ToList();
        var expected = Enumerable.Range(1, claims.Count).ToList();

        if (!numbers.SequenceEqual(expected))
        {
            return new ValidationResult(
                "P-CLAIM-01", ValidationLevel.ActionRequired, "权利要求编号",
                $"权利要求编号不连续：实际为 {string.Join("、", numbers)}。", "PatentClaims", "重新生成权利要求书。");
        }

        var independent = claims.Count(c => c.Type == ClaimType.Independent);
        if (independent != 1)
        {
            return new ValidationResult(
                "P-CLAIM-01", ValidationLevel.Warning, "权利要求编号",
                $"权利要求书中有 {independent} 条独立权利要求，按惯例应只有 1 条。", "PatentClaims", "请由专利代理人复核权利要求的层次。");
        }

        return new ValidationResult(
            "P-CLAIM-01", ValidationLevel.Pass, "权利要求编号",
            $"共 {claims.Count} 条权利要求，编号连续且独立权利要求唯一。");
    }

    private static ValidationResult CheckClaimDependencies(IReadOnlyList<PatentClaim> claims)
    {
        var byNumber = claims.ToDictionary(c => c.Number);
        var problems = new List<string>();

        foreach (var claim in claims.Where(c => c.Type == ClaimType.Dependent))
        {
            if (claim.DependsOn is null)
            {
                problems.Add($"权利要求 {claim.Number} 标为从属但未引用母项");
                continue;
            }

            if (!byNumber.TryGetValue(claim.DependsOn.Value, out var parent))
            {
                problems.Add($"权利要求 {claim.Number} 引用的权利要求 {claim.DependsOn} 不存在");
                continue;
            }

            if (claim.DependsOn.Value >= claim.Number)
            {
                problems.Add($"权利要求 {claim.Number} 引用了在其之后的权利要求 {claim.DependsOn}");
            }

            if (parent.Type != ClaimType.Independent && parent.DependsOn is not null)
            {
                problems.Add($"权利要求 {claim.Number} 引用了同样为从属的权利要求 {claim.DependsOn}（应引用独立权利要求）");
            }
        }

        if (problems.Count > 0)
        {
            return new ValidationResult(
                "P-CLAIM-02", ValidationLevel.ActionRequired, "权利要求引用关系",
                string.Join("；", problems), "PatentClaims", "检查从属权利要求的引用对象与前后顺序。");
        }

        return new ValidationResult(
            "P-CLAIM-02", ValidationLevel.Pass, "权利要求引用关系",
            "所有从属权利要求的引用对象存在、编号在前且引用链正确。");
    }

    private static ValidationResult CheckClaimFeatures(IReadOnlyList<PatentClaim> claims)
    {
        var empty = claims.Where(c => string.IsNullOrWhiteSpace(c.Text)).Select(c => c.Number).ToList();
        if (empty.Count > 0)
        {
            return new ValidationResult(
                "P-CLAIM-03", ValidationLevel.ActionRequired, "权利要求技术特征",
                $"权利要求 {string.Join("、", empty)} 没有正文内容。", "PatentClaims", "重新生成权利要求书。");
        }

        var placeholders = claims.Where(c => c.Text.Contains("待申请人补充", StringComparison.Ordinal)).Select(c => c.Number).ToList();
        if (placeholders.Count > 0)
        {
            return new ValidationResult(
                "P-CLAIM-03", ValidationLevel.ActionRequired, "权利要求技术特征",
                $"权利要求 {string.Join("、", placeholders)} 中的技术特征仍为占位内容。",
                "PatentClaims", "在技术交底书中补充技术方案，使每个特征都有可回溯依据。");
        }

        return new ValidationResult(
            "P-CLAIM-03", ValidationLevel.Pass, "权利要求技术特征",
            "每条权利要求均已写入具体技术特征，无占位内容。");
    }

    private static ValidationResult CheckSpecificationSections(InventionDisclosure disclosure)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(disclosure.TechnicalField)) missing.Add("技术领域");
        if (string.IsNullOrWhiteSpace(disclosure.BackgroundTechnology)) missing.Add("背景技术");
        if (string.IsNullOrWhiteSpace(disclosure.ProblemStatement)) missing.Add("发明内容（要解决的技术问题）");
        if (string.IsNullOrWhiteSpace(disclosure.TechnicalSolution)) missing.Add("发明内容（技术方案）");
        if (string.IsNullOrWhiteSpace(disclosure.DrawingDescription)) missing.Add("附图说明");
        if (string.IsNullOrWhiteSpace(disclosure.EmbodimentsJson) || disclosure.EmbodimentsJson == "[]") missing.Add("具体实施方式");

        if (missing.Count > 0)
        {
            return new ValidationResult(
                "P-SPEC-01", ValidationLevel.ActionRequired, "说明书章节完整性",
                $"说明书缺少以下章节：{string.Join("、", missing)}。",
                "PatentDisclosure",
                "按《专利法实施细则》说明书五部分补齐：技术领域、背景技术、发明内容、附图说明、具体实施方式。");
        }

        return new ValidationResult(
            "P-SPEC-01", ValidationLevel.Pass, "说明书章节完整性",
            "说明书五部分（技术领域、背景技术、发明内容、附图说明、具体实施方式）齐全。");
    }

    private static ValidationResult CheckEmbodiments(InventionDisclosure disclosure)
    {
        var count = Deserialize<Embodiment>(disclosure.EmbodimentsJson).Count;

        if (count < RecommendedEmbodimentCount)
        {
            return new ValidationResult(
                "P-SPEC-02", ValidationLevel.Warning, "实施例数量",
                $"当前只有 {count} 个实施例，少于建议的 {RecommendedEmbodimentCount} 个，说明书可能公开不充分。",
                "PatentDisclosure", "补充不同参数或场景下的实施例。");
        }

        return new ValidationResult(
            "P-SPEC-02", ValidationLevel.Pass, "实施例数量",
            $"共 {count} 个实施例，具备基本的多例公开。");
    }

    private static ValidationResult CheckAbstract(InventionDisclosure disclosure)
    {
        if (string.IsNullOrWhiteSpace(disclosure.Abstract))
        {
            return new ValidationResult(
                "P-ABS-01", ValidationLevel.ActionRequired, "摘要",
                "尚未生成摘要。", "PatentDisclosure", "生成专利草稿后会自动生成摘要。");
        }

        var length = disclosure.Abstract.Length;
        if (length > AbstractMaxLength)
        {
            return new ValidationResult(
                "P-ABS-01", ValidationLevel.ActionRequired, "摘要",
                $"摘要 {length} 字，超过 {AbstractMaxLength} 字上限。", "PatentDisclosure", "请精简摘要。");
        }

        return new ValidationResult(
            "P-ABS-01", ValidationLevel.Pass, "摘要",
            $"摘要 {length} 字，符合 {AbstractMaxLength} 字上限。");
    }

    private static ValidationResult CheckFeatureEvidence(InventionDisclosure disclosure)
    {
        var features = Deserialize<TechnicalFeature>(disclosure.TechnicalFeaturesJson);
        if (features.Count == 0)
        {
            return new ValidationResult(
                "P-EVID-01", ValidationLevel.Warning, "技术特征证据绑定",
                "尚未提取技术特征。", "PatentDisclosure", "生成专利草稿后会按证据自动提取技术特征。");
        }

        var unconfirmed = features.Where(f => !f.Confirmed).Select(f => f.Title).ToList();
        if (unconfirmed.Count > 0)
        {
            return new ValidationResult(
                "P-EVID-01", ValidationLevel.Warning, "技术特征证据绑定",
                $"有 {unconfirmed.Count} 项技术特征没有找到可回溯的代码证据：{string.Join("；", unconfirmed.Take(5))}。",
                "PatentDisclosure",
                "这些特征由申请人提供、尚未在仓库中找到对应实现，需补充证据或核对说明书表述。");
        }

        return new ValidationResult(
            "P-EVID-01", ValidationLevel.Pass, "技术特征证据绑定",
            $"全部 {features.Count} 项技术特征均绑定了可回溯的代码证据。");
    }

    private static ValidationResult CheckPriorArt(InventionDisclosure disclosure)
    {
        if (string.IsNullOrWhiteSpace(disclosure.PriorArt))
        {
            return new ValidationResult(
                "P-PRIOR-01", ValidationLevel.Warning, "现有技术来源",
                "未填写现有技术。系统不代为检索，也不编造对比文件。",
                "PatentDisclosure", "请填写已知的现有技术及其出处（如公开专利号、文献名称）。");
        }

        if (string.IsNullOrWhiteSpace(disclosure.PriorArtSources))
        {
            return new ValidationResult(
                "P-PRIOR-01", ValidationLevel.ActionRequired, "现有技术来源",
                "填写了现有技术但没有注明来源，存在编造对比文件的风险。",
                "PatentDisclosure", "为每条现有技术标注可核验的出处。");
        }

        return new ValidationResult(
            "P-PRIOR-01", ValidationLevel.Pass, "现有技术来源",
            "现有技术已注明来源。");
    }

    private static List<T> Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, Mapping.JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
