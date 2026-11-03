namespace SoftCopyright.Domain.Options;

/// <summary>
/// 材料生成规则（版本化）。规则内容集中在此，禁止散落硬编码。
/// 注意：本规则集是本项目的工程配置，不是官方权威解释，材料中一律提示申请人按官方最新要求核对。
/// </summary>
public sealed class MaterialRules
{
    public const string SectionName = "MaterialRules";

    /// <summary>规则集版本，写入校验报告与生成清单。</summary>
    public string Version { get; set; } = "2026.11-v1";

    /// <summary>规则来源说明（用于报告脚注）。</summary>
    public string Source { get; set; } = "本工具内置工程配置，非官方权威解释，请按官方最新要求核对。";

    /// <summary>代码材料相关规则。</summary>
    public CodeMaterialRules CodeMaterial { get; set; } = new();

    /// <summary>字段规则。</summary>
    public FieldRules Fields { get; set; } = new();

    /// <summary>文档排版规则。</summary>
    public LayoutRules Layout { get; set; } = new();
}

/// <summary>代码鉴别材料规则。</summary>
public sealed class CodeMaterialRules
{
    /// <summary>每页代码行数。</summary>
    public int LinesPerPage { get; set; } = 50;

    /// <summary>目标页数（材料按实际内容生成，不足时如实报告实际页数）。</summary>
    public int TargetPages { get; set; } = 30;

    /// <summary>是否在文件边界插入说明行。</summary>
    public bool IncludeFileBoundaryMarkers { get; set; } = true;
}

/// <summary>申请字段规则。</summary>
public sealed class FieldRules
{
    /// <summary>必填字段。</summary>
    public string[] RequiredFields { get; set; } =
    [
        nameof(ApplicantInfo.SoftwareFullName),
        nameof(ApplicantInfo.VersionNumber),
        nameof(ApplicantInfo.CopyrightOwner),
        nameof(ApplicantInfo.DevelopmentMode),
        nameof(ApplicantInfo.RightsAcquisitionMethod),
        nameof(ApplicantInfo.DevelopmentEnvironment),
        nameof(ApplicantInfo.RuntimeEnvironment),
        nameof(ApplicantInfo.ProgrammingLanguages),
        nameof(ApplicantInfo.SourceProgramAmount),
        nameof(ApplicantInfo.FunctionSummary)
    ];

    /// <summary>法律与权属字段：系统永不预填。</summary>
    public string[] LegalOnlyFields { get; set; } =
    [
        nameof(ApplicantInfo.CopyrightOwner),
        nameof(ApplicantInfo.DevelopmentMode),
        nameof(ApplicantInfo.RightsAcquisitionMethod),
        nameof(ApplicantInfo.CompletionDate),
        nameof(ApplicantInfo.FirstPublicationStatus),
        nameof(ApplicantInfo.FirstPublicationDate)
    ];

    /// <summary>字段最大长度。</summary>
    public int MaxFieldLength { get; set; } = 2000;

    /// <summary>功能简介最大长度。</summary>
    public int MaxSummaryLength { get; set; } = 4000;
}

/// <summary>文档排版规则。</summary>
public sealed class LayoutRules
{
    /// <summary>正文字号（半磅）。</summary>
    public int BodyFontSizeHalfPoints { get; set; } = 24;

    /// <summary>行距（240 = 单倍行距）。</summary>
    public int LineSpacing { get; set; } = 312;

    /// <summary>是否在代码材料页脚显示页码。</summary>
    public bool ShowPageNumbers { get; set; } = true;
}
