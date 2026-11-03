namespace SoftCopyright.Web.Models;

/// <summary>申请信息表单的可绑定模型。</summary>
public sealed class ApplicantForm
{
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

    public int ExpectedRowVersion { get; set; }
}
