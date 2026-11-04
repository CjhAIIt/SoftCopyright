using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Options;

namespace SoftCopyright.Application.Validation;

/// <summary>软件著作权登记的专属校验规则（申请表字段、一致性、源程序量、代码可回溯性）。</summary>
public sealed partial class SoftCopyrightValidationRuleSet(IOptions<MaterialRules> rulesOptions) : IMaterialValidationRuleSet
{
    private readonly MaterialRules _rules = rulesOptions.Value;

    public MaterialKind Kind => MaterialKind.SoftwareCopyright;

    public IReadOnlyList<ValidationResult> Evaluate(MaterialValidationContext context)
    {
        var results = new List<ValidationResult>();
        results.AddRange(CheckRequiredFields(context.ApplicantInfo));
        results.Add(CheckConsistency(context.Case, context.ApplicantInfo));
        results.Add(CheckSourceProgramAmount(context.ApplicantInfo, context.CodeSelectionItems));
        results.Add(CheckTraceability(context.CodeSelectionItems, context.ProjectFiles));
        results.Add(CheckPendingItems(context.Case, context.Analysis));
        return results;
    }

    private IEnumerable<ValidationResult> CheckRequiredFields(ApplicantInfo? info)
    {
        var missing = new List<string>();

        foreach (var field in _rules.Fields.RequiredFields)
        {
            var value = field switch
            {
                nameof(ApplicantInfo.SoftwareFullName) => info?.SoftwareFullName,
                nameof(ApplicantInfo.VersionNumber) => info?.VersionNumber,
                nameof(ApplicantInfo.CopyrightOwner) => info?.CopyrightOwner,
                nameof(ApplicantInfo.DevelopmentMode) => info?.DevelopmentMode,
                nameof(ApplicantInfo.RightsAcquisitionMethod) => info?.RightsAcquisitionMethod,
                nameof(ApplicantInfo.DevelopmentEnvironment) => info?.DevelopmentEnvironment,
                nameof(ApplicantInfo.RuntimeEnvironment) => info?.RuntimeEnvironment,
                nameof(ApplicantInfo.ProgrammingLanguages) => info?.ProgrammingLanguages,
                nameof(ApplicantInfo.SourceProgramAmount) => info?.SourceProgramAmount,
                nameof(ApplicantInfo.FunctionSummary) => info?.FunctionSummary,
                _ => null
            };

            if (string.IsNullOrWhiteSpace(value))
            {
                missing.Add(field);
            }
        }

        if (missing.Count == 0)
        {
            yield return new ValidationResult("R-FIELDS-01", ValidationLevel.Pass, "必填字段完整性",
                "必填字段均已填写。", "MaterialRules", null);
        }
        else
        {
            yield return new ValidationResult("R-FIELDS-01", ValidationLevel.ActionRequired, "必填字段完整性",
                $"以下必填字段尚未填写：{string.Join("、", missing)}。",
                "MaterialRules",
                "在「申请信息」页补全后重新确认。");
        }
    }

    private static ValidationResult CheckConsistency(ApplicationCase caseEntity, ApplicantInfo? info)
    {
        if (info is null || string.IsNullOrWhiteSpace(info.SoftwareFullName))
        {
            return new ValidationResult("R-CONSIST-01", ValidationLevel.ActionRequired, "名称与版本一致性",
                "尚未填写软件全称，无法核对一致性。", "MaterialRules", "先填写申请信息。");
        }

        var nameMatches = string.IsNullOrWhiteSpace(caseEntity.SoftwareFullName) ||
                          string.Equals(caseEntity.SoftwareFullName, info.SoftwareFullName, StringComparison.Ordinal);

        var versionMatches = string.IsNullOrWhiteSpace(caseEntity.SoftwareVersion) ||
                             string.Equals(caseEntity.SoftwareVersion, info.VersionNumber, StringComparison.Ordinal);

        if (nameMatches && versionMatches)
        {
            return new ValidationResult("R-CONSIST-01", ValidationLevel.Pass, "名称与版本一致性",
                "案例登记的软件全称与版本号在申请表、操作手册与代码材料之间保持一致。");
        }

        return new ValidationResult("R-CONSIST-01", ValidationLevel.ActionRequired, "名称与版本一致性",
            "案例登记的软件全称或版本号与当前申请信息不一致。", "MaterialRules", "保存申请信息后重新生成材料。");
    }

    private static ValidationResult CheckSourceProgramAmount(ApplicantInfo? info, IReadOnlyList<CodeSelectionItem> items)
    {
        var actual = items.Where(i => i.Included).Sum(i => i.LineCount);
        var match = NumberPattern().Match(info?.SourceProgramAmount ?? string.Empty);
        if (!match.Success)
        {
            return new ValidationResult("R-CONSIST-02", ValidationLevel.Warning, "源程序量一致性",
                "无法从申请信息中解析源程序量数值，请人工核对材料中的实际行数。",
                "SourceProgramAmount",
                $"当前选中代码实际约 {actual} 行。");
        }

        var declared = int.Parse(match.Value);
        if (Math.Abs(declared - actual) <= Math.Max(10, actual * 0.05))
        {
            return new ValidationResult("R-CONSIST-02", ValidationLevel.Pass, "源程序量一致性",
                $"申请信息填报 {declared} 行，材料实际 {actual} 行，差异在允许范围内。");
        }

        return new ValidationResult("R-CONSIST-02", ValidationLevel.Warning, "源程序量一致性",
            $"申请信息填报 {declared} 行，材料实际 {actual} 行，存在差异。",
            "SourceProgramAmount",
            $"请调整填报值或勾选范围，当前实际约 {actual} 行。");
    }

    private static ValidationResult CheckTraceability(IReadOnlyList<CodeSelectionItem> items, IReadOnlyList<ProjectFile> files)
    {
        var included = items.Where(i => i.Included).ToList();
        if (included.Count == 0)
        {
            return new ValidationResult("R-TRACE-01", ValidationLevel.ActionRequired, "代码可回溯性",
                "尚未选择任何源文件。", "CodeSelection", "请先勾选纳入材料包的真实源文件。");
        }

        var mismatched = included
            .Where(item => files.FirstOrDefault(f =>
                string.Equals(f.RelativePath, item.RelativePath, StringComparison.OrdinalIgnoreCase)) is not { } file ||
                !string.Equals(file.Sha256, item.Sha256, StringComparison.OrdinalIgnoreCase))
            .Select(i => i.RelativePath)
            .ToList();

        if (mismatched.Count == 0)
        {
            return new ValidationResult("R-TRACE-01", ValidationLevel.Pass, "代码可回溯性",
                $"全部 {included.Count} 个代码文件的哈希与仓库原始文件一致，可精确回溯。");
        }

        return new ValidationResult("R-TRACE-01", ValidationLevel.ActionRequired, "代码可回溯性",
            $"以下文件在确认后发生了变化：{string.Join("、", mismatched)}。",
            "CodeSelection",
            "重新扫描并重新确认代码范围。");
    }

    private static ValidationResult CheckPendingItems(ApplicationCase caseEntity, ProjectAnalysis? analysis)
    {
        var pending = new List<string>();

        if (analysis is not null)
        {
            try
            {
                var questions = JsonSerializer.Deserialize<List<string>>(
                    analysis.OpenQuestionsJson, Mapping.JsonOptions) ?? [];
                pending.AddRange(questions.Where(q => !string.IsNullOrWhiteSpace(q)));
            }
            catch (JsonException)
            {
                // 解析失败时不阻断校验。
            }
        }

        if (caseEntity.SoftwareFullName is null)
        {
            pending.Add("软件全称尚未登记。");
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

    [GeneratedRegex(@"\d+")]
    private static partial Regex NumberPattern();
}
