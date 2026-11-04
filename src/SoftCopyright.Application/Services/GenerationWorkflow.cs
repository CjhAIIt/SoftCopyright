using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoftCopyright.Application.Generation;
using SoftCopyright.Application.MaterialFlows;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Domain.Options;

namespace SoftCopyright.Application.Services;

/// <summary>
/// 材料生成编排：按材料类型挑选生成策略，保存产物并打包。
///
/// 软著与专利的产物清单、产物类型映射与材料包命名都各自独立，
/// 这里只负责加载数据、驱动策略、落盘与推进阶段。
/// </summary>
public sealed class GenerationWorkflow(
    IRepository<ApplicationCase> cases,
    IRepository<ApplicantInfo> applicantInfos,
    IRepository<CodeSelection> selections,
    IRepository<CodeSelectionItem> selectionItems,
    IRepository<ProjectAnalysis> analyses,
    IRepository<GeneratedArtifact> artifacts,
    IRepository<InventionDisclosure> disclosures,
    IRepository<PatentClaim> patentClaims,
    MaterialGenerationStrategies strategies,
    MaterialFlowRegistry flows,
    IValidationService validationService,
    IMaterialPackageBuilder packageBuilder,
    IFileStore fileStore,
    ICaseWorkflow workflow,
    IOptions<MaterialRules> rulesOptions,
    ILogger<GenerationWorkflow> logger) : ICaseGenerationWorkflow
{
    private readonly MaterialRules _rules = rulesOptions.Value;

    public async Task<GenerationSummary> GenerateAsync(Guid caseId, Func<ScanProgress, Task>? progress, CancellationToken ct)
    {
        var caseEntity = await cases.FindAsync(caseId, ct);
        if (caseEntity is null || caseEntity.IsDeleted)
        {
            throw new NotFoundException("申请案例不存在。");
        }

        var flow = flows.Get(caseEntity.MaterialKind);
        var strategy = strategies.Get(caseEntity.MaterialKind);

        var info = (await applicantInfos.ListAsync(a => a.CaseId == caseId, ct)).FirstOrDefault();
        var disclosure = (await disclosures.ListAsync(d => d.CaseId == caseId, ct)).FirstOrDefault();
        var claims = (await patentClaims.ListAsync(c => c.CaseId == caseId, ct)).OrderBy(c => c.Number).ToList();

        var selection = (await selections.ListAsync(s => s.CaseId == caseId, ct)).FirstOrDefault();
        var items = selection is null
            ? []
            : (await selectionItems.ListAsync(i => i.SelectionId == selection.Id, ct))
                .Where(i => i.Included)
                .OrderBy(i => i.RelativePath, StringComparer.Ordinal)
                .ToList();

        // 软著要提交代码鉴别材料，必须先确认代码范围；专利申请文件不含代码，不受此门禁约束。
        if (flow.RequiresCodeSelection)
        {
            if (selection is null)
            {
                throw new ConflictException("请先确认代码范围，再生成材料。");
            }

            if (items.Count == 0)
            {
                throw new ConflictException("尚未勾选任何源文件，无法生成代码鉴别材料。");
            }
        }
        else if (disclosure is null || string.IsNullOrWhiteSpace(disclosure.TechnicalSolution))
        {
            throw new ConflictException("请先填写并确认技术交底书，再生成专利申请文件。");
        }

        await ReportAsync(progress, 15, flow.RequiresCodeSelection ? "正在从真实源码抽取代码……" : "正在整理专利申请文件内容……");

        var validationResults = await validationService.ValidateAsync(caseId, ct, includeArtifactCheck: false);

        var analysis = (await analyses.ListAsync(a => a.CaseId == caseId, ct)).OrderByDescending(a => a.CreatedAt).FirstOrDefault();
        var profile = ParseProfile(analysis?.ProfileJson);
        var sections = ParseSections(analysis?.OperationSectionsJson);

        await ReportAsync(progress, 60, "正在生成正式材料……");

        var context = new MaterialGenerationContext(
            caseId,
            caseEntity,
            info,
            disclosure,
            claims,
            items,
            profile,
            sections,
            validationResults,
            _rules.Version,
            _rules.Source,
            DateTimeOffset.UtcNow);

        var documents = await strategy.GenerateAsync(context, ct);

        await ReportAsync(progress, 80, "正在保存材料产物……");

        var savedArtifacts = new List<GeneratedArtifact>();
        foreach (var document in documents)
        {
            var artifact = new GeneratedArtifact
            {
                Id = Guid.NewGuid(),
                CaseId = caseId,
                Kind = strategy.MapKind(document.RelativePath),
                FileName = document.FileName,
                SizeBytes = document.Content.Length,
                Sha256 = Hashing.OfBytes(document.Content),
                ContentType = document.ContentType,
                CreatedAt = DateTimeOffset.UtcNow,
                IsStale = false
            };

            artifact.RelativePath = await fileStore.SaveArtifactAsync(caseId, artifact.Id, document.RelativePath, document.Content, ct);
            await artifacts.AddAsync(artifact, ct);
            savedArtifacts.Add(artifact);
        }

        await ReportAsync(progress, 90, "正在打包材料……");

        var packageBytes = await packageBuilder.BuildAsync(documents, ct);
        var packageArtifact = new GeneratedArtifact
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            Kind = ArtifactKind.Package,
            FileName = flow.PackageFileName,
            SizeBytes = packageBytes.Length,
            Sha256 = Hashing.OfBytes(packageBytes),
            ContentType = "application/zip",
            CreatedAt = DateTimeOffset.UtcNow,
            IsStale = false
        };

        packageArtifact.RelativePath = await fileStore.SaveArtifactAsync(
            caseId,
            packageArtifact.Id,
            flow.PackageRelativePath,
            packageBytes,
            ct);

        await artifacts.AddAsync(packageArtifact, ct);
        await artifacts.SaveChangesAsync(ct);

        if (caseEntity.MaterialKind == MaterialKind.SoftwareCopyright)
        {
            caseEntity.SoftwareFullName = info?.SoftwareFullName;
            caseEntity.SoftwareVersion = info?.VersionNumber;
        }

        caseEntity.RulesVersion = _rules.Version;
        caseEntity.FailureReason = null;
        caseEntity.UpdatedAt = DateTimeOffset.UtcNow;

        if (workflow.CanTransition(caseEntity.Stage, CaseStage.Validating, caseEntity.MaterialKind))
        {
            caseEntity.Stage = CaseStage.Validating;
            caseEntity.StageUpdatedAt = caseEntity.UpdatedAt;
        }

        if (workflow.CanTransition(caseEntity.Stage, CaseStage.PackageReady, caseEntity.MaterialKind))
        {
            caseEntity.Stage = CaseStage.PackageReady;
            caseEntity.StageUpdatedAt = caseEntity.UpdatedAt;
        }

        caseEntity.StatusMessage = $"材料生成完成，共 {savedArtifacts.Count + 1} 份产物，等待申请人最终确认。";
        await cases.SaveChangesAsync(ct);

        await ReportAsync(progress, 100, "材料生成完成。");

        logger.LogInformation("案例 {CaseId} 已生成 {Count} 份材料产物（{Kind}）", caseId, savedArtifacts.Count + 1, flow.ShortName);

        return new GenerationSummary(
            savedArtifacts.Count + 1,
            packageBytes.Length,
            packageArtifact.Id,
            sections.Count == 0 ? [] : ["已生成操作手册章节 " + sections.Count + " 个"]);
    }

    private static Task ReportAsync(Func<ScanProgress, Task>? progress, int percent, string message) =>
        progress is null ? Task.CompletedTask : progress(new ScanProgress(percent, message));

    private static ProjectProfile ParseProfile(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return EmptyProfile();
        }

        try
        {
            return JsonSerializer.Deserialize<ProjectProfile>(json, Mapping.JsonOptions) ?? EmptyProfile();
        }
        catch (JsonException)
        {
            return EmptyProfile();
        }
    }

    private static ProjectProfile EmptyProfile() => new(
        Fact<string>.Unknown("尚未分析"),
        Fact<string>.Unknown("尚未分析"),
        Fact<string>.Unknown("尚未分析"),
        [],
        [],
        [],
        [],
        "1.0.0");

    private static List<string> ParseSections(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, Mapping.JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
