using SoftCopyright.Domain;

namespace SoftCopyright.Application.Dtos;

/// <summary>申请案例列表项。</summary>
public sealed record CaseSummaryDto(
    Guid Id,
    string Name,
    string? SoftwareFullName,
    CaseStage Stage,
    string StageText,
    string StatusMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? FailureReason,
    int ArtifactCount,
    int ConfirmedCount,
    int TotalConfirmations,
    string? SourceRepositoryUrl = null,
    string? SourceCommit = null,
    MaterialKind MaterialKind = MaterialKind.SoftwareCopyright);

/// <summary>创建案例请求。</summary>
public sealed record CreateCaseRequest(string? Name, MaterialKind MaterialKind = MaterialKind.SoftwareCopyright);

/// <summary>账号对外信息。</summary>
public sealed record AccountDto(
    Guid Id,
    string UserName,
    string DisplayName,
    bool OnboardingCompleted,
    bool AiAssistantEnabled,
    bool HasAiConfiguration,
    bool IsAdmin,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

/// <summary>管理端账号条目。</summary>
public sealed record AdminAccountDto(
    Guid Id,
    string UserName,
    string DisplayName,
    bool IsAdmin,
    bool OnboardingCompleted,
    bool AiAssistantEnabled,
    string AiMode,
    int CaseCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

/// <summary>管理端数据总览。</summary>
public sealed record AdminOverviewDto(
    int TotalAccounts,
    int AdminAccounts,
    int OnlineAccounts,
    int TotalCases,
    int SoftCopyrightCases,
    int PatentCases,
    int TotalArtifacts,
    int PendingJobs,
    int FailedJobs,
    int SuspectedSecretFiles);

/// <summary>材料流程的一个工作页标签。</summary>
public sealed record MaterialTabDto(string Key, string Label, string Route);

/// <summary>
/// 材料流程描述：软著登记与专利申请两条流程各自的阶段文案、页面构成与产物清单。
/// 前端据此渲染入口页与工作台，不再在前端硬编码两条流程的差异。
/// </summary>
public sealed record MaterialFlowDto(
    string Kind,
    string DisplayName,
    string ShortName,
    string Summary,
    string Deliverables,
    IReadOnlyList<string> StageLabels,
    IReadOnlyList<MaterialTabDto> Tabs,
    bool RequiresCodeSelection,
    string WorkspacePath);

/// <summary>创建案例结果。</summary>
public sealed record CreateCaseResponse(Guid Id, string Name);

/// <summary>删除案例结果。</summary>
public sealed record DeleteCaseResponse(Guid Id, string Name, int RemovedFiles, int RemovedArtifacts, int RemovedConfirmations);

/// <summary>后台任务信息。</summary>
public sealed record JobDto(
    Guid Id,
    JobType Type,
    string TypeText,
    JobStatus Status,
    string StatusText,
    int ProgressPercent,
    string Message,
    string? ErrorMessage,
    int AttemptCount,
    int MaxAttempts,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FinishedAt);

/// <summary>证据引用 DTO。</summary>
public sealed record EvidenceDto(
    string RelativePath,
    int? StartLine,
    int? EndLine,
    string EvidenceType,
    string Sha256,
    string Excerpt);

/// <summary>事实 DTO。</summary>
public sealed record FactDto(
    string? Value,
    FactStatus Status,
    string StatusText,
    string? Explanation,
    IReadOnlyList<EvidenceDto> Evidence);

/// <summary>覆盖情况 DTO。</summary>
public sealed record CoverageDto(string Dimension, bool Covered, string Detail);

/// <summary>项目分析结果 DTO。</summary>
public sealed record AnalysisDto(
    string? Name,
    FactStatus NameStatus,
    string? Version,
    FactStatus VersionStatus,
    string? Purpose,
    FactStatus PurposeStatus,
    IReadOnlyList<FactDto> Features,
    IReadOnlyList<string> Technologies,
    IReadOnlyList<string> Entrypoints,
    IReadOnlyList<string> OpenQuestions,
    IReadOnlyList<CoverageDto> Coverage,
    IReadOnlyList<EvidenceDto> Evidences,
    IReadOnlyList<string> AdapterHits,
    string AnalyzerVersion,
    string RulesVersion,
    DateTimeOffset? CreatedAt);

/// <summary>上传摘要 DTO。</summary>
public sealed record UploadResultDto(
    string FileName,
    int EntryCount,
    long TotalBytes,
    int ExtractedFileCount,
    IReadOnlyList<string> SkippedEntries,
    IReadOnlyList<string> Warnings,
    string SecurityNote);

/// <summary>申请信息 DTO。</summary>
public sealed record ApplicantInfoDto(
    string SoftwareFullName,
    string? SoftwareAbbreviation,
    string VersionNumber,
    string CopyrightOwner,
    string DevelopmentMode,
    string RightsAcquisitionMethod,
    string? CompletionDate,
    string FirstPublicationStatus,
    string? FirstPublicationDate,
    string DevelopmentEnvironment,
    string RuntimeEnvironment,
    string ProgrammingLanguages,
    string SourceProgramAmount,
    string FunctionSummary,
    string OwnerType,
    string SoftwareSource,
    string? ModificationDescription,
    string? OriginalRegistrationNumber,
    string RightsScope,
    string? PartialRightsDescription,
    string? FirstPublicationPlace,
    string DepositMode,
    string ApplicationHandlingMethod,
    string? ClassificationCode,
    IReadOnlyDictionary<string, string> FieldSources,
    string RulesVersion,
    int RowVersion,
    bool IsConfirmed);

/// <summary>申请信息保存请求。</summary>
public sealed record UpdateApplicantInfoRequest(
    string SoftwareFullName,
    string? SoftwareAbbreviation,
    string VersionNumber,
    string CopyrightOwner,
    string DevelopmentMode,
    string RightsAcquisitionMethod,
    string? CompletionDate,
    string FirstPublicationStatus,
    string? FirstPublicationDate,
    string DevelopmentEnvironment,
    string RuntimeEnvironment,
    string ProgrammingLanguages,
    string SourceProgramAmount,
    string FunctionSummary,
    string OwnerType,
    string SoftwareSource,
    string? ModificationDescription,
    string? OriginalRegistrationNumber,
    string RightsScope,
    string? PartialRightsDescription,
    string? FirstPublicationPlace,
    string DepositMode,
    string ApplicationHandlingMethod,
    string? ClassificationCode,
    int ExpectedRowVersion);

/// <summary>申请表中一个受约束候选项。</summary>
public sealed record ApplicantOptionDto(string Value, string Label, string? Note);

/// <summary>申请表中一个选项型字段。</summary>
public sealed record ApplicantOptionFieldDto(
    string Field,
    string Label,
    string Description,
    bool Required,
    IReadOnlyList<ApplicantOptionDto> Options);

/// <summary>条件必填规则：某字段取特定选项时，另一字段必须填写。</summary>
public sealed record ApplicantConditionDto(
    string Field,
    string Value,
    string RequiredField,
    string Reason);

/// <summary>跨字段取值约束。</summary>
public sealed record ApplicantConstraintDto(
    string Field,
    string Value,
    string RelatedField,
    IReadOnlyList<string> AllowedValues,
    string Reason);

/// <summary>选项目录：前端据此渲染受约束的下拉项，不在前端硬编码候选项。</summary>
public sealed record ApplicantOptionCatalogDto(
    string Source,
    IReadOnlyList<ApplicantOptionFieldDto> Fields,
    IReadOnlyList<ApplicantConditionDto> Conditions,
    IReadOnlyList<ApplicantConstraintDto> Constraints);

/// <summary>代码候选文件 DTO。</summary>
public sealed record CodeCandidateDto(
    string RelativePath,
    int? LineCount,
    string Sha256,
    string Category,
    string? Language,
    string? ExclusionReason,
    bool IsCodeCandidate,
    bool Included);

/// <summary>代码选择保存请求。</summary>
public sealed record UpdateCodeSelectionRequest(IReadOnlyList<string> SelectedPaths);

/// <summary>确认状态 DTO。</summary>
public sealed record ConfirmationDto(
    ConfirmationStage Stage,
    string StageText,
    bool Confirmed,
    bool IsStale,
    DateTimeOffset? ConfirmedAt,
    string? ConfirmedBy,
    string? Note,
    string? InvalidatedReason);

/// <summary>确认请求。</summary>
public sealed record ConfirmStageRequest(string? Note);

/// <summary>校验结果 DTO。</summary>
public sealed record ValidationDto(
    string RuleId,
    ValidationLevel Level,
    string LevelText,
    string Title,
    string Message,
    string? FixHint);

/// <summary>产物 DTO。</summary>
public sealed record ArtifactDto(
    Guid Id,
    string Kind,
    string KindText,
    string FileName,
    long SizeBytes,
    string Sha256,
    DateTimeOffset CreatedAt,
    bool IsStale,
    string DownloadUrl);

/// <summary>Git 导入结果 DTO。</summary>
public sealed record GitImportResultDto(
    string Url,
    string? Commit,
    int FileCount,
    IReadOnlyList<string> Warnings);

/// <summary>技术特征 DTO。</summary>
public sealed record TechnicalFeatureDto(
    string Title,
    string Description,
    bool Confirmed,
    IReadOnlyList<EvidenceDto> Evidence);

/// <summary>实施例 DTO。</summary>
public sealed record EmbodimentDto(
    string Title,
    string Description,
    bool Confirmed,
    IReadOnlyList<EvidenceDto> Evidence);

/// <summary>权利要求 DTO。</summary>
public sealed record PatentClaimDto(
    int Number,
    ClaimType Type,
    int? DependsOn,
    string Text,
    IReadOnlyList<TechnicalFeatureDto> Features);

/// <summary>技术交底书 DTO。</summary>
public sealed record PatentDisclosureDto(
    string Title,
    string TechnicalField,
    string BackgroundTechnology,
    string ProblemStatement,
    string TechnicalSolution,
    string BeneficialEffects,
    string DrawingDescription,
    string PriorArt,
    string PriorArtSources,
    string Abstract,
    string ApplicantName,
    string InventorNames,
    PatentType PatentType,
    IReadOnlyList<TechnicalFeatureDto> Features,
    IReadOnlyList<EmbodimentDto> Embodiments,
    IReadOnlyList<string> Terminology,
    IReadOnlyList<PatentClaimDto> Claims);

/// <summary>专利草稿结果 DTO。</summary>
public sealed record PatentDraftDto(
    string Abstract,
    IReadOnlyList<TechnicalFeatureDto> Features,
    IReadOnlyList<EmbodimentDto> Embodiments,
    IReadOnlyList<PatentClaimDto> Claims,
    IReadOnlyList<string> Warnings);

/// <summary>扫描摘要 DTO。</summary>
public sealed record ScanSummaryDto(
    int TotalFiles,
    int IncludedFiles,
    int ExcludedFiles,
    int CodeCandidates,
    int EvidenceCount,
    int SuspectedSecretCount,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> ExclusionReasons,
    IReadOnlyList<string> AdapterHits,
    IReadOnlyList<string> Warnings);

/// <summary>申请案例详情 DTO。</summary>
public sealed record CaseDetailDto(
    CaseSummaryDto Summary,
    IReadOnlyList<JobDto> Jobs,
    IReadOnlyList<ConfirmationDto> Confirmations,
    ScanSummaryDto? ScanSummary,
    string RulesVersion,
    string AnalyzerVersion,
    IReadOnlyList<string> PipelineStages,
    IReadOnlyList<string> StageNames);
