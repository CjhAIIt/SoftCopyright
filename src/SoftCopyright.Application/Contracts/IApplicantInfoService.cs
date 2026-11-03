using SoftCopyright.Application.Dtos;
using SoftCopyright.Domain;

namespace SoftCopyright.Application.Contracts;

/// <summary>申请信息用例服务。</summary>
public interface IApplicantInfoService
{
    Task<ApplicantInfo?> GetAsync(Guid caseId, CancellationToken ct);

    Task<ApplicantInfo> SaveAsync(Guid caseId, UpdateApplicantInfoRequest request, CancellationToken ct);
}
