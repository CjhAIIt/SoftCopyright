using Microsoft.Extensions.Logging;
using SoftCopyright.Application.Dtos;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Application.Services;

/// <summary>申请案例用例服务。</summary>
public sealed class CaseService(
    IRepository<ApplicationCase> cases,
    IRepository<GeneratedArtifact> artifacts,
    IRepository<ConfirmationRecord> confirmations,
    IRepository<ProjectFile> files,
    IRepository<CaseDeletionAudit> audits,
    ICurrentAccountContext currentAccount,
    IFileStore fileStore,
    ILogger<CaseService> logger) : ICaseService
{
    /// <summary>当前账号；未登录时为 null。</summary>
    private Guid? CurrentUserId => currentAccount.UserId;

    /// <summary>
    /// 列出当前账号可见的案例。
    /// 未登录时返回空列表——材料按账号隔离，匿名不得看到任何案例。
    /// </summary>
    public async Task<IReadOnlyList<ApplicationCase>> ListAsync(CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null)
        {
            return [];
        }

        return await cases.ListAsync(c => !c.IsDeleted && c.UserId == userId.Value, ct);
    }

    /// <summary>按材料类型列出当前账号的案例：软著与专利各自独立的工作区。</summary>
    public async Task<IReadOnlyList<ApplicationCase>> ListAsync(MaterialKind materialKind, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null)
        {
            return [];
        }

        return await cases.ListAsync(
            c => !c.IsDeleted && c.UserId == userId.Value && c.MaterialKind == materialKind,
            ct);
    }

    public async Task<ApplicationCase?> GetAsync(Guid id, CancellationToken ct)
    {
        var entity = await cases.FindAsync(id, ct);
        if (entity is null || entity.IsDeleted)
        {
            return null;
        }

        // 案例按账号隔离：他人案例一律视为不可见。
        var userId = CurrentUserId;
        return userId is null || entity.UserId == userId.Value ? entity : null;
    }

    public async Task<ApplicationCase> CreateAsync(string? name, CancellationToken ct) =>
        await CreateAsync(name, MaterialKind.SoftwareCopyright, ct);

    public async Task<ApplicationCase> CreateAsync(string? name, MaterialKind materialKind, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var entity = new ApplicationCase
        {
            Id = id,
            Name = string.IsNullOrWhiteSpace(name) ? "未命名项目" : name.Trim(),
            MaterialKind = materialKind,
            UserId = currentAccount.UserId ?? Guid.Empty,
            Stage = CaseStage.Created,
            StageUpdatedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
            StatusMessage = materialKind == MaterialKind.Patent
                ? "案例已创建，等待导入代码仓库并填写技术交底书。"
                : "案例已创建，等待导入代码仓库压缩包。",
            DataDirectory = fileStore.GetCaseDirectory(id)
        };

        await cases.AddAsync(entity, ct);
        await cases.SaveChangesAsync(ct);

        logger.LogInformation("已创建申请案例 {CaseId}（{Name}）", id, entity.Name);
        return entity;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var entity = await cases.FindAsync(id, ct);
        if (entity is null)
        {
            return false;
        }

        var artifactCount = (await artifacts.ListAsync(a => a.CaseId == id, ct)).Count;
        var confirmationCount = (await confirmations.ListAsync(c => c.CaseId == id, ct)).Count;
        var fileCount = (await files.ListAsync(f => f.CaseId == id, ct)).Count;

        await audits.AddAsync(new CaseDeletionAudit
        {
            Id = Guid.NewGuid(),
            CaseId = id,
            CaseName = entity.Name,
            DeletedAt = DateTimeOffset.UtcNow,
            FileCount = fileCount,
            ArtifactCount = artifactCount,
            ConfirmationCount = confirmationCount
        }, ct);

        cases.Remove(entity);
        await cases.SaveChangesAsync(ct);

        fileStore.DeleteCaseDirectory(id);

        logger.LogInformation("已删除申请案例 {CaseId}", id);
        return true;
    }

    /// <summary>构建案例列表项。</summary>
    public async Task<CaseSummaryDto> ToSummaryAsync(ApplicationCase entity, CancellationToken ct)
    {
        var artifactCount = (await artifacts.ListAsync(a => a.CaseId == entity.Id, ct)).Count;
        var confirmed = await confirmations.ListAsync(c => c.CaseId == entity.Id, ct);

        return new CaseSummaryDto(
            entity.Id,
            entity.Name,
            entity.SoftwareFullName,
            entity.Stage,
            Mapping.StageText(entity.Stage),
            entity.StatusMessage,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.FailureReason,
            artifactCount,
            confirmed.Count(c => c.InvalidatedAt is null),
            5,
            entity.SourceRepositoryUrl,
            entity.SourceCommit,
            entity.MaterialKind);
    }
}
