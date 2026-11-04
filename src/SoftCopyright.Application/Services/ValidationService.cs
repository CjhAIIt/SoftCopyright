using Microsoft.Extensions.Options;
using SoftCopyright.Application.Validation;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Domain.Options;

namespace SoftCopyright.Application.Services;

/// <summary>
/// 校验中心：把自动化规则检查结果分成通过、警告与需申请人处理三级。
///
/// 软著与专利的专属检查由各自的 <see cref="IMaterialValidationRuleSet"/> 提供，
/// 这里只负责装载数据、挑选规则集并汇总通用检查（产物完整性、规则版本、凭据风险、覆盖度）。
/// </summary>
public sealed class ValidationService(
    IRepository<ApplicationCase> cases,
    IRepository<ApplicantInfo> applicantInfos,
    IRepository<CodeSelection> selections,
    IRepository<CodeSelectionItem> selectionItems,
    IRepository<ProjectFile> projectFiles,
    IRepository<GeneratedArtifact> artifacts,
    IRepository<ProjectAnalysis> analyses,
    IRepository<InventionDisclosure> disclosures,
    IRepository<PatentClaim> patentClaims,
    MaterialValidationRuleSets ruleSets,
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
        var disclosure = (await disclosures.ListAsync(d => d.CaseId == caseId, ct)).FirstOrDefault();
        var claims = await patentClaims.ListAsync(c => c.CaseId == caseId, ct);

        var context = new MaterialValidationContext(
            caseEntity,
            info,
            items,
            files,
            caseArtifacts,
            analysis,
            disclosure,
            claims,
            includeArtifactCheck);

        results.AddRange(ruleSets.Get(caseEntity.MaterialKind).Evaluate(context));

        results.Add(CheckArtifacts(caseArtifacts, includeArtifactCheck));
        results.Add(CheckRulesVersion(caseEntity));
        results.Add(CheckSecrets(files));
        results.Add(CheckCoverage(analysis));

        return results;
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
        if (analysis == null)
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

        if (coverage == null || coverage.TotalCount == 0)
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
}
