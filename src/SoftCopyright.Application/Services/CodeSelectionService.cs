using Microsoft.Extensions.Logging;
using SoftCopyright.Application.Dtos;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Application.Services;

/// <summary>代码范围选择服务：材料中的每一行代码都必须来自这里勾选的真实文件。</summary>
public sealed class CodeSelectionService(
    IRepository<ProjectFile> files,
    IRepository<CodeSelection> selections,
    IRepository<CodeSelectionItem> selectionItems,
    IRepository<ConfirmationRecord> confirmations,
    IRepository<GeneratedArtifact> artifacts,
    IFileStore fileStore,
    IProjectFileReaderFactory readerFactory,
    ILogger<CodeSelectionService> logger) : ICodeSelectionService
{
    public async Task<IReadOnlyList<ProjectFile>> ListCandidatesAsync(Guid caseId, CancellationToken ct) =>
        await files.ListAsync(f => f.CaseId == caseId && !f.IsExcluded, ct);

    public async Task<IReadOnlyList<string>> GetSelectedPathsAsync(Guid caseId, CancellationToken ct)
    {
        var selection = (await selections.ListAsync(s => s.CaseId == caseId, ct)).FirstOrDefault();
        if (selection is null)
        {
            return [];
        }

        var items = await selectionItems.ListAsync(i => i.SelectionId == selection.Id, ct);
        return items.Where(i => i.Included).Select(i => i.RelativePath)
            .OrderBy(p => p, StringComparer.Ordinal).ToList();
    }

    public async Task SaveSelectionAsync(Guid caseId, IReadOnlyList<string> selectedPaths, CancellationToken ct)
    {
        var candidates = await ListCandidatesAsync(caseId, ct);
        var candidatePaths = candidates.Select(c => c.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var normalized = selectedPaths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        foreach (var path in normalized)
        {
            if (!candidatePaths.Contains(path))
            {
                throw new InputValidationException($"文件 {path} 不在候选列表中，无法纳入材料。");
            }
        }

        var selection = (await selections.ListAsync(s => s.CaseId == caseId, ct)).FirstOrDefault();
        if (selection is null)
        {
            selection = new CodeSelection
            {
                Id = Guid.NewGuid(),
                CaseId = caseId
            };
            await selections.AddAsync(selection, ct);
        }

        var existing = await selectionItems.ListAsync(i => i.SelectionId == selection.Id, ct);
        foreach (var item in existing)
        {
            selectionItems.Remove(item);
        }

        foreach (var path in normalized)
        {
            var candidate = candidates.First(c => string.Equals(c.RelativePath, path, StringComparison.OrdinalIgnoreCase));
            await selectionItems.AddAsync(new CodeSelectionItem
            {
                Id = Guid.NewGuid(),
                CaseId = caseId,
                SelectionId = selection.Id,
                RelativePath = candidate.RelativePath,
                LineCount = candidate.LineCount ?? 0,
                Sha256 = candidate.Sha256,
                Included = true
            }, ct);
        }

        selection.ConfirmedHash = Hashing.Of(string.Join("|", normalized));
        selection.UpdatedAt = DateTimeOffset.UtcNow;
        selection.IsConfirmed = false;

        await selections.SaveChangesAsync(ct);
        await selectionItems.SaveChangesAsync(ct);

        await InvalidateDownstreamAsync(caseId, "代码范围已调整，需要重新确认草稿与最终材料。", ct);

        logger.LogInformation("已保存代码范围（案例 {CaseId}，选中 {Count} 个文件）", caseId, normalized.Count);
    }

    public async Task<string?> PreviewAsync(Guid caseId, string relativePath, int maxLines, CancellationToken ct)
    {
        var root = fileStore.GetExtractedRoot(caseId);
        var reader = readerFactory.Create(root);
        var text = await reader.ReadTextAsync(relativePath, 512 * 1024, ct);
        if (text is null)
        {
            return null;
        }

        var lines = text.Split('\n');
        var take = Math.Clamp(maxLines, 1, 500);
        return string.Join('\n', lines.Take(take));
    }

    /// <summary>把候选文件映射为 DTO。</summary>
    public static CodeCandidateDto ToDto(ProjectFile file, bool included) => new(
        file.RelativePath,
        file.LineCount,
        file.Sha256,
        Mapping.FieldCategoryText(file.Category),
        file.DetectedLanguage,
        file.ExclusionReason,
        file.IsCodeCandidate,
        included);

    private async Task InvalidateDownstreamAsync(Guid caseId, string reason, CancellationToken ct)
    {
        var records = await confirmations.ListAsync(c => c.CaseId == caseId, ct);
        var now = DateTimeOffset.UtcNow;

        foreach (var record in records)
        {
            if (record.Stage is ConfirmationStage.CodeScope or ConfirmationStage.DocumentDraft or ConfirmationStage.FinalPackage)
            {
                record.InvalidatedAt ??= now;
                record.InvalidatedReason ??= reason;
            }
        }

        var caseArtifacts = await artifacts.ListAsync(a => a.CaseId == caseId, ct);
        foreach (var artifact in caseArtifacts)
        {
            artifact.IsStale = true;
        }

        await confirmations.SaveChangesAsync(ct);
        await artifacts.SaveChangesAsync(ct);
    }
}
