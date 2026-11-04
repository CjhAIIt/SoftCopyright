using SoftCopyright.Domain;

namespace SoftCopyright.Application.Validation;

/// <summary>材料校验上下文：一次校验所需的全部已载入数据。</summary>
public sealed record MaterialValidationContext(
    ApplicationCase Case,
    ApplicantInfo? ApplicantInfo,
    IReadOnlyList<CodeSelectionItem> CodeSelectionItems,
    IReadOnlyList<ProjectFile> ProjectFiles,
    IReadOnlyList<GeneratedArtifact> Artifacts,
    ProjectAnalysis? Analysis,
    InventionDisclosure? Disclosure,
    IReadOnlyList<PatentClaim> Claims,
    bool IncludeArtifactCheck);
