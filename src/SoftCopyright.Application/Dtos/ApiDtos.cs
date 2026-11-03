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
    int TotalConfirmations);

/// <summary>创建案例请求。</summary>
public sealed record CreateCaseRequest(string? Name);

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
    int ExpectedRowVersion);

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
