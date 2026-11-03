using System.Text.Json;
using Microsoft.Extensions.Logging;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Application.Services;

/// <summary>智能分析编排：在证据包上运行分析器与草稿生成器，合并功能条目。</summary>
public sealed class AnalyzeWorkflow(
    IRepository<ApplicationCase> cases,
    IRepository<ProjectAnalysis> analyses,
    IRepository<ProjectFile> projectFiles,
    IRepository<Evidence> evidences,
    IRepository<ApplicantInfo> applicantInfos,
    IEvidencePackBuilder packBuilder,
    IProjectAnalyzer analyzer,
    IMaterialDraftGenerator draftGenerator,
    IFileStore fileStore,
    IProjectFileReaderFactory readerFactory,
    ILogger<AnalyzeWorkflow> logger) : ICaseAnalyzeWorkflow
{
    public async Task<ScanSummary> AnalyzeAsync(Guid caseId, Func<ScanProgress, Task>? progress, CancellationToken ct)
    {
        var caseEntity = await cases.FindAsync(caseId, ct);
        if (caseEntity is null || caseEntity.IsDeleted)
        {
            throw new NotFoundException("申请案例不存在。");
        }

        var analysis = (await analyses.ListAsync(a => a.CaseId == caseId, ct)).OrderByDescending(a => a.CreatedAt).FirstOrDefault();
        if (analysis is null)
        {
            throw new ConflictException("请先执行仓库扫描，再进行智能分析。");
        }

        await ReportAsync(progress, 20, "正在构建证据包……");

        var files = await projectFiles.ListAsync(f => f.CaseId == caseId, ct);
        var caseEvidences = await evidences.ListAsync(e => e.CaseId == caseId, ct);
        var reader = readerFactory.Create(fileStore.GetExtractedRoot(caseId));

        var fileEntries = files.Select(f => new ProjectFileEntry(
            f.RelativePath, f.SizeBytes, f.Sha256, f.IsText, f.DetectedEncoding, f.DetectedLanguage,
            f.Category, f.IsExcluded, f.ExclusionReason, f.LineCount, f.SecretRisk, f.IsCodeCandidate)).ToList();

        var evidenceEntries = caseEvidences.Select(e => new EvidenceEntry(
            e.RelativePath, e.StartLine, e.EndLine, e.EvidenceType, e.Sha256, e.Excerpt, e.Source)).ToList();

        var pack = await packBuilder.BuildAsync(fileEntries, evidenceEntries, reader, ct);

        await ReportAsync(progress, 50, "正在分析证据……");

        var baseline = ParseProfile(analysis.ProfileJson);
        var analysisResult = await analyzer.AnalyzeAsync(pack, baseline, ct);

        await ReportAsync(progress, 75, "正在生成材料草稿章节……");

        var applicant = (await applicantInfos.ListAsync(a => a.CaseId == caseId, ct)).FirstOrDefault();
        var snapshot = new ApplicantInfoSnapshot(
            applicant?.SoftwareFullName ?? caseEntity.SoftwareFullName ?? "待申请人填写",
            applicant?.VersionNumber ?? caseEntity.SoftwareVersion ?? string.Empty,
            applicant?.CopyrightOwner ?? string.Empty,
            applicant?.FunctionSummary ?? string.Empty,
            applicant?.ProgrammingLanguages ?? string.Empty,
            applicant?.DevelopmentEnvironment ?? string.Empty,
            applicant?.RuntimeEnvironment ?? string.Empty);

        var draft = await draftGenerator.DraftAsync(pack, snapshot, ct);

        var merged = MergeFeatures(baseline.Features, analysisResult.Profile.Features, draft.Features);

        var updatedProfile = analysisResult.Profile with { Features = merged };
        analysis.ProfileJson = JsonSerializer.Serialize(updatedProfile, Mapping.JsonOptions);
        analysis.OperationSectionsJson = JsonSerializer.Serialize(draft.OperationSections, Mapping.JsonOptions);
        analysis.OpenQuestionsJson = JsonSerializer.Serialize(
            analysisResult.Profile.OpenQuestions.Distinct(StringComparer.Ordinal).ToList(),
            Mapping.JsonOptions);

        await analyses.SaveChangesAsync(ct);

        caseEntity.ProfileJson = analysis.ProfileJson;
        caseEntity.UpdatedAt = DateTimeOffset.UtcNow;
        caseEntity.StatusMessage = $"智能分析完成，共整理 {merged.Count} 条功能描述。";
        await cases.SaveChangesAsync(ct);

        await ReportAsync(progress, 100, "智能分析完成。");

        logger.LogInformation("案例 {CaseId} 智能分析完成，证据包 {Items} 条，功能描述 {Features} 条",
            caseId, pack.Items.Count, merged.Count);

        var warnings = analysisResult.Warnings.Concat(draft.Warnings).Distinct(StringComparer.Ordinal).ToList();

        return new ScanSummary(
            files.Count,
            files.Count(f => !f.IsExcluded),
            files.Count(f => f.IsExcluded),
            files.Count(f => f.IsCodeCandidate),
            caseEvidences.Count,
            files.Count(f => f.SecretRisk == SecretRisk.Suspected),
            files.Where(f => f.DetectedLanguage is not null).Select(f => f.DetectedLanguage!).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            files.Where(f => f.IsExcluded && !string.IsNullOrWhiteSpace(f.ExclusionReason)).Select(f => f.ExclusionReason!).Distinct(StringComparer.Ordinal).ToList(),
            [],
            warnings);
    }

    private static ProjectProfile ParseProfile(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ProjectProfile>(json, Mapping.JsonOptions)
                   ?? throw new JsonException("画像为空");
        }
        catch (JsonException)
        {
            return new ProjectProfile(
                Fact<string>.Unknown("画像解析失败"),
                Fact<string>.Unknown("画像解析失败"),
                Fact<string>.Unknown("画像解析失败"),
                [],
                [],
                [],
                [],
                "1.0.0");
        }
    }

    private static List<Fact<string>> MergeFeatures(
        IReadOnlyList<Fact<string>> baseline,
        IReadOnlyList<Fact<string>> analyzed,
        IReadOnlyList<Fact<string>> drafted)
    {
        var merged = new List<Fact<string>>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var feature in baseline.Concat(analyzed).Concat(drafted))
        {
            if (string.IsNullOrWhiteSpace(feature.Value))
            {
                continue;
            }

            var key = feature.Value.Trim();
            if (!seen.Add(key))
            {
                continue;
            }

            merged.Add(feature);
        }

        return merged;
    }

    private static Task ReportAsync(Func<ScanProgress, Task>? progress, int percent, string message) =>
        progress is null ? Task.CompletedTask : progress(new ScanProgress(percent, message));
}
