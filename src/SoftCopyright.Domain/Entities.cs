namespace SoftCopyright.Domain;

/// <summary>申请案例。</summary>
public sealed class ApplicationCase
{
    public Guid Id { get; set; }

    /// <summary>案例显示名称，未填写软件名时显示“未命名项目”。</summary>
    public string Name { get; set; } = string.Empty;

    public string? SoftwareFullName { get; set; }

    public string? SoftwareVersion { get; set; }

    public CaseStage Stage { get; set; } = CaseStage.Created;

    public DateTimeOffset StageUpdatedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string StatusMessage { get; set; } = string.Empty;

    public string? FailureReason { get; set; }

    public string? UploadedFileName { get; set; }

    /// <summary>来源仓库地址（从 Git 导入时记录）。</summary>
    public string? SourceRepositoryUrl { get; set; }

    /// <summary>来源仓库提交号（从 Git 导入时记录）。</summary>
    public string? SourceCommit { get; set; }

    /// <summary>申请材料类型：软著登记或专利申请。</summary>
    public MaterialKind MaterialKind { get; set; } = MaterialKind.SoftwareCopyright;

    /// <summary>案例隔离目录（绝对路径，由服务端生成，客户端不可控）。</summary>
    public string DataDirectory { get; set; } = string.Empty;

    /// <summary>扫描摘要 JSON（文件统计、排除规则、风险提示）。</summary>
    public string? ScanSummaryJson { get; set; }

    /// <summary>项目画像 JSON。</summary>
    public string? ProfileJson { get; set; }

    public string RulesVersion { get; set; } = string.Empty;

    public string AnalyzerVersion { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public List<ProjectFile> ProjectFiles { get; set; } = [];

    public List<Evidence> Evidences { get; set; } = [];

    public List<ConfirmationRecord> Confirmations { get; set; } = [];

    public List<GeneratedArtifact> Artifacts { get; set; } = [];

    public List<BackgroundJob> Jobs { get; set; } = [];
}

/// <summary>扫描得到的仓库文件。</summary>
public sealed class ProjectFile
{
    public Guid Id { get; set; }

    public Guid CaseId { get; set; }

    public string RelativePath { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public bool IsText { get; set; }

    public string DetectedEncoding { get; set; } = "unknown";

    public string? DetectedLanguage { get; set; }

    public FileCategory Category { get; set; }

    public bool IsExcluded { get; set; }

    public string? ExclusionReason { get; set; }

    public int? LineCount { get; set; }

    public SecretRisk SecretRisk { get; set; }

    public bool IsCodeCandidate { get; set; }

    public ApplicationCase? Case { get; set; }
}

/// <summary>证据条目。</summary>
public sealed class Evidence
{
    public Guid Id { get; set; }

    public Guid CaseId { get; set; }

    public string RelativePath { get; set; } = string.Empty;

    public int? StartLine { get; set; }

    public int? EndLine { get; set; }

    public string EvidenceType { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;

    /// <summary>证据摘要（截断），不保存源码全文。</summary>
    public string Excerpt { get; set; } = string.Empty;

    public EvidenceSource Source { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ApplicationCase? Case { get; set; }
}

/// <summary>一次项目分析结果。</summary>
public sealed class ProjectAnalysis
{
    public Guid Id { get; set; }

    public Guid CaseId { get; set; }

    public string ProfileJson { get; set; } = string.Empty;

    public string CoverageJson { get; set; } = string.Empty;

    public string OpenQuestionsJson { get; set; } = string.Empty;

    /// <summary>操作手册章节（由规则或模型生成，仅为章节标题与说明文字）。</summary>
    public string OperationSectionsJson { get; set; } = "[]";

    public string AdapterHitsJson { get; set; } = string.Empty;

    public string AnalyzerVersion { get; set; } = string.Empty;

    public string RulesVersion { get; set; } = string.Empty;

    public long DurationMs { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ApplicationCase? Case { get; set; }
}

/// <summary>申请人信息（仅存放申请人填写或确认的值）。</summary>
public sealed class ApplicantInfo
{
    public Guid Id { get; set; }

    public Guid CaseId { get; set; }

    public string SoftwareFullName { get; set; } = string.Empty;

    public string? SoftwareAbbreviation { get; set; }

    public string VersionNumber { get; set; } = string.Empty;

    public string CopyrightOwner { get; set; } = string.Empty;

    public string DevelopmentMode { get; set; } = string.Empty;

    public string RightsAcquisitionMethod { get; set; } = string.Empty;

    public string? CompletionDate { get; set; }

    public string FirstPublicationStatus { get; set; } = string.Empty;

    public string? FirstPublicationDate { get; set; }

    public string DevelopmentEnvironment { get; set; } = string.Empty;

    public string RuntimeEnvironment { get; set; } = string.Empty;

    public string ProgrammingLanguages { get; set; } = string.Empty;

    public string SourceProgramAmount { get; set; } = string.Empty;

    public string FunctionSummary { get; set; } = string.Empty;

    /// <summary>字段来源标注 JSON：字段名 → 来源与证据引用。</summary>
    public string FieldSourcesJson { get; set; } = "{}";

    public string RulesVersion { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>乐观并发版本号，每次保存递增。</summary>
    public int RowVersion { get; set; }

    public ApplicationCase? Case { get; set; }
}

/// <summary>代码范围选择（每个案例一份）。</summary>
public sealed class CodeSelection
{
    public Guid Id { get; set; }

    public Guid CaseId { get; set; }

    public bool IsConfirmed { get; set; }

    /// <summary>所选路径集合与各文件哈希的规范化哈希。</summary>
    public string ConfirmedHash { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    public ApplicationCase? Case { get; set; }

    public List<CodeSelectionItem> Items { get; set; } = [];
}

/// <summary>被勾选纳入代码材料的文件。</summary>
public sealed class CodeSelectionItem
{
    public Guid Id { get; set; }

    public Guid CaseId { get; set; }

    public Guid SelectionId { get; set; }

    public string RelativePath { get; set; } = string.Empty;

    public int LineCount { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public bool Included { get; set; }

    public CodeSelection? Selection { get; set; }
}

/// <summary>人工确认记录。</summary>
public sealed class ConfirmationRecord
{
    public Guid Id { get; set; }

    public Guid CaseId { get; set; }

    public ConfirmationStage Stage { get; set; }

    public string ConfirmedBy { get; set; } = string.Empty;

    public DateTimeOffset ConfirmedAt { get; set; }

    /// <summary>被确认数据的规范化哈希，用于判定确认是否失效。</summary>
    public string DataHash { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTimeOffset? InvalidatedAt { get; set; }

    public string? InvalidatedReason { get; set; }

    public ApplicationCase? Case { get; set; }
}

/// <summary>生成的产物索引。</summary>
public sealed class GeneratedArtifact
{
    public Guid Id { get; set; }

    public Guid CaseId { get; set; }

    public ArtifactKind Kind { get; set; }

    public string FileName { get; set; } = string.Empty;

    /// <summary>相对数据根目录的路径，由服务端生成，客户端不可提交。</summary>
    public string RelativePath { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>上游数据变更后置为 true，产物不可下载。</summary>
    public bool IsStale { get; set; }

    public ApplicationCase? Case { get; set; }
}

/// <summary>持久化后台任务。</summary>
public sealed class BackgroundJob
{
    public Guid Id { get; set; }

    public Guid CaseId { get; set; }

    public JobType Type { get; set; }

    public JobStatus Status { get; set; } = JobStatus.Pending;

    public int ProgressPercent { get; set; }

    public string Message { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public int AttemptCount { get; set; }

    public int MaxAttempts { get; set; } = 3;

    public string PayloadJson { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>租约到期时间，进程崩溃后任务自动回到 Pending。</summary>
    public DateTimeOffset? LeaseUntil { get; set; }

    /// <summary>下一次可执行时间（重试退避）。</summary>
    public DateTimeOffset? NextRunAt { get; set; }

    public ApplicationCase? Case { get; set; }
}

/// <summary>技术交底书（专利撰写的单一真源）。</summary>
public sealed class InventionDisclosure
{
    public Guid Id { get; set; }

    public Guid CaseId { get; set; }

    /// <summary>发明名称。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>技术领域。</summary>
    public string TechnicalField { get; set; } = string.Empty;

    /// <summary>背景技术（现有技术描述）。</summary>
    public string BackgroundTechnology { get; set; } = string.Empty;

    /// <summary>要解决的技术问题。</summary>
    public string ProblemStatement { get; set; } = string.Empty;

    /// <summary>技术方案总述。</summary>
    public string TechnicalSolution { get; set; } = string.Empty;

    /// <summary>有益效果。</summary>
    public string BeneficialEffects { get; set; } = string.Empty;

    /// <summary>附图说明。</summary>
    public string DrawingDescription { get; set; } = string.Empty;

    /// <summary>现有技术（须由申请人填写并注明来源，系统不代为检索）。</summary>
    public string PriorArt { get; set; } = string.Empty;

    /// <summary>现有技术来源说明。</summary>
    public string PriorArtSources { get; set; } = string.Empty;

    /// <summary>摘要（生成后回填，限 300 字）。</summary>
    public string Abstract { get; set; } = string.Empty;

    /// <summary>术语表 JSON。</summary>
    public string TerminologyJson { get; set; } = "[]";

    /// <summary>技术特征（要素）JSON，含证据引用。</summary>
    public string TechnicalFeaturesJson { get; set; } = "[]";

    /// <summary>实施例 JSON，含证据引用。</summary>
    public string EmbodimentsJson { get; set; } = "[]";

    /// <summary>申请人。</summary>
    public string ApplicantName { get; set; } = string.Empty;

    /// <summary>发明人。</summary>
    public string InventorNames { get; set; } = string.Empty;

    public PatentType PatentType { get; set; } = PatentType.Invention;

    public string RulesVersion { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    public ApplicationCase? Case { get; set; }
}

/// <summary>权利要求条目。</summary>
public sealed class PatentClaim
{
    public Guid Id { get; set; }

    public Guid CaseId { get; set; }

    /// <summary>权利要求编号（从 1 开始连续）。</summary>
    public int Number { get; set; }

    public ClaimType Type { get; set; }

    /// <summary>从属权利要求引用的母项编号。</summary>
    public int? DependsOn { get; set; }

    /// <summary>权利要求正文。</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>技术特征 JSON。</summary>
    public string FeaturesJson { get; set; } = "[]";

    public DateTimeOffset UpdatedAt { get; set; }

    public ApplicationCase? Case { get; set; }
}

/// <summary>案例删除审计摘要（不保留源码内容）。</summary>
public sealed class CaseDeletionAudit
{
    public Guid Id { get; set; }

    public Guid CaseId { get; set; }

    public string CaseName { get; set; } = string.Empty;

    public DateTimeOffset DeletedAt { get; set; }

    public int FileCount { get; set; }

    public int ArtifactCount { get; set; }

    public int ConfirmationCount { get; set; }
}
