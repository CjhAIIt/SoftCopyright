namespace SoftCopyright.Domain;

/// <summary>申请案例阶段（对应策划书 §5.2 状态机）。</summary>
public enum CaseStage
{
    Created,
    Uploaded,
    Scanning,
    AnalysisReady,
    ApplicantInfoPending,
    CodeSelectionPending,
    DraftReady,
    ReviewPending,
    Validating,
    PackageReady,
    Archived,
    Failed
}

/// <summary>事实的可信度状态。申请人确认数据与模型推断数据分开保存。</summary>
public enum FactStatus
{
    /// <summary>在仓库证据中直接观察到。</summary>
    Observed,

    /// <summary>由模型或规则推断得出，需要申请人复核。</summary>
    Inferred,

    /// <summary>申请人已填写并确认。</summary>
    ApplicantConfirmed,

    /// <summary>证据不足，无法得出结论。</summary>
    Unknown
}

/// <summary>人工确认门禁的阶段。</summary>
public enum ConfirmationStage
{
    ProjectProfile,
    ApplicantInfo,
    CodeScope,
    DocumentDraft,
    FinalPackage
}

/// <summary>申请材料类型。</summary>
public enum MaterialKind
{
    /// <summary>软件著作权登记材料。</summary>
    SoftwareCopyright,

    /// <summary>专利申请文件（发明 / 实用新型）。</summary>
    Patent
}

/// <summary>专利类型。</summary>
public enum PatentType
{
    /// <summary>发明专利。</summary>
    Invention,

    /// <summary>实用新型。</summary>
    UtilityModel
}

/// <summary>权利要求类型。</summary>
public enum ClaimType
{
    /// <summary>独立权利要求。</summary>
    Independent,

    /// <summary>从属权利要求。</summary>
    Dependent
}

/// <summary>后台任务类型。</summary>
public enum JobType
{
    Scan,
    Analyze,
    Generate,
    PatentDraft
}

/// <summary>后台任务状态。</summary>
public enum JobStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Canceled
}

/// <summary>生成产物类型。</summary>
public enum ArtifactKind
{
    ApplicationFormInfo,
    OperationManual,
    CodeMaterial,
    ProjectProfile,
    EvidenceManifest,
    ApplicantInfoDraft,
    ValidationReport,
    Manifest,
    Package,
    PatentClaims,
    PatentSpecification,
    PatentAbstract,
    PatentDisclosure
}

/// <summary>校验结论级别。</summary>
public enum ValidationLevel
{
    Pass,
    Warning,
    ActionRequired
}

/// <summary>文件分类。</summary>
public enum FileCategory
{
    Code,
    Config,
    Doc,
    Binary,
    Unknown
}

/// <summary>疑似凭据风险等级。</summary>
public enum SecretRisk
{
    None,
    Suspected
}

/// <summary>申请字段的来源标注。</summary>
public enum FieldSource
{
    /// <summary>仓库证据。</summary>
    RepositoryEvidence,

    /// <summary>AI 推断。</summary>
    AiInferred,

    /// <summary>申请人输入（尚未确认）。</summary>
    ApplicantInput,

    /// <summary>申请人已确认。</summary>
    ApplicantConfirmed
}

/// <summary>证据来源。</summary>
public enum EvidenceSource
{
    Scanner,
    Adapter,
    Model,
    Applicant
}
