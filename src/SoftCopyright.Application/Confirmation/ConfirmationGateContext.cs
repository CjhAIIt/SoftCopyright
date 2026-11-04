using SoftCopyright.Domain;

namespace SoftCopyright.Application.Confirmation;

/// <summary>确认门禁上下文：判断某个确认阶段能否通过所需的全部数据。</summary>
/// <param name="IsConfirmationValid">
/// 查询某个确认阶段是否已经确认且未失效（上游数据变更后确认自动失效）。
/// </param>
public sealed record ConfirmationGateContext(
    Guid CaseId,
    ApplicationCase Case,
    ApplicantInfo? ApplicantInfo,
    CodeSelection? CodeSelection,
    IReadOnlyList<CodeSelectionItem> CodeSelectionItems,
    InventionDisclosure? Disclosure,
    IReadOnlyList<PatentClaim> Claims,
    IReadOnlyList<GeneratedArtifact> Artifacts,
    Func<ConfirmationStage, Task<bool>> IsConfirmationValid);
