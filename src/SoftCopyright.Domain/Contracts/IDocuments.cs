namespace SoftCopyright.Domain.Contracts;

/// <summary>材料文档生成器。</summary>
public interface IMaterialDocumentGenerator
{
    /// <summary>生成全部材料产物。</summary>
    Task<IReadOnlyList<GeneratedDocument>> GenerateAsync(MaterialGenerationRequest request, CancellationToken ct);
}

/// <summary>材料生成请求。</summary>
/// <remarks>
/// 申请信息整体以 <see cref="ApplicantInfo" /> 实体携带，避免把二十多个申请字段摊平成一长串参数；
/// 其中软件全称与版本号仍单独给出，因为生成时可能用案例名称兜底。
/// </remarks>
public sealed record MaterialGenerationRequest(
    Guid CaseId,
    string SoftwareFullName,
    string SoftwareVersion,
    ApplicantInfo? Applicant,
    string Purpose,
    string FunctionSummary,
    IReadOnlyList<Fact<string>> Features,
    IReadOnlyList<string> Technologies,
    IReadOnlyList<string> Entrypoints,
    IReadOnlyList<CodeLineBlock> CodeBlocks,
    IReadOnlyList<EvidenceEntry> Evidences,
    IReadOnlyList<ValidationResult> ValidationResults,
    string RulesVersion,
    string RulesSource,
    DateTimeOffset GeneratedAt);

/// <summary>生成的文档。</summary>
public sealed record GeneratedDocument(
    string FileName,
    string RelativePath,
    string ContentType,
    byte[] Content);

/// <summary>材料包打包器。</summary>
public interface IMaterialPackageBuilder
{
    /// <summary>把已生成的产物打包为 ZIP。</summary>
    Task<byte[]> BuildAsync(IReadOnlyList<GeneratedDocument> documents, CancellationToken ct);
}

/// <summary>专利申请文件生成请求。</summary>
public sealed record PatentDocumentRequest(
    string Title,
    string Version,
    InventionDisclosure Disclosure,
    IReadOnlyList<PatentClaim> Claims,
    IReadOnlyList<ValidationResult> ValidationResults,
    string RulesVersion,
    string RulesSource,
    DateTimeOffset GeneratedAt);

/// <summary>专利申请文件生成器：权利要求书、说明书、摘要与技术交底书。</summary>
public interface IPatentDocumentGenerator
{
    Task<IReadOnlyList<GeneratedDocument>> GenerateAsync(PatentDocumentRequest request, CancellationToken ct);
}
