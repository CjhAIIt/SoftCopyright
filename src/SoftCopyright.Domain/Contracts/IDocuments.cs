namespace SoftCopyright.Domain.Contracts;

/// <summary>材料文档生成器。</summary>
public interface IMaterialDocumentGenerator
{
    /// <summary>生成全部材料产物。</summary>
    Task<IReadOnlyList<GeneratedDocument>> GenerateAsync(MaterialGenerationRequest request, CancellationToken ct);
}

/// <summary>材料生成请求。</summary>
public sealed record MaterialGenerationRequest(
    Guid CaseId,
    string SoftwareFullName,
    string SoftwareVersion,
    string Purpose,
    string FunctionSummary,
    string CopyrightOwner,
    string DevelopmentMode,
    string RightsAcquisitionMethod,
    string? CompletionDate,
    string? FirstPublicationStatus,
    string? FirstPublicationDate,
    string DevelopmentEnvironment,
    string RuntimeEnvironment,
    string ProgrammingLanguages,
    string SourceProgramAmount,
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
