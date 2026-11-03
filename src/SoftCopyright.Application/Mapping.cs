using SoftCopyright.Application.Dtos;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Application;

/// <summary>DTO 映射与中文文案。</summary>
public static class Mapping
{
    public static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string StageText(CaseStage stage) => stage switch
    {
        CaseStage.Created => "已创建",
        CaseStage.Uploaded => "已上传",
        CaseStage.Scanning => "扫描中",
        CaseStage.AnalysisReady => "分析完成",
        CaseStage.ApplicantInfoPending => "待填写申请信息",
        CaseStage.CodeSelectionPending => "待确认代码范围",
        CaseStage.DraftReady => "草稿就绪",
        CaseStage.ReviewPending => "待审核材料",
        CaseStage.Validating => "校验中",
        CaseStage.PackageReady => "材料包已生成",
        CaseStage.Archived => "已归档",
        CaseStage.Failed => "执行失败",
        _ => stage.ToString()
    };

    public static string FactStatusText(FactStatus status) => status switch
    {
        FactStatus.Observed => "已观察（仓库证据）",
        FactStatus.Inferred => "推断（需确认）",
        FactStatus.ApplicantConfirmed => "申请人已确认",
        FactStatus.Unknown => "未知（待补充）",
        _ => status.ToString()
    };

    public static string JobStatusText(JobStatus status) => status switch
    {
        JobStatus.Pending => "等待中",
        JobStatus.Running => "执行中",
        JobStatus.Succeeded => "已完成",
        JobStatus.Failed => "失败",
        JobStatus.Canceled => "已取消",
        _ => status.ToString()
    };

    public static string JobTypeText(JobType type) => type switch
    {
        JobType.Scan => "仓库扫描",
        JobType.Analyze => "智能分析",
        JobType.Generate => "材料生成",
        _ => type.ToString()
    };

    public static string ArtifactKindText(ArtifactKind kind) => kind switch
    {
        ArtifactKind.ApplicationFormInfo => "申请表信息",
        ArtifactKind.OperationManual => "操作手册",
        ArtifactKind.CodeMaterial => "代码鉴别材料",
        ArtifactKind.ProjectProfile => "项目画像",
        ArtifactKind.EvidenceManifest => "证据清单",
        ArtifactKind.ApplicantInfoDraft => "申请信息草稿",
        ArtifactKind.ValidationReport => "校验报告",
        ArtifactKind.Manifest => "生成清单",
        ArtifactKind.Package => "材料包",
        _ => kind.ToString()
    };

    public static string ConfirmationStageText(ConfirmationStage stage) => stage switch
    {
        ConfirmationStage.ProjectProfile => "项目画像确认",
        ConfirmationStage.ApplicantInfo => "申请信息确认",
        ConfirmationStage.CodeScope => "代码范围确认",
        ConfirmationStage.DocumentDraft => "文档草稿确认",
        ConfirmationStage.FinalPackage => "最终材料确认",
        _ => stage.ToString()
    };

    public static string ValidationLevelText(ValidationLevel level) => level switch
    {
        ValidationLevel.Pass => "通过",
        ValidationLevel.Warning => "警告",
        ValidationLevel.ActionRequired => "需申请人处理",
        _ => level.ToString()
    };

    public static string FieldCategoryText(FileCategory category) => category switch
    {
        FileCategory.Code => "源码",
        FileCategory.Config => "配置",
        FileCategory.Doc => "文档",
        FileCategory.Binary => "二进制",
        _ => "其他"
    };

    public static FactDto ToDto(Fact<string> fact) => new(
        fact.Value,
        fact.Status,
        FactStatusText(fact.Status),
        fact.Explanation,
        fact.Evidence.Select(e => new EvidenceDto(e.RelativePath, e.StartLine, e.EndLine, e.EvidenceType, e.Sha256, string.Empty)).ToList());

    public static EvidenceDto ToDto(EvidenceEntry evidence) => new(
        evidence.RelativePath,
        evidence.StartLine,
        evidence.EndLine,
        evidence.EvidenceType,
        evidence.Sha256,
        evidence.Excerpt);

    public static JobDto ToDto(BackgroundJob job) => new(
        job.Id,
        job.Type,
        JobTypeText(job.Type),
        job.Status,
        JobStatusText(job.Status),
        job.ProgressPercent,
        job.Message,
        job.ErrorMessage,
        job.AttemptCount,
        job.MaxAttempts,
        job.CreatedAt,
        job.FinishedAt);

    public static ArtifactDto ToDto(GeneratedArtifact artifact) => new(
        artifact.Id,
        artifact.Kind.ToString(),
        ArtifactKindText(artifact.Kind),
        artifact.FileName,
        artifact.SizeBytes,
        artifact.Sha256,
        artifact.CreatedAt,
        artifact.IsStale,
        $"/api/artifacts/{artifact.Id}/download");

    public static ConfirmationDto ToDto(ConfirmationStatus status) => new(
        status.Stage,
        ConfirmationStageText(status.Stage),
        status.Confirmed,
        status.IsStale,
        status.ConfirmedAt,
        status.ConfirmedBy,
        status.Note,
        status.InvalidatedReason);

    public static ValidationDto ToDto(ValidationResult result) => new(
        result.RuleId,
        result.Level,
        ValidationLevelText(result.Level),
        result.Title,
        result.Message,
        result.FixHint);
}
