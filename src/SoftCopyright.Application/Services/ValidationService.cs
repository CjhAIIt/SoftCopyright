using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Domain.Options;

namespace SoftCopyright.Application.Services;

/// <summary>校验中心：把自动化规则检查结果分成通过、警告与需申请人处理三级。</summary>
public sealed partial class ValidationService(
    IRepository<ApplicationCase> cases,
    IRepository<ApplicantInfo> applicantInfos,
    IRepository<CodeSelection> selections,
    IRepository<CodeSelectionItem> selectionItems,
    IRepository<ProjectFile> projectFiles,
    IRepository<GeneratedArtifact> artifacts,
    IRepository<ProjectAnalysis> analyses,
    IFileStore fileStore,
    IOptions<MaterialRules> rulesOptions) : IValidationService
{
    private readonly MaterialRules _rules = rulesOptions.Value;

    public async Task<IReadOnlyList<ValidationResult>> ValidateAsync(Guid caseId, CancellationToken ct, bool includeArtifactCheck = true)
    {
        var results = new List<ValidationResult>();

        var caseEntity = await cases.FindAsync(caseId, ct);
        if (caseEntity is null)
        {
            throw new NotFoundException("申请案例不存在。");
        }

        var info = (await applicantInfos.ListAsync(a => a.CaseId == caseId, ct)).FirstOrDefault();
        var selection = (await selections.ListAsync(s => s.CaseId == caseId, ct)).FirstOrDefault();
        var items = selection is null
            ? []
            : await selectionItems.ListAsync(i => i.SelectionId == selection.Id, ct);
        var files = await projectFiles.ListAsync(f => f.CaseId == caseId, ct);
        var caseArtifacts = await artifacts.ListAsync(a => a.CaseId == caseId, ct);
        var analysis = (await analyses.ListAsync(a => a.CaseId == caseId, ct)).OrderByDescending(a => a.CreatedAt).FirstOrDefault();

        results.AddRange(CheckRequiredFields(info));
        results.Add(CheckConsistency(caseEntity, info));
        results.Add(CheckSourceProgramAmount(info, items));
        results.Add(CheckTraceability(items, files));
        results.Add(CheckArtifacts(caseArtifacts, includeArtifactCheck));
        results.Add(CheckRulesVersion(caseEntity));
        results.Add(CheckPendingItems(caseEntity, analysis));
        results.Add(CheckSecrets(files));
        results.Add(CheckCoverage(analysis));

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

    private ValidationResult CheckArtifacts(IReadOnlyList<GeneratedArtifact> caseArtifacts, bool includeArtifactCheck)
    {
        if (!includeArtifactCheck)
        {
            return new ValidationResult("R-FILE-01", ValidationLevel.Pass, "产物完整性",
                "本次产物正在生成中，生成完成后会再次校验文件是否完整可读。");
        }

        var fresh = caseArtifacts.Where(a => !a.IsStale).ToList();
        if (fresh.Count == 0)
        {
            return new ValidationResult("R-FILE-01", ValidationLevel.ActionRequired, "产物完整性",
                "尚未生成可用的材料产物。", "Artifacts", "请先生成材料。");
        }

        var missing = new List<string>();
        foreach (var artifact in fresh)
        {
            try
            {
                var path = fileStore.ResolveSafePath(artifact.RelativePath);
                if (!File.Exists(path) || new FileInfo(path).Length == 0)
                {
                    missing.Add(artifact.FileName);
                }
            }
            catch (InvalidOperationException)
            {
                missing.Add(artifact.FileName);
            }
        }

        if (missing.Count == 0)
        {
            return new ValidationResult("R-FILE-01", ValidationLevel.Pass, "产物完整性",
                $"{fresh.Count} 份产物均存在且可读取。");
        }

        return new ValidationResult("R-FILE-01", ValidationLevel.ActionRequired, "产物完整性",
            $"以下产物缺失或为空：{string.Join("、", missing)}。", "Artifacts", "重新生成材料。");
    }

    private ValidationResult CheckRulesVersion(ApplicationCase caseEntity)
    {
        if (string.IsNullOrWhiteSpace(caseEntity.RulesVersion))
        {
            return new ValidationResult("R-RULES-01", ValidationLevel.Warning, "规则版本记录",
                "案例尚未记录材料规则版本。", "RulesVersion", "重新扫描后会自动记录。");
        }

        if (!string.Equals(caseEntity.RulesVersion, _rules.Version, StringComparison.Ordinal))
        {
            return new ValidationResult("R-RULES-01", ValidationLevel.Warning, "规则版本记录",
                $"案例使用的规则版本为 {caseEntity.RulesVersion}，当前配置版本为 {_rules.Version}。",
                "RulesVersion",
                "如需按最新规则生成材料，请重新扫描并生成。");
        }

        return new ValidationResult("R-RULES-01", ValidationLevel.Pass, "规则版本记录",
            $"规则版本 {caseEntity.RulesVersion} 已记录在案。");
    }

    private static ValidationResult CheckPendingItems(ApplicationCase caseEntity, ProjectAnalysis? analysis)
    {
        var pending = new List<string>();

        if (analysis is not null)
        {
            try
            {
                var questions = System.Text.Json.JsonSerializer.Deserialize<List<string>>(
                    analysis.OpenQuestionsJson, Mapping.JsonOptions) ?? [];
                pending.AddRange(questions.Where(q => !string.IsNullOrWhiteSpace(q)));
            }
            catch (System.Text.Json.JsonException)
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

    private static ValidationResult CheckSecrets(IReadOnlyList<ProjectFile> files)
    {
        var suspicious = files.Where(f => f.SecretRisk == SecretRisk.Suspected).Select(f => f.RelativePath).ToList();
        if (suspicious.Count == 0)
        {
            return new ValidationResult("R-SECRET-01", ValidationLevel.Pass, "凭据风险检查",
                "未发现疑似凭据文件。");
        }

        return new ValidationResult("R-SECRET-01", ValidationLevel.Warning, "凭据风险检查",
            $"发现 {suspicious.Count} 个疑似凭据文件（已排除，不进入材料与模型请求）：{string.Join("、", suspicious.Take(5))}。",
            "ProjectFiles",
            "建议申请人尽快处理仓库中的泄露风险，并轮换相关密钥。");
    }

    private static ValidationResult CheckCoverage(ProjectAnalysis? analysis)
    {
        if (analysis is null)
        {
            return new ValidationResult("R-COVERAGE-01", ValidationLevel.Warning, "分析覆盖度",
                "尚未生成分析结果。", "Coverage", "请先执行扫描。");
        }

        CoverageSummary? coverage = null;
        try
        {
            coverage = System.Text.Json.JsonSerializer.Deserialize<CoverageSummary>(analysis.CoverageJson, Mapping.JsonOptions);
        }
        catch (System.Text.Json.JsonException)
        {
            coverage = null;
        }

        if (coverage is null || coverage.TotalCount == 0)
        {
            return new ValidationResult("R-COVERAGE-01", ValidationLevel.Warning, "分析覆盖度",
                "分析覆盖摘要不可用。", "Coverage", null);
        }

        if (coverage.Ratio >= 0.6)
        {
            return new ValidationResult("R-COVERAGE-01", ValidationLevel.Pass, "分析覆盖度",
                $"分析覆盖 {coverage.CoveredCount}/{coverage.TotalCount} 项，识别到的信息较为完整。");
        }

        return new ValidationResult("R-COVERAGE-01", ValidationLevel.Warning, "分析覆盖度",
            $"分析覆盖 {coverage.CoveredCount}/{coverage.TotalCount} 项，部分信息未被仓库证据支持。",
            "Coverage",
            "请补充未识别项的说明，例如运行方式与功能概述。");
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex NumberPattern();
}
