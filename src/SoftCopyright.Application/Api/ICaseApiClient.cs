using SoftCopyright.Application.Dtos;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Application.Api;

/// <summary>UI 访问后端 API 的客户端契约。</summary>
public interface ICaseApiClient
{
    Task<IReadOnlyList<CaseSummaryDto>> GetCasesAsync(CancellationToken ct);

    Task<CreateCaseResponse> CreateCaseAsync(string? name, CancellationToken ct);

    Task<CreateCaseResponse> CreateCaseAsync(string? name, MaterialKind materialKind, CancellationToken ct);

    Task<CaseDetailDto> GetCaseAsync(Guid id, CancellationToken ct);

    Task<DeleteCaseResponse> DeleteCaseAsync(Guid id, CancellationToken ct);

    Task<UploadResultDto> UploadAsync(Guid id, string fileName, Stream content, CancellationToken ct);

    Task<GitImportResultDto> ImportFromGitAsync(Guid id, GitImportRequest request, CancellationToken ct);

    Task<JobDto> TriggerScanAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<JobDto>> GetJobsAsync(Guid id, CancellationToken ct);

    Task<AnalysisDto> GetAnalysisAsync(Guid id, CancellationToken ct);

    Task<ApplicantInfoDto?> GetApplicantInfoAsync(Guid id, CancellationToken ct);

    Task<ApplicantInfoDto> SaveApplicantInfoAsync(Guid id, UpdateApplicantInfoRequest request, CancellationToken ct);

    Task<IReadOnlyList<CodeCandidateDto>> GetCodeCandidatesAsync(Guid id, CancellationToken ct);

    Task<string?> PreviewCodeAsync(Guid id, string path, int maxLines, CancellationToken ct);

    Task UpdateCodeSelectionAsync(Guid id, UpdateCodeSelectionRequest request, CancellationToken ct);

    Task<IReadOnlyList<ConfirmationDto>> GetConfirmationsAsync(Guid id, CancellationToken ct);

    Task<ConfirmationDto> ConfirmAsync(Guid id, string stage, string? note, CancellationToken ct);

    Task<JobDto> TriggerGenerateAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<ArtifactDto>> GetArtifactsAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<ValidationDto>> GetValidationAsync(Guid id, CancellationToken ct);

    Task<PatentDisclosureDto> GetPatentDisclosureAsync(Guid id, CancellationToken ct);

    Task<PatentDisclosureDto> SavePatentDisclosureAsync(Guid id, PatentDisclosureUpdateRequest request, CancellationToken ct);

    Task<JobDto> TriggerPatentDraftAsync(Guid id, CancellationToken ct);
}
