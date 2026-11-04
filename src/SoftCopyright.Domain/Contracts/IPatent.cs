using SoftCopyright.Domain;

namespace SoftCopyright.Domain.Contracts;

/// <summary>专利草稿生成器：把证据包与交底书转成摘要、权利要求与说明书要素。</summary>
public interface IPatentDraftGenerator
{
    string Id { get; }

    Task<PatentDraftResult> DraftAsync(
        EvidencePack pack,
        InventionDisclosure disclosure,
        CancellationToken ct);
}

/// <summary>专利交底书更新请求。</summary>
public sealed record PatentDisclosureUpdateRequest(
    string Title,
    string TechnicalField,
    string BackgroundTechnology,
    string ProblemStatement,
    string TechnicalSolution,
    string BeneficialEffects,
    string DrawingDescription,
    string PriorArt,
    string PriorArtSources,
    string ApplicantName,
    string InventorNames,
    PatentType PatentType);

/// <summary>专利用例服务。</summary>
public interface IPatentService
{
    Task<InventionDisclosure?> GetDisclosureAsync(Guid caseId, CancellationToken ct);

    Task<InventionDisclosure> SaveDisclosureAsync(Guid caseId, PatentDisclosureUpdateRequest request, CancellationToken ct);

    Task<IReadOnlyList<PatentClaim>> GetClaimsAsync(Guid caseId, CancellationToken ct);

    Task<PatentDraftResult> DraftAsync(Guid caseId, CancellationToken ct);
}
