using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoftCopyright.Application.Confirmation;
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
    IRepository<InventionDisclosure> disclosures,
    IRepository<PatentClaim> patentClaims,
    IRepository<ApplicantInfo> applicantInfos,
    IRepository<CodeSelection> selections,
    IRepository<CodeSelectionItem> selectionItems,
    IRepository<GeneratedArtifact> artifacts,
    IAccountService accounts,
    ICurrentAccountContext currentAccount,
    ConfirmationGateRegistry gates,
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
        record.ConfirmedBy = await ResolveConfirmatorAsync(ct);
        record.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        record.InvalidatedAt = null;
        record.InvalidatedReason = null;

        await confirmations.SaveChangesAsync(ct);

        await ApplyStageTransitionAsync(caseEntity, stage, ct);

        logger.LogInformation("已确认阶段 {Stage}（案例 {CaseId}）", stage, caseId);

        return new ConfirmationStatus(stage, true, false, record.ConfirmedAt, record.ConfirmedBy, hash, record.Note, null);
    }

    /// <summary>确认人：取当前登录账号的显示名称，未登录时退化为账号标识。</summary>
    private async Task<string> ResolveConfirmatorAsync(CancellationToken ct)
    {
        var userId = currentAccount.UserId;
        if (userId is null)
        {
            return "未知账号";
        }

        var profile = await accounts.GetAsync(userId.Value, ct);
        return profile is null ? "未知账号" : profile.DisplayName;
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

    /// <summary>
    /// 校验确认阶段的前置条件。软著与专利的前置条件完全不同，
    /// 这里只负责装载数据并交给对应材料类型的门禁判断。
    /// </summary>
    private async Task EnsurePreconditionsAsync(Guid caseId, ConfirmationStage stage, ApplicationCase caseEntity, CancellationToken ct)
    {
        var info = (await applicantInfos.ListAsync(a => a.CaseId == caseId, ct)).FirstOrDefault();
        var selection = (await selections.ListAsync(s => s.CaseId == caseId, ct)).FirstOrDefault();
        var items = selection is null
            ? []
            : await selectionItems.ListAsync(i => i.SelectionId == selection.Id, ct);
        var disclosure = (await disclosures.ListAsync(d => d.CaseId == caseId, ct)).FirstOrDefault();
        var claims = await patentClaims.ListAsync(c => c.CaseId == caseId, ct);
        var caseArtifacts = await artifacts.ListAsync(a => a.CaseId == caseId, ct);

        var context = new ConfirmationGateContext(
            caseId,
            caseEntity,
            info,
            selection,
            items,
            disclosure,
            claims,
            caseArtifacts,
            required => IsConfirmationValidAsync(caseId, required, ct));

        await gates.Get(caseEntity.MaterialKind).EnsureAsync(stage, context, ct);
    }

    /// <summary>某个确认阶段是否已确认且未因上游变更而失效。</summary>
    private async Task<bool> IsConfirmationValidAsync(Guid caseId, ConfirmationStage stage, CancellationToken ct)
    {
        var records = await confirmations.ListAsync(c => c.CaseId == caseId, ct);
        var record = records
            .Where(r => r.Stage == stage)
            .OrderByDescending(r => r.ConfirmedAt)
            .FirstOrDefault();

        if (record is null)
        {
            return false;
        }

        var currentHash = await ComputeDataHashAsync(caseId, stage, ct);
        return string.Equals(currentHash, record.DataHash, StringComparison.Ordinal);
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

        if (!workflow.CanTransition(caseEntity.Stage, target, caseEntity.MaterialKind))
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
