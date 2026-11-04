using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoftCopyright.Application.Contracts;
using SoftCopyright.Application.Dtos;
using SoftCopyright.Application.Validation;
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
        entity.OwnerType = NormalizeOption(nameof(ApplicantInfo.OwnerType), request.OwnerType);
        entity.SoftwareSource = NormalizeOption(nameof(ApplicantInfo.SoftwareSource), request.SoftwareSource);
        entity.ModificationDescription = Trim(request.ModificationDescription);
        entity.OriginalRegistrationNumber = Trim(request.OriginalRegistrationNumber);
        entity.DevelopmentMode = NormalizeOption(nameof(ApplicantInfo.DevelopmentMode), request.DevelopmentMode);
        entity.RightsAcquisitionMethod = NormalizeOption(nameof(ApplicantInfo.RightsAcquisitionMethod), request.RightsAcquisitionMethod);
        entity.RightsScope = NormalizeOption(nameof(ApplicantInfo.RightsScope), request.RightsScope);
        entity.PartialRightsDescription = Trim(request.PartialRightsDescription);
        entity.CompletionDate = Trim(request.CompletionDate);
        entity.FirstPublicationStatus = NormalizeOption(nameof(ApplicantInfo.FirstPublicationStatus), request.FirstPublicationStatus);
        entity.FirstPublicationDate = Trim(request.FirstPublicationDate);
        entity.FirstPublicationPlace = Trim(request.FirstPublicationPlace);
        entity.DevelopmentEnvironment = request.DevelopmentEnvironment.Trim();
        entity.RuntimeEnvironment = request.RuntimeEnvironment.Trim();
        entity.ProgrammingLanguages = request.ProgrammingLanguages.Trim();
        entity.SourceProgramAmount = request.SourceProgramAmount.Trim();
        entity.DepositMode = NormalizeOption(nameof(ApplicantInfo.DepositMode), request.DepositMode);
        entity.ApplicationHandlingMethod = NormalizeOption(nameof(ApplicantInfo.ApplicationHandlingMethod), request.ApplicationHandlingMethod);
        entity.ClassificationCode = Trim(request.ClassificationCode);
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
            entity.OwnerType,
            entity.SoftwareSource,
            entity.ModificationDescription,
            entity.OriginalRegistrationNumber,
            entity.RightsScope,
            entity.PartialRightsDescription,
            entity.FirstPublicationPlace,
            entity.DepositMode,
            entity.ApplicationHandlingMethod,
            entity.ClassificationCode,
            sources,
            entity.RulesVersion,
            entity.RowVersion,
            confirmed);
    }

    /// <summary>
    /// 把选项字段的任意写法归一化为规范选项码；留空视为尚未选择，
    /// 出现不在候选项之内的取值时直接拒绝（数据完整性问题）。
    /// </summary>
    private static string NormalizeOption(string field, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var normalized = ApplicantOptionCatalog.Normalize(field, raw);
        if (normalized is null)
        {
            var label = ApplicantOptionCatalog.LabelOfField(field);
            var allowed = string.Join("、", ApplicantOptionCatalog.OptionsOf(field).Select(o => o.Label));
            throw new InputValidationException($"「{label}」的取值「{raw.Trim()}」不在允许的候选项之内。可选：{allowed}。");
        }

        return normalized;
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

        // 选项值域与分类号格式：非法取值直接拒绝，避免脏数据落库。
        var reader = new RequestFieldReader(request);
        foreach (var issue in ApplicantOptionChecker.CheckBlocking(reader.Read))
        {
            throw new InputValidationException(issue.Message);
        }
    }

    /// <summary>按字段名读取请求中的值，供选项检查器使用。</summary>
    private sealed class RequestFieldReader(UpdateApplicantInfoRequest request)
    {
        public string? Read(string field) => field switch
        {
            nameof(ApplicantInfo.SoftwareFullName) => request.SoftwareFullName,
            nameof(ApplicantInfo.SoftwareAbbreviation) => request.SoftwareAbbreviation,
            nameof(ApplicantInfo.VersionNumber) => request.VersionNumber,
            nameof(ApplicantInfo.CopyrightOwner) => request.CopyrightOwner,
            nameof(ApplicantInfo.OwnerType) => request.OwnerType,
            nameof(ApplicantInfo.SoftwareSource) => request.SoftwareSource,
            nameof(ApplicantInfo.ModificationDescription) => request.ModificationDescription,
            nameof(ApplicantInfo.OriginalRegistrationNumber) => request.OriginalRegistrationNumber,
            nameof(ApplicantInfo.DevelopmentMode) => request.DevelopmentMode,
            nameof(ApplicantInfo.RightsAcquisitionMethod) => request.RightsAcquisitionMethod,
            nameof(ApplicantInfo.RightsScope) => request.RightsScope,
            nameof(ApplicantInfo.PartialRightsDescription) => request.PartialRightsDescription,
            nameof(ApplicantInfo.CompletionDate) => request.CompletionDate,
            nameof(ApplicantInfo.FirstPublicationStatus) => request.FirstPublicationStatus,
            nameof(ApplicantInfo.FirstPublicationDate) => request.FirstPublicationDate,
            nameof(ApplicantInfo.FirstPublicationPlace) => request.FirstPublicationPlace,
            nameof(ApplicantInfo.DevelopmentEnvironment) => request.DevelopmentEnvironment,
            nameof(ApplicantInfo.RuntimeEnvironment) => request.RuntimeEnvironment,
            nameof(ApplicantInfo.ProgrammingLanguages) => request.ProgrammingLanguages,
            nameof(ApplicantInfo.SourceProgramAmount) => request.SourceProgramAmount,
            nameof(ApplicantInfo.DepositMode) => request.DepositMode,
            nameof(ApplicantInfo.ApplicationHandlingMethod) => request.ApplicationHandlingMethod,
            nameof(ApplicantInfo.ClassificationCode) => request.ClassificationCode,
            nameof(ApplicantInfo.FunctionSummary) => request.FunctionSummary,
            _ => null
        };
    }

    private static string BuildFieldSources(ApplicantInfo entity)
    {
        var sources = new Dictionary<string, string>();

        void Mark(string field, string value)
        {
            sources[field] = string.IsNullOrWhiteSpace(value) ? "待填写" : "申请人输入";
        }

        void MarkChoice(string field, string value)
        {
            sources[field] = string.IsNullOrWhiteSpace(value) ? "待选择" : "申请人选择";
        }

        Mark(nameof(ApplicantInfo.SoftwareFullName), entity.SoftwareFullName);
        Mark(nameof(ApplicantInfo.SoftwareAbbreviation), entity.SoftwareAbbreviation ?? string.Empty);
        Mark(nameof(ApplicantInfo.VersionNumber), entity.VersionNumber);
        Mark(nameof(ApplicantInfo.CopyrightOwner), entity.CopyrightOwner);
        MarkChoice(nameof(ApplicantInfo.OwnerType), entity.OwnerType);
        MarkChoice(nameof(ApplicantInfo.SoftwareSource), entity.SoftwareSource);
        Mark(nameof(ApplicantInfo.ModificationDescription), entity.ModificationDescription ?? string.Empty);
        Mark(nameof(ApplicantInfo.OriginalRegistrationNumber), entity.OriginalRegistrationNumber ?? string.Empty);
        MarkChoice(nameof(ApplicantInfo.DevelopmentMode), entity.DevelopmentMode);
        MarkChoice(nameof(ApplicantInfo.RightsAcquisitionMethod), entity.RightsAcquisitionMethod);
        MarkChoice(nameof(ApplicantInfo.RightsScope), entity.RightsScope);
        Mark(nameof(ApplicantInfo.PartialRightsDescription), entity.PartialRightsDescription ?? string.Empty);
        Mark(nameof(ApplicantInfo.CompletionDate), entity.CompletionDate ?? string.Empty);
        MarkChoice(nameof(ApplicantInfo.FirstPublicationStatus), entity.FirstPublicationStatus);
        Mark(nameof(ApplicantInfo.FirstPublicationDate), entity.FirstPublicationDate ?? string.Empty);
        Mark(nameof(ApplicantInfo.FirstPublicationPlace), entity.FirstPublicationPlace ?? string.Empty);
        Mark(nameof(ApplicantInfo.DevelopmentEnvironment), entity.DevelopmentEnvironment);
        Mark(nameof(ApplicantInfo.RuntimeEnvironment), entity.RuntimeEnvironment);
        Mark(nameof(ApplicantInfo.ProgrammingLanguages), entity.ProgrammingLanguages);
        Mark(nameof(ApplicantInfo.SourceProgramAmount), entity.SourceProgramAmount);
        MarkChoice(nameof(ApplicantInfo.DepositMode), entity.DepositMode);
        MarkChoice(nameof(ApplicantInfo.ApplicationHandlingMethod), entity.ApplicationHandlingMethod);
        Mark(nameof(ApplicantInfo.ClassificationCode), entity.ClassificationCode ?? string.Empty);
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
