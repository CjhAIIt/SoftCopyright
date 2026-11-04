using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Domain.Options;

namespace SoftCopyright.Application.Services;

/// <summary>材料生成编排：从真实源码抽取代码、生成正式材料并打包。</summary>
public sealed class GenerationWorkflow(
    IRepository<ApplicationCase> cases,
    IRepository<ApplicantInfo> applicantInfos,
    IRepository<CodeSelection> selections,
    IRepository<CodeSelectionItem> selectionItems,
    IRepository<ProjectAnalysis> analyses,
    IRepository<GeneratedArtifact> artifacts,
    IRepository<InventionDisclosure> disclosures,
    IRepository<PatentClaim> patentClaims,
    IValidationService validationService,
    IMaterialDocumentGenerator documentGenerator,
    IPatentDocumentGenerator patentDocumentGenerator,
    IMaterialPackageBuilder packageBuilder,
    IFileStore fileStore,
    IProjectFileReaderFactory readerFactory,
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

        var info = (await applicantInfos.ListAsync(a => a.CaseId == caseId, ct)).FirstOrDefault();
        var isPatent = caseEntity.MaterialKind == MaterialKind.Patent;

        if (isPatent)
        {
            var disclosure = (await disclosures.ListAsync(d => d.CaseId == caseId, ct)).FirstOrDefault();
            if (disclosure is null || string.IsNullOrWhiteSpace(disclosure.TechnicalSolution))
            {
                throw new ConflictException("请先填写并确认技术交底书，再生成专利申请文件。");
            }
        }
        else if (info is null || string.IsNullOrWhiteSpace(info.SoftwareFullName))
        {
            throw new ConflictException("请先填写并确认申请信息，再生成材料。");
        }

        var items = new List<CodeSelectionItem>();
        if (!isPatent)
        {
            // 软著材料包需要提交代码鉴别材料，必须先确认代码范围。
            var selection = (await selections.ListAsync(s => s.CaseId == caseId, ct)).FirstOrDefault();
            if (selection is null)
            {
                throw new ConflictException("请先确认代码范围，再生成材料。");
            }

            items = (await selectionItems.ListAsync(i => i.SelectionId == selection.Id, ct))
                .Where(i => i.Included)
                .OrderBy(i => i.RelativePath, StringComparer.Ordinal)
                .ToList();

            if (items.Count == 0)
            {
                throw new ConflictException("尚未勾选任何源文件，无法生成代码鉴别材料。");
            }
        }

        await ReportAsync(progress, 15, isPatent ? "正在整理专利申请文件内容……" : "正在从真实源码抽取代码……");

        var blocks = await BuildCodeBlocksAsync(caseId, items, ct);

        await ReportAsync(progress, 40, "正在执行一致性校验……");

        var validationResults = await validationService.ValidateAsync(caseId, ct, includeArtifactCheck: false);

        var analysis = (await analyses.ListAsync(a => a.CaseId == caseId, ct)).OrderByDescending(a => a.CreatedAt).FirstOrDefault();
        var profile = ParseProfile(analysis?.ProfileJson);
        var sections = ParseSections(analysis?.OperationSectionsJson);

        await ReportAsync(progress, 60, "正在生成正式材料……");

        var request = new MaterialGenerationRequest(
            caseId,
            info?.SoftwareFullName ?? caseEntity.Name,
            info?.VersionNumber ?? string.Empty,
            profile.Purpose.Value ?? info?.FunctionSummary ?? string.Empty,
            info?.FunctionSummary ?? string.Empty,
            info?.CopyrightOwner ?? string.Empty,
            info?.DevelopmentMode ?? string.Empty,
            info?.RightsAcquisitionMethod ?? string.Empty,
            info?.CompletionDate,
            info?.FirstPublicationStatus ?? string.Empty,
            info?.FirstPublicationDate,
            info?.DevelopmentEnvironment ?? string.Empty,
            info?.RuntimeEnvironment ?? string.Empty,
            info?.ProgrammingLanguages ?? string.Empty,
            info?.SourceProgramAmount ?? string.Empty,
            profile.Features,
            profile.DetectedTechnologies,
            profile.DetectedEntrypoints,
            blocks,
            [],
            validationResults,
            _rules.Version,
            _rules.Source,
            DateTimeOffset.UtcNow);

        var documents = isPatent
            ? await patentDocumentGenerator.GenerateAsync(
                new PatentDocumentRequest(
                    (await disclosures.ListAsync(d => d.CaseId == caseId, ct)).FirstOrDefault()?.Title ?? caseEntity.Name,
                    info?.VersionNumber ?? string.Empty,
                    (await disclosures.ListAsync(d => d.CaseId == caseId, ct)).FirstOrDefault()!,
                    (await patentClaims.ListAsync(c => c.CaseId == caseId, ct)).OrderBy(c => c.Number).ToList(),
                    validationResults,
                    _rules.Version,
                    _rules.Source,
                    DateTimeOffset.UtcNow),
                ct)
            : await documentGenerator.GenerateAsync(request, ct);

        await ReportAsync(progress, 80, "正在保存材料产物……");

        var savedArtifacts = new List<GeneratedArtifact>();
        foreach (var document in documents)
        {
            var artifact = new GeneratedArtifact
            {
                Id = Guid.NewGuid(),
                CaseId = caseId,
                Kind = MapKind(document.RelativePath),
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
            FileName = isPatent ? "专利申请材料包.zip" : "软著申请材料包.zip",
            SizeBytes = packageBytes.Length,
            Sha256 = Hashing.OfBytes(packageBytes),
            ContentType = "application/zip",
            CreatedAt = DateTimeOffset.UtcNow,
            IsStale = false
        };

        packageArtifact.RelativePath = await fileStore.SaveArtifactAsync(
            caseId,
            packageArtifact.Id,
            isPatent ? "材料包/专利申请材料包.zip" : "材料包/软著申请材料包.zip",
            packageBytes,
            ct);

        await artifacts.AddAsync(packageArtifact, ct);
        await artifacts.SaveChangesAsync(ct);

        if (!isPatent)
        {
            caseEntity.SoftwareFullName = info?.SoftwareFullName;
            caseEntity.SoftwareVersion = info?.VersionNumber;
        }
        caseEntity.RulesVersion = _rules.Version;
        caseEntity.FailureReason = null;
        caseEntity.UpdatedAt = DateTimeOffset.UtcNow;

        if (workflow.CanTransition(caseEntity.Stage, CaseStage.Validating))
        {
            caseEntity.Stage = CaseStage.Validating;
            caseEntity.StageUpdatedAt = caseEntity.UpdatedAt;
        }

        if (workflow.CanTransition(caseEntity.Stage, CaseStage.PackageReady))
        {
            caseEntity.Stage = CaseStage.PackageReady;
            caseEntity.StageUpdatedAt = caseEntity.UpdatedAt;
        }

        caseEntity.StatusMessage = $"材料生成完成，共 {savedArtifacts.Count + 1} 份产物，等待申请人最终确认。";
        await cases.SaveChangesAsync(ct);

        await ReportAsync(progress, 100, "材料生成完成。");

        logger.LogInformation("案例 {CaseId} 已生成 {Count} 份材料产物", caseId, savedArtifacts.Count + 1);

        return new GenerationSummary(
            savedArtifacts.Count + 1,
            packageBytes.Length,
            packageArtifact.Id,
            sections.Count == 0 ? [] : ["已生成操作手册章节 " + sections.Count + " 个"]);
    }

    private async Task<IReadOnlyList<CodeLineBlock>> BuildCodeBlocksAsync(
        Guid caseId,
        IReadOnlyList<CodeSelectionItem> items,
        CancellationToken ct)
    {
        var reader = readerFactory.Create(fileStore.GetExtractedRoot(caseId));
        var blocks = new List<CodeLineBlock>();

        foreach (var item in items)
        {
            var text = await reader.ReadTextAsync(item.RelativePath, 2 * 1024 * 1024, ct);
            if (text is null)
            {
                logger.LogWarning("代码文件 {Path} 已无法读取，已从材料中跳过", item.RelativePath);
                continue;
            }

            var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            // 去掉文件末尾的空行，保持与行数统计口径一致。
            if (lines.Length > 0 && lines[^1].Length == 0)
            {
                lines = lines[..^1];
            }

            blocks.Add(new CodeLineBlock(item.RelativePath, item.Sha256, 1, lines));
        }

        return blocks;
    }

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

    internal static ArtifactKind MapKind(string relativePath) => relativePath switch
    {
        var p when p.EndsWith("申请表信息.txt", StringComparison.Ordinal) => ArtifactKind.ApplicationFormInfo,
        var p when p.EndsWith("操作手册.docx", StringComparison.Ordinal) => ArtifactKind.OperationManual,
        var p when p.EndsWith("代码鉴别材料.docx", StringComparison.Ordinal) => ArtifactKind.CodeMaterial,
        var p when p.EndsWith("项目画像.json", StringComparison.Ordinal) => ArtifactKind.ProjectProfile,
        var p when p.EndsWith("证据清单.json", StringComparison.Ordinal) => ArtifactKind.EvidenceManifest,
        var p when p.EndsWith("申请信息草稿.json", StringComparison.Ordinal) => ArtifactKind.ApplicantInfoDraft,
        var p when p.EndsWith("校验报告.html", StringComparison.Ordinal) => ArtifactKind.ValidationReport,
        var p when p.EndsWith("权利要求书.docx", StringComparison.Ordinal) => ArtifactKind.PatentClaims,
        var p when p.EndsWith("说明书.docx", StringComparison.Ordinal) => ArtifactKind.PatentSpecification,
        var p when p.EndsWith("摘要.docx", StringComparison.Ordinal) => ArtifactKind.PatentAbstract,
        var p when p.EndsWith("技术交底书.docx", StringComparison.Ordinal) => ArtifactKind.PatentDisclosure,
        _ => ArtifactKind.Manifest
    };

    private static Task ReportAsync(Func<ScanProgress, Task>? progress, int percent, string message) =>
        progress is null ? Task.CompletedTask : progress(new ScanProgress(percent, message));
}
