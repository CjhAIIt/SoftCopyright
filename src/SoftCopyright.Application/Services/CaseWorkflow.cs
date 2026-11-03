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

    public IReadOnlyList<CaseStage> AllowedTransitions(CaseStage from) =>
        Allowed.TryGetValue(from, out var targets) ? targets : [];
}
