using System.Text.Json;
using SoftCopyright.Application.Services;
using SoftCopyright.Domain;

namespace SoftCopyright.Application.Validation;

/// <summary>专利申请文件的专属校验规则（交底书必填项 + 权利要求、说明书、摘要、证据、现有技术）。</summary>
public sealed class PatentValidationRuleSet : IMaterialValidationRuleSet
{
    public MaterialKind Kind => MaterialKind.Patent;

    public IReadOnlyList<ValidationResult> Evaluate(MaterialValidationContext context)
    {
        var results = new List<ValidationResult>();
        results.Add(CheckRequiredFields(context.Disclosure));
        results.AddRange(PatentValidationRules.Evaluate(context.Disclosure, context.Claims));
        results.Add(CheckPendingItems(context.Disclosure));
        return results;
    }

    /// <summary>专利案例的必填项以技术交底书为准，不要求填写软著申请表字段。</summary>
    private static ValidationResult CheckRequiredFields(InventionDisclosure? disclosure)
    {
        var missing = new List<string>();

        if (disclosure is null)
        {
            missing.Add("技术交底书");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(disclosure.Title))
            {
                missing.Add("发明名称");
            }

            if (string.IsNullOrWhiteSpace(disclosure.TechnicalSolution))
            {
                missing.Add("技术方案");
            }

            if (string.IsNullOrWhiteSpace(disclosure.ApplicantName))
            {
                missing.Add("申请人");
            }

            if (string.IsNullOrWhiteSpace(disclosure.InventorNames))
            {
                missing.Add("发明人");
            }
        }

        if (missing.Count == 0)
        {
            return new ValidationResult("R-FIELDS-01", ValidationLevel.Pass, "交底书必填项完整性",
                "技术交底书的必填项均已填写。", "MaterialRules", null);
        }

        return new ValidationResult("R-FIELDS-01", ValidationLevel.ActionRequired, "交底书必填项完整性",
            $"以下必填项尚未填写：{string.Join("、", missing)}。",
            "MaterialRules",
            "在「专利交底书」页补全后重新确认。");
    }

    private static ValidationResult CheckPendingItems(InventionDisclosure? disclosure)
    {
        var pending = new List<string>();

        if (disclosure is null)
        {
            pending.Add("尚未填写技术交底书。");
        }
        else if (string.IsNullOrWhiteSpace(disclosure.Title))
        {
            pending.Add("发明名称尚未登记。");
        }

        if (pending.Count == 0)
        {
            return new ValidationResult("R-PENDING-01", ValidationLevel.Pass, "待确认事项",
                "没有遗留的待确认事项。");
        }

        return new ValidationResult("R-PENDING-01", ValidationLevel.ActionRequired, "待确认事项",
            $"仍有 {pending.Count} 项待申请人确认：{string.Join("；", pending.Take(5))}。",
            "OpenQuestions",
            "逐项处理后重新确认。");
    }
}
