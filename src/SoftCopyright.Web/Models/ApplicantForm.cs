namespace SoftCopyright.Web.Models;

/// <summary>申请信息表单的可绑定模型。</summary>
/// <remarks>
/// 选项型字段存放的是选项目录中的规范选项码（而不是显示文本），
/// 显示文本由前端从服务端下发的选项目录中解析，避免两处各写一份中文。
/// </remarks>
public sealed class ApplicantForm
{
    public string SoftwareFullName { get; set; } = string.Empty;

    public string? SoftwareAbbreviation { get; set; }

    public string VersionNumber { get; set; } = string.Empty;

    public string? ClassificationCode { get; set; }

    public string CopyrightOwner { get; set; } = string.Empty;

    /// <summary>著作权人类别。</summary>
    public string OwnerType { get; set; } = string.Empty;

    /// <summary>软件来源。</summary>
    public string SoftwareSource { get; set; } = string.Empty;

    /// <summary>修改说明（选择修改类来源时必填）。</summary>
    public string? ModificationDescription { get; set; }

    /// <summary>原登记号。</summary>
    public string? OriginalRegistrationNumber { get; set; }

    /// <summary>开发方式。</summary>
    public string DevelopmentMode { get; set; } = string.Empty;

    /// <summary>权利取得方式。</summary>
    public string RightsAcquisitionMethod { get; set; } = string.Empty;

    /// <summary>权利范围。</summary>
    public string RightsScope { get; set; } = string.Empty;

    /// <summary>部分权利权项说明（选择「部分权利」时必填）。</summary>
    public string? PartialRightsDescription { get; set; }

    public string? CompletionDate { get; set; }

    /// <summary>发表状态。</summary>
    public string FirstPublicationStatus { get; set; } = string.Empty;

    public string? FirstPublicationDate { get; set; }

    /// <summary>首次发表地点（选择「已发表」时必填）。</summary>
    public string? FirstPublicationPlace { get; set; }

    public string DevelopmentEnvironment { get; set; } = string.Empty;

    public string RuntimeEnvironment { get; set; } = string.Empty;

    public string ProgrammingLanguages { get; set; } = string.Empty;

    public string SourceProgramAmount { get; set; } = string.Empty;

    /// <summary>鉴别材料交存方式。</summary>
    public string DepositMode { get; set; } = string.Empty;

    /// <summary>申请办理方式。</summary>
    public string ApplicationHandlingMethod { get; set; } = string.Empty;

    public string FunctionSummary { get; set; } = string.Empty;

    public int ExpectedRowVersion { get; set; }
}
