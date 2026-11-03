using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Domain.Options;

namespace SoftCopyright.Application.Services;

/// <summary>扫描编排：调用扫描器、落库结果并推进阶段。</summary>
public sealed class ScanWorkflow(
    IRepository<ApplicationCase> cases,
    IRepository<ProjectFile> projectFiles,
    IRepository<Evidence> evidences,
    IRepository<ProjectAnalysis> analyses,
    IRepository<ConfirmationRecord> confirmations,
    IRepository<GeneratedArtifact> artifacts,
    IProjectScanService scanner,
    IFileStore fileStore,
    ICaseWorkflow workflow,
    IOptions<MaterialRules> rulesOptions,
    ILogger<ScanWorkflow> logger) : ICaseScanWorkflow
{
    private readonly MaterialRules _rules = rulesOptions.Value;

    public async Task<ScanSummary> ScanAsync(Guid caseId, Func<ScanProgress, Task>? progress, CancellationToken ct)
    {
        var caseEntity = await cases.FindAsync(caseId, ct);
        if (caseEntity is null || caseEntity.IsDeleted)
        {
            throw new NotFoundException("申请案例不存在。");
        }

        if (workflow.CanTransition(caseEntity.Stage, CaseStage.Scanning))
        {
            caseEntity.Stage = CaseStage.Scanning;
            caseEntity.StageUpdatedAt = DateTimeOffset.UtcNow;
            caseEntity.StatusMessage = "正在扫描仓库……";
            await cases.SaveChangesAsync(ct);
        }

        await ReportAsync(progress, 10, "正在建立文件索引……");

        var extractedRoot = fileStore.GetExtractedRoot(caseId);
        var started = DateTimeOffset.UtcNow;

        var result = await scanner.ScanAsync(new ProjectScanRequest(caseId, extractedRoot, []), ct);

        await ReportAsync(progress, 60, "正在整理证据与项目画像……");

        // 清理上一轮扫描结果，避免重复数据。
        var oldFiles = await projectFiles.ListAsync(f => f.CaseId == caseId, ct);
        foreach (var file in oldFiles)
        {
            projectFiles.Remove(file);
        }

        var oldEvidences = await evidences.ListAsync(e => e.CaseId == caseId, ct);
        foreach (var evidence in oldEvidences)
        {
            evidences.Remove(evidence);
        }

        foreach (var file in result.Files)
        {
            await projectFiles.AddAsync(new ProjectFile
            {
                Id = Guid.NewGuid(),
                CaseId = caseId,
                RelativePath = file.RelativePath,
                SizeBytes = file.SizeBytes,
                Sha256 = file.Sha256,
                IsText = file.IsText,
                DetectedEncoding = file.DetectedEncoding,
                DetectedLanguage = file.DetectedLanguage,
                Category = file.Category,
                IsExcluded = file.IsExcluded,
                ExclusionReason = file.ExclusionReason,
                LineCount = file.LineCount,
                SecretRisk = file.SecretRisk,
                IsCodeCandidate = file.IsCodeCandidate
            }, ct);
        }

        foreach (var evidence in result.Evidences)
        {
            await evidences.AddAsync(new Evidence
            {
                Id = Guid.NewGuid(),
                CaseId = caseId,
                RelativePath = evidence.RelativePath,
                StartLine = evidence.StartLine,
                EndLine = evidence.EndLine,
                EvidenceType = evidence.EvidenceType,
                Sha256 = evidence.Sha256,
                Excerpt = evidence.Excerpt,
                Source = evidence.Source,
                CreatedAt = DateTimeOffset.UtcNow
            }, ct);
        }

        await projectFiles.SaveChangesAsync(ct);
        await evidences.SaveChangesAsync(ct);

        await ReportAsync(progress, 80, "正在写入项目画像……");

        var profileJson = JsonSerializer.Serialize(result.Profile, Mapping.JsonOptions);
        var coverageJson = JsonSerializer.Serialize(result.Coverage, Mapping.JsonOptions);
        var questionsJson = JsonSerializer.Serialize(result.OpenQuestions, Mapping.JsonOptions);
        var adapterJson = JsonSerializer.Serialize(
            result.AdapterResults.Select(a => new { a.AdapterId, a.Summary, a.Technologies, a.Entrypoints }),
            Mapping.JsonOptions);

        var analysis = (await analyses.ListAsync(a => a.CaseId == caseId, ct)).FirstOrDefault();
        if (analysis is null)
        {
            analysis = new ProjectAnalysis
            {
                Id = Guid.NewGuid(),
                CaseId = caseId
            };
            await analyses.AddAsync(analysis, ct);
        }

        analysis.ProfileJson = profileJson;
        analysis.CoverageJson = coverageJson;
        analysis.OpenQuestionsJson = questionsJson;
        analysis.AdapterHitsJson = adapterJson;
        analysis.AnalyzerVersion = result.AnalyzerVersion;
        analysis.RulesVersion = _rules.Version;
        analysis.DurationMs = (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds;
        analysis.CreatedAt = DateTimeOffset.UtcNow;

        await analyses.SaveChangesAsync(ct);

        // 重新扫描使全部下游确认与已生成材料失效。
        await InvalidateDownstreamAsync(caseId, "仓库已重新扫描，需要重新确认下游内容。", ct);

        caseEntity.ProfileJson = profileJson;
        caseEntity.ScanSummaryJson = JsonSerializer.Serialize(BuildSummary(result), Mapping.JsonOptions);
        caseEntity.RulesVersion = _rules.Version;
        caseEntity.AnalyzerVersion = result.AnalyzerVersion;
        caseEntity.FailureReason = null;
        caseEntity.UpdatedAt = DateTimeOffset.UtcNow;

        if (workflow.CanTransition(caseEntity.Stage, CaseStage.AnalysisReady))
        {
            caseEntity.Stage = CaseStage.AnalysisReady;
            caseEntity.StageUpdatedAt = caseEntity.UpdatedAt;
        }

        caseEntity.StatusMessage = $"扫描完成：共 {result.Files.Count} 个文件，{result.Evidences.Count} 条证据，{result.OpenQuestions.Count} 个待确认事项。";
        await cases.SaveChangesAsync(ct);

        await ReportAsync(progress, 100, "扫描完成。");

        logger.LogInformation("案例 {CaseId} 扫描完成，文件 {Files} 个，证据 {Evidences} 条",
            caseId, result.Files.Count, result.Evidences.Count);

        return BuildSummary(result);
    }

    internal static ScanSummary BuildSummary(ProjectScanResult result)
    {
        var languages = result.Files
            .Where(f => f.DetectedLanguage is not null)
            .Select(f => f.DetectedLanguage!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var exclusionReasons = result.Files
            .Where(f => f.IsExcluded && !string.IsNullOrWhiteSpace(f.ExclusionReason))
            .Select(f => f.ExclusionReason!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var adapterHits = result.AdapterResults
            .Where(a => a.Matched)
            .Select(a => $"{a.AdapterId}：{a.Summary}")
            .ToList();

        return new ScanSummary(
            result.Files.Count,
            result.Files.Count(f => !f.IsExcluded),
            result.Files.Count(f => f.IsExcluded),
            result.Files.Count(f => f.IsCodeCandidate),
            result.Evidences.Count,
            result.Files.Count(f => f.SecretRisk == SecretRisk.Suspected),
            languages,
            exclusionReasons,
            adapterHits,
            result.Warnings);
    }

    private async Task InvalidateDownstreamAsync(Guid caseId, string reason, CancellationToken ct)
    {
        var records = await confirmations.ListAsync(c => c.CaseId == caseId, ct);
        var now = DateTimeOffset.UtcNow;

        foreach (var record in records)
        {
            record.InvalidatedAt ??= now;
            record.InvalidatedReason ??= reason;
        }

        var caseArtifacts = await artifacts.ListAsync(a => a.CaseId == caseId, ct);
        foreach (var artifact in caseArtifacts)
        {
            artifact.IsStale = true;
        }

        await confirmations.SaveChangesAsync(ct);
        await artifacts.SaveChangesAsync(ct);
    }

    private static Task ReportAsync(Func<ScanProgress, Task>? progress, int percent, string message) =>
        progress is null ? Task.CompletedTask : progress(new ScanProgress(percent, message));
}
