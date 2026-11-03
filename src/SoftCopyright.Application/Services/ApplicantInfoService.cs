using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoftCopyright.Application.Contracts;
using SoftCopyright.Application.Dtos;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Domain.Options;

namespace SoftCopyright.Application.Services;

/// <summary>申请信息用例服务：法律与权属字段永不预填。</summary>
public sealed class ApplicantInfoService(
    IRepository<ApplicantInfo> applicantInfos,
    IRepository<ApplicationCase> cases,
    IRepository<ConfirmationRecord> confirmations,
    IRepository<GeneratedArtifact> artifacts,
    IOptions<MaterialRules> rulesOptions,
    ILogger<ApplicantInfoService> logger) : IApplicantInfoService
{
    private readonly MaterialRules _rules = rulesOptions.Value;

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public async Task<ApplicantInfo?> GetAsync(Guid caseId, CancellationToken ct)
    {
        var list = await applicantInfos.ListAsync(a => a.CaseId == caseId, ct);
        return list.FirstOrDefault();
    }

    public async Task<ApplicantInfo> SaveAsync(Guid caseId, UpdateApplicantInfoRequest request, CancellationToken ct)
    {
        var existing = await GetAsync(caseId, ct);

        if (existing is not null && request.ExpectedRowVersion != existing.RowVersion)
        {
            throw new ConflictException("申请信息已被其他操作更新，请刷新页面后重试。");
        }

        Validate(request);

        var now = DateTimeOffset.UtcNow;
        var entity = existing ?? new ApplicantInfo
        {
            Id = Guid.NewGuid(),
            CaseId = caseId
        };

        entity.SoftwareFullName = request.SoftwareFullName.Trim();
        entity.SoftwareAbbreviation = Trim(request.SoftwareAbbreviation);
        entity.VersionNumber = request.VersionNumber.Trim();
        entity.CopyrightOwner = request.CopyrightOwner.Trim();
        entity.DevelopmentMode = request.DevelopmentMode.Trim();
        entity.RightsAcquisitionMethod = request.RightsAcquisitionMethod.Trim();
        entity.CompletionDate = Trim(request.CompletionDate);
        entity.FirstPublicationStatus = request.FirstPublicationStatus.Trim();
        entity.FirstPublicationDate = Trim(request.FirstPublicationDate);
        entity.DevelopmentEnvironment = request.DevelopmentEnvironment.Trim();
        entity.RuntimeEnvironment = request.RuntimeEnvironment.Trim();
        entity.ProgrammingLanguages = request.ProgrammingLanguages.Trim();
        entity.SourceProgramAmount = request.SourceProgramAmount.Trim();
        entity.FunctionSummary = request.FunctionSummary.Trim();
        entity.RulesVersion = _rules.Version;
        entity.UpdatedAt = now;
        entity.RowVersion = existing?.RowVersion + 1 ?? 1;
        entity.FieldSourcesJson = BuildFieldSources(entity);

        if (existing is null)
        {
            await applicantInfos.AddAsync(entity, ct);
        }

        await applicantInfos.SaveChangesAsync(ct);

        // 案例列表展示的软件名与版本同步自申请人确认信息。
        var caseEntity = await cases.FindAsync(caseId, ct);
        if (caseEntity is not null)
        {
            caseEntity.SoftwareFullName = entity.SoftwareFullName;
            caseEntity.SoftwareVersion = entity.VersionNumber;
            caseEntity.UpdatedAt = now;
            await cases.SaveChangesAsync(ct);
        }

        // 上游数据变更后，下游确认与已生成材料立即失效。
        await InvalidateDownstreamAsync(caseId, "申请信息已修改，需要重新确认代码范围与材料草稿。", ct);

        logger.LogInformation("已保存申请信息（案例 {CaseId}，版本 {RowVersion}）", caseId, entity.RowVersion);
        return entity;
    }

    /// <summary>把申请信息映射为 DTO，并附带字段来源标注。</summary>
    public ApplicantInfoDto ToDto(ApplicantInfo entity, bool confirmed)
    {
        var sources = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
            entity.FieldSourcesJson,
            Mapping.JsonOptions) ?? new Dictionary<string, string>();

        return new ApplicantInfoDto(
            entity.SoftwareFullName,
            entity.SoftwareAbbreviation,
            entity.VersionNumber,
            entity.CopyrightOwner,
            entity.DevelopmentMode,
            entity.RightsAcquisitionMethod,
            entity.CompletionDate,
            entity.FirstPublicationStatus,
            entity.FirstPublicationDate,
            entity.DevelopmentEnvironment,
            entity.RuntimeEnvironment,
            entity.ProgrammingLanguages,
            entity.SourceProgramAmount,
            entity.FunctionSummary,
            sources,
            entity.RulesVersion,
            entity.RowVersion,
            confirmed);
    }

    private void Validate(UpdateApplicantInfoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SoftwareFullName))
        {
            throw new InputValidationException("软件全称不能为空。");
        }

        if (request.SoftwareFullName.Length > _rules.Fields.MaxFieldLength)
        {
            throw new InputValidationException($"软件全称长度不能超过 {_rules.Fields.MaxFieldLength} 个字符。");
        }

        if (request.FunctionSummary.Length > _rules.Fields.MaxSummaryLength)
        {
            throw new InputValidationException($"功能简介长度不能超过 {_rules.Fields.MaxSummaryLength} 个字符。");
        }
    }

    private static string BuildFieldSources(ApplicantInfo entity)
    {
        var sources = new Dictionary<string, string>();

        void Mark(string field, string value)
        {
            sources[field] = string.IsNullOrWhiteSpace(value) ? "待填写" : "申请人输入";
        }

        Mark(nameof(ApplicantInfo.SoftwareFullName), entity.SoftwareFullName);
        Mark(nameof(ApplicantInfo.SoftwareAbbreviation), entity.SoftwareAbbreviation ?? string.Empty);
        Mark(nameof(ApplicantInfo.VersionNumber), entity.VersionNumber);
        Mark(nameof(ApplicantInfo.CopyrightOwner), entity.CopyrightOwner);
        Mark(nameof(ApplicantInfo.DevelopmentMode), entity.DevelopmentMode);
        Mark(nameof(ApplicantInfo.RightsAcquisitionMethod), entity.RightsAcquisitionMethod);
        Mark(nameof(ApplicantInfo.CompletionDate), entity.CompletionDate ?? string.Empty);
        Mark(nameof(ApplicantInfo.FirstPublicationStatus), entity.FirstPublicationStatus);
        Mark(nameof(ApplicantInfo.FirstPublicationDate), entity.FirstPublicationDate ?? string.Empty);
        Mark(nameof(ApplicantInfo.DevelopmentEnvironment), entity.DevelopmentEnvironment);
        Mark(nameof(ApplicantInfo.RuntimeEnvironment), entity.RuntimeEnvironment);
        Mark(nameof(ApplicantInfo.ProgrammingLanguages), entity.ProgrammingLanguages);
        Mark(nameof(ApplicantInfo.SourceProgramAmount), entity.SourceProgramAmount);
        Mark(nameof(ApplicantInfo.FunctionSummary), entity.FunctionSummary);

        return System.Text.Json.JsonSerializer.Serialize(sources, Mapping.JsonOptions);
    }

    private async Task InvalidateDownstreamAsync(Guid caseId, string reason, CancellationToken ct)
    {
        var records = await confirmations.ListAsync(c => c.CaseId == caseId, ct);
        var now = DateTimeOffset.UtcNow;

        foreach (var record in records)
        {
            if (record.Stage is ConfirmationStage.ApplicantInfo or ConfirmationStage.CodeScope
                or ConfirmationStage.DocumentDraft or ConfirmationStage.FinalPackage)
            {
                record.InvalidatedAt ??= now;
                record.InvalidatedReason ??= reason;
            }
        }

        var caseArtifacts = await artifacts.ListAsync(a => a.CaseId == caseId, ct);
        foreach (var artifact in caseArtifacts)
        {
            artifact.IsStale = true;
        }

        await confirmations.SaveChangesAsync(ct);
        await artifacts.SaveChangesAsync(ct);
    }
}
