using Microsoft.Extensions.Logging;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Application.Services;

/// <summary>专利草稿生成编排（后台任务）。</summary>
public sealed class PatentDraftWorkflow(
    IPatentService patentService,
    IRepository<ApplicationCase> cases,
    ICaseWorkflow workflow,
    ILogger<PatentDraftWorkflow> logger) : ICasePatentDraftWorkflow
{
    public async Task<PatentDraftSummary> DraftAsync(
        Guid caseId,
        Func<ScanProgress, Task>? progress,
        CancellationToken ct)
    {
        if (progress is not null)
        {
            await progress(new ScanProgress(20, "正在整理证据与交底书……"));
        }

        var result = await patentService.DraftAsync(caseId, ct);

        if (progress is not null)
        {
            await progress(new ScanProgress(100, "专利草稿已生成。"));
        }

        await AdvanceStageAsync(caseId, result.Claims.Count, ct);

        logger.LogInformation("案例 {CaseId} 专利草稿生成完成", caseId);

        return new PatentDraftSummary(
            result.Features.Count,
            result.Claims.Count,
            result.Embodiments.Count,
            result.Warnings);
    }

    /// <summary>
    /// 权利要求生成后推进到「权利要求」阶段槽位。
    /// 专利线复用软著的阶段槽位（这里对应 CodeSelectionPending），
    /// 因此需要从当前阶段沿合法路径逐级推进，不能直接跳阶段。
    /// </summary>
    private async Task AdvanceStageAsync(Guid caseId, int claimCount, CancellationToken ct)
    {
        var caseEntity = await cases.FindAsync(caseId, ct);
        if (caseEntity is null || caseEntity.IsDeleted)
        {
            return;
        }

        const CaseStage target = CaseStage.CodeSelectionPending;
        var guard = 0;

        while (caseEntity.Stage != target && guard++ < 6)
        {
            var next = workflow.AllowedTransitions(caseEntity.Stage)
                .Where(t => t != CaseStage.Failed && Order(t) > Order(caseEntity.Stage))
                .OrderBy(Order)
                .ToList();

            if (next.Count == 0 || !workflow.CanTransition(caseEntity.Stage, next[0], caseEntity.MaterialKind))
            {
                break;
            }

            caseEntity.Stage = next[0];
        }

        caseEntity.StageUpdatedAt = DateTimeOffset.UtcNow;
        caseEntity.UpdatedAt = caseEntity.StageUpdatedAt;
        caseEntity.StatusMessage = $"专利草稿已生成：{claimCount} 条权利要求已就绪，请核对后确认草稿。";
        await cases.SaveChangesAsync(ct);

        int Order(CaseStage stage)
        {
            var index = workflow.Pipeline.ToList().IndexOf(stage);
            return index < 0 ? int.MaxValue : index;
        }
    }
}
