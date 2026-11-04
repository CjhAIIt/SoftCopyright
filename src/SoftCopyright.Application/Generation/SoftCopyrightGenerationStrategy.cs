using Microsoft.Extensions.Logging;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Application.Generation;

/// <summary>
/// 软件著作权登记产物生成策略。
///
/// 负责从被确认的真实源码中逐行抽取代码鉴别材料，并产出申请表信息、操作手册、
/// 项目画像、证据清单与校验报告。
/// </summary>
public sealed class SoftCopyrightGenerationStrategy(
    IMaterialDocumentGenerator documentGenerator,
    IProjectFileReaderFactory readerFactory,
    IFileStore fileStore,
    ILogger<SoftCopyrightGenerationStrategy> logger) : IMaterialGenerationStrategy
{
    public MaterialKind Kind => MaterialKind.SoftwareCopyright;

    public async Task<IReadOnlyList<GeneratedDocument>> GenerateAsync(
        MaterialGenerationContext context,
        CancellationToken ct)
    {
        var info = context.ApplicantInfo;
        var blocks = await BuildCodeBlocksAsync(context.CaseId, context.CodeSelectionItems, ct);

        var request = new MaterialGenerationRequest(
            context.CaseId,
            info?.SoftwareFullName ?? context.Case.Name,
            info?.VersionNumber ?? string.Empty,
            context.Profile.Purpose.Value ?? info?.FunctionSummary ?? string.Empty,
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
            context.Profile.Features,
            context.Profile.DetectedTechnologies,
            context.Profile.DetectedEntrypoints,
            blocks,
            [],
            context.ValidationResults,
            context.RulesVersion,
            context.RulesSource,
            context.GeneratedAt);

        return await documentGenerator.GenerateAsync(request, ct);
    }

    public ArtifactKind MapKind(string relativePath) => relativePath switch
    {
        var p when p.EndsWith("申请表信息.txt", StringComparison.Ordinal) => ArtifactKind.ApplicationFormInfo,
        var p when p.EndsWith("操作手册.docx", StringComparison.Ordinal) => ArtifactKind.OperationManual,
        var p when p.EndsWith("代码鉴别材料.docx", StringComparison.Ordinal) => ArtifactKind.CodeMaterial,
        var p when p.EndsWith("项目画像.json", StringComparison.Ordinal) => ArtifactKind.ProjectProfile,
        var p when p.EndsWith("证据清单.json", StringComparison.Ordinal) => ArtifactKind.EvidenceManifest,
        var p when p.EndsWith("申请信息草稿.json", StringComparison.Ordinal) => ArtifactKind.ApplicantInfoDraft,
        var p when p.EndsWith("校验报告.html", StringComparison.Ordinal) => ArtifactKind.ValidationReport,
        _ => ArtifactKind.Manifest
    };

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
}
