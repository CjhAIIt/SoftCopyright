using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Application.Services;

/// <summary>申请案例阶段状态机。</summary>
public sealed class CaseWorkflow : ICaseWorkflow
{
    private static readonly CaseStage[] PipelineStages =
    [
        CaseStage.Created,
        CaseStage.Uploaded,
        CaseStage.Scanning,
        CaseStage.AnalysisReady,
        CaseStage.ApplicantInfoPending,
        CaseStage.CodeSelectionPending,
        CaseStage.DraftReady,
        CaseStage.ReviewPending,
        CaseStage.Validating,
        CaseStage.PackageReady,
        CaseStage.Archived
    ];

    private static readonly Dictionary<CaseStage, CaseStage[]> Allowed = new()
    {
        [CaseStage.Created] = [CaseStage.Uploaded, CaseStage.Failed],
        [CaseStage.Uploaded] = [CaseStage.Scanning, CaseStage.Failed],
        [CaseStage.Scanning] = [CaseStage.AnalysisReady, CaseStage.Failed],
        [CaseStage.AnalysisReady] = [CaseStage.ApplicantInfoPending, CaseStage.Failed],
        [CaseStage.ApplicantInfoPending] = [CaseStage.CodeSelectionPending, CaseStage.Failed],
        [CaseStage.CodeSelectionPending] = [CaseStage.DraftReady, CaseStage.Failed],
        [CaseStage.DraftReady] = [CaseStage.ReviewPending, CaseStage.Failed],
        [CaseStage.ReviewPending] = [CaseStage.Validating, CaseStage.Failed],
        [CaseStage.Validating] = [CaseStage.PackageReady, CaseStage.Failed],
        [CaseStage.PackageReady] = [CaseStage.Archived, CaseStage.Failed],
        [CaseStage.Archived] = [],
        [CaseStage.Failed] =
        [
            CaseStage.Uploaded, CaseStage.Scanning, CaseStage.AnalysisReady, CaseStage.ApplicantInfoPending,
            CaseStage.CodeSelectionPending, CaseStage.DraftReady, CaseStage.ReviewPending,
            CaseStage.Validating, CaseStage.PackageReady
        ]
    };

    public IReadOnlyList<CaseStage> Pipeline { get; } = PipelineStages;

    public bool CanTransition(CaseStage from, CaseStage to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>
    /// 按材料类型判断迁移是否合法。
    /// 专利案例没有"代码范围"这一步：权利要求生成后直接进入材料审核，
    /// 因此额外允许「代码范围 → 材料审核」这条边（专利路径复用同一批阶段槽位）。
    /// </summary>
    public bool CanTransition(CaseStage from, CaseStage to, MaterialKind materialKind)
    {
        if (CanTransition(from, to))
        {
            return true;
        }

        return materialKind == MaterialKind.Patent
               && from == CaseStage.CodeSelectionPending
               && to == CaseStage.ReviewPending;
    }

    public IReadOnlyList<CaseStage> AllowedTransitions(CaseStage from) =>
        Allowed.TryGetValue(from, out var targets) ? targets : [];
}
