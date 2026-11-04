using SoftCopyright.Domain;

namespace SoftCopyright.Application.Generation;

/// <summary>材料生成上下文：一次生成所需的全部已载入数据。</summary>
public sealed record MaterialGenerationContext(
    Guid CaseId,
    ApplicationCase Case,
    ApplicantInfo? ApplicantInfo,
    InventionDisclosure? Disclosure,
    IReadOnlyList<PatentClaim> Claims,
    IReadOnlyList<CodeSelectionItem> CodeSelectionItems,
    ProjectProfile Profile,
    IReadOnlyList<string> Sections,
    IReadOnlyList<ValidationResult> ValidationResults,
    string RulesVersion,
    string RulesSource,
    DateTimeOffset GeneratedAt);
