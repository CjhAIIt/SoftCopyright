using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoftCopyright.Application.Dtos;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Domain.Options;

namespace SoftCopyright.Application.Services;

/// <summary>人工确认门禁服务：确认内容哈希化，上游变更即令下游失效。</summary>
public sealed class ConfirmationService(
    IRepository<ConfirmationRecord> confirmations,
    IRepository<ApplicationCase> cases,
    IRepository<ProjectAnalysis> analyses,
    IRepository<ApplicantInfo> applicantInfos,
    IRepository<CodeSelection> selections,
    IRepository<CodeSelectionItem> selectionItems,
    IRepository<GeneratedArtifact> artifacts,
    ICaseWorkflow workflow,
    IOptions<MaterialRules> rulesOptions,
    ILogger<ConfirmationService> logger) : IConfirmationService
{
    private readonly MaterialRules _rules = rulesOptions.Value;

    private static readonly ConfirmationStage[] AllStages =
    [
        ConfirmationStage.ProjectProfile,
        ConfirmationStage.ApplicantInfo,
        ConfirmationStage.CodeScope,
        ConfirmationStage.DocumentDraft,
        ConfirmationStage.FinalPackage
    ];

    public async Task<IReadOnlyList<ConfirmationStatus>> ListAsync(Guid caseId, CancellationToken ct)
    {
        var records = await confirmations.ListAsync(c => c.CaseId == caseId, ct);
        var statuses = new List<ConfirmationStatus>();

        foreach (var stage in AllStages)
        {
            var record = records
                .Where(r => r.Stage == stage)
                .OrderByDescending(r => r.ConfirmedAt)
                .FirstOrDefault();

            if (record is null)
            {
                statuses.Add(new ConfirmationStatus(stage, false, false, null, null, null, null, null));
                continue;
            }

            var currentHash = await ComputeDataHashAsync(caseId, stage, ct);
            var isStale = !string.Equals(currentHash, record.DataHash, StringComparison.Ordinal);

            statuses.Add(new ConfirmationStatus(
                stage,
                true,
                isStale,
                record.ConfirmedAt,
                record.ConfirmedBy,
                record.DataHash,
                record.Note,
                isStale ? record.InvalidatedReason ?? "确认后相关数据已变更，需要重新确认。" : record.InvalidatedReason));
        }

        return statuses;
    }

    public async Task<ConfirmationStatus> ConfirmAsync(Guid caseId, ConfirmationStage stage, string note, CancellationToken ct)
    {
        var caseEntity = await cases.FindAsync(caseId, ct);
        if (caseEntity is null || caseEntity.IsDeleted)
        {
            throw new NotFoundException("申请案例不存在。");
        }

        await EnsurePreconditionsAsync(caseId, stage, caseEntity, ct);

        var hash = await ComputeDataHashAsync(caseId, stage, ct);
        var records = await confirmations.ListAsync(c => c.CaseId == caseId && c.Stage == stage, ct);
        var record = records.OrderByDescending(r => r.ConfirmedAt).FirstOrDefault();
        var now = DateTimeOffset.UtcNow;

        if (record is null)
        {
            record = new ConfirmationRecord
            {
                Id = Guid.NewGuid(),
                CaseId = caseId,
                Stage = stage
            };
            await confirmations.AddAsync(record, ct);
        }

        record.DataHash = hash;
        record.ConfirmedAt = now;
        record.ConfirmedBy = "本地用户";
        record.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        record.InvalidatedAt = null;
        record.InvalidatedReason = null;

        await confirmations.SaveChangesAsync(ct);

        await ApplyStageTransitionAsync(caseEntity, stage, ct);

        logger.LogInformation("已确认阶段 {Stage}（案例 {CaseId}）", stage, caseId);

        return new ConfirmationStatus(stage, true, false, record.ConfirmedAt, record.ConfirmedBy, hash, record.Note, null);
    }

    public async Task<string> ComputeDataHashAsync(Guid caseId, ConfirmationStage stage, CancellationToken ct)
    {
        var profileHash = await ComputeProfileHashAsync(caseId, ct);
        var applicantHash = await ComputeApplicantHashAsync(caseId, profileHash, ct);
        var codeHash = await ComputeCodeHashAsync(caseId, applicantHash, ct);

        switch (stage)
        {
            case ConfirmationStage.ProjectProfile:
                return profileHash;

            case ConfirmationStage.ApplicantInfo:
                return applicantHash;

            case ConfirmationStage.CodeScope:
                return codeHash;

            case ConfirmationStage.DocumentDraft:
                return Hashing.Of(profileHash, applicantHash, codeHash, _rules.Version);

            case ConfirmationStage.FinalPackage:
            {
                var caseArtifacts = await artifacts.ListAsync(a => a.CaseId == caseId, ct);
                var shas = caseArtifacts
                    .Where(a => a.Kind != ArtifactKind.Package)
                    .Select(a => a.Sha256)
                    .OrderBy(s => s, StringComparer.Ordinal);

                return Hashing.Of(profileHash, applicantHash, codeHash, string.Join(",", shas));
            }
            default:
                return Hashing.Of(profileHash, applicantHash, codeHash);
        }
    }

    private async Task<string> ComputeProfileHashAsync(Guid caseId, CancellationToken ct)
    {
        var analysisList = await analyses.ListAsync(a => a.CaseId == caseId, ct);
        var analysis = analysisList.OrderByDescending(a => a.CreatedAt).FirstOrDefault();
        if (analysis is null)
        {
            return Hashing.Of("无分析结果");
        }

        ProjectProfile? profile = null;
        try
        {
            profile = JsonSerializer.Deserialize<ProjectProfile>(analysis.ProfileJson, Mapping.JsonOptions);
        }
        catch (JsonException)
        {
            profile = null;
        }

        if (profile is null)
        {
            return Hashing.Of(analysis.ProfileJson);
        }

        var features = string.Join("|", profile.Features.Select(f => $"{f.Value}::{f.Status}"));
        var technologies = string.Join("|", profile.DetectedTechnologies);
        var entrypoints = string.Join("|", profile.DetectedEntrypoints);

        return Hashing.Of(
            profile.Name.Value,
            profile.Name.Status.ToString(),
            profile.Version.Value,
            profile.Version.Status.ToString(),
            profile.Purpose.Value,
            profile.Purpose.Status.ToString(),
            features,
            technologies,
            entrypoints);
    }

    private async Task<string> ComputeApplicantHashAsync(Guid caseId, string profileHash, CancellationToken ct)
    {
        var list = await applicantInfos.ListAsync(a => a.CaseId == caseId, ct);
        var info = list.FirstOrDefault();
        if (info is null)
        {
            return Hashing.Of(profileHash, "无申请信息");
        }

        return Hashing.Of(
            profileHash,
            info.SoftwareFullName,
            info.SoftwareAbbreviation,
            info.VersionNumber,
            info.CopyrightOwner,
            info.DevelopmentMode,
            info.RightsAcquisitionMethod,
            info.CompletionDate,
            info.FirstPublicationStatus,
            info.FirstPublicationDate,
            info.DevelopmentEnvironment,
            info.RuntimeEnvironment,
            info.ProgrammingLanguages,
            info.SourceProgramAmount,
            info.FunctionSummary);
    }

    private async Task<string> ComputeCodeHashAsync(Guid caseId, string applicantHash, CancellationToken ct)
    {
        var selectionList = await selections.ListAsync(s => s.CaseId == caseId, ct);
        var selection = selectionList.FirstOrDefault();
        if (selection is null)
        {
            return Hashing.Of(applicantHash, "无代码选择");
        }

        var items = await selectionItems.ListAsync(i => i.SelectionId == selection.Id, ct);
        var included = items
            .Where(i => i.Included)
            .OrderBy(i => i.RelativePath, StringComparer.Ordinal)
            .Select(i => $"{i.RelativePath}#{i.Sha256}");

        return Hashing.Of(applicantHash, selection.ConfirmedHash, string.Join("|", included));
    }

    private async Task EnsurePreconditionsAsync(Guid caseId, ConfirmationStage stage, ApplicationCase caseEntity, CancellationToken ct)
    {
        switch (stage)
        {
            case ConfirmationStage.ProjectProfile:
                return;

            case ConfirmationStage.ApplicantInfo:
            {
                var info = (await applicantInfos.ListAsync(a => a.CaseId == caseId, ct)).FirstOrDefault();
                if (info is null || string.IsNullOrWhiteSpace(info.SoftwareFullName))
                {
                    throw new ConflictException("请先填写申请信息，再进行确认。");
                }

                return;
            }

            case ConfirmationStage.CodeScope:
            {
                var selection = (await selections.ListAsync(s => s.CaseId == caseId, ct)).FirstOrDefault();
                if (selection is null)
                {
                    throw new ConflictException("请先选择纳入材料包的真实源文件。");
                }

                var items = await selectionItems.ListAsync(i => i.SelectionId == selection.Id, ct);
                if (!items.Any(i => i.Included))
                {
                    throw new ConflictException("至少需要勾选一个源文件才能确认代码范围。");
                }

                return;
            }

            case ConfirmationStage.DocumentDraft:
            {
                // 草稿确认依赖前序三道门禁已经通过且未失效。
                var records = await confirmations.ListAsync(c => c.CaseId == caseId, ct);
                foreach (var required in new[]
                         {
                             ConfirmationStage.ProjectProfile,
                             ConfirmationStage.ApplicantInfo,
                             ConfirmationStage.CodeScope
                         })
                {
                    var record = records
                        .Where(r => r.Stage == required)
                        .OrderByDescending(r => r.ConfirmedAt)
                        .FirstOrDefault();

                    if (record is null)
                    {
                        throw new ConflictException(
                            $"请先完成「{Mapping.ConfirmationStageText(required)}」，再确认文档草稿。");
                    }

                    var currentHash = await ComputeDataHashAsync(caseId, required, ct);
                    if (!string.Equals(currentHash, record.DataHash, StringComparison.Ordinal))
                    {
                        throw new ConflictException(
                            $"「{Mapping.ConfirmationStageText(required)}」的确认已失效，请重新确认后再确认文档草稿。");
                    }
                }

                return;
            }

            case ConfirmationStage.FinalPackage:
            {
                var finalArtifacts = await artifacts.ListAsync(a => a.CaseId == caseId, ct);
                if (!finalArtifacts.Any(a => a.Kind == ArtifactKind.Package && !a.IsStale))
                {
                    throw new ConflictException("请先生成材料包，再进行最终确认。");
                }

                return;
            }
        }
    }

    private async Task ApplyStageTransitionAsync(ApplicationCase caseEntity, ConfirmationStage stage, CancellationToken ct)
    {
        var target = stage switch
        {
            ConfirmationStage.ProjectProfile => CaseStage.ApplicantInfoPending,
            ConfirmationStage.ApplicantInfo => CaseStage.CodeSelectionPending,
            ConfirmationStage.CodeScope => CaseStage.DraftReady,
            ConfirmationStage.DocumentDraft => CaseStage.ReviewPending,
            ConfirmationStage.FinalPackage => CaseStage.Archived,
            _ => caseEntity.Stage
        };

        if (!workflow.CanTransition(caseEntity.Stage, target))
        {
            // 已经是目标阶段或处于可恢复的失败状态时，不强行迁移。
            if (caseEntity.Stage != target)
            {
                logger.LogInformation("案例 {CaseId} 当前阶段 {From} 无法直接迁移到 {To}，保持原阶段", caseEntity.Id, caseEntity.Stage, target);
                return;
            }
        }

        caseEntity.Stage = target;
        caseEntity.StageUpdatedAt = DateTimeOffset.UtcNow;
        caseEntity.UpdatedAt = caseEntity.StageUpdatedAt;
        caseEntity.StatusMessage = $"已完成「{Mapping.ConfirmationStageText(stage)}」，当前阶段：{Mapping.StageText(target)}。";
        caseEntity.FailureReason = null;

        await cases.SaveChangesAsync(ct);
    }
}
