namespace SoftCopyright.Domain.Contracts;

/// <summary>任务进度回报点。</summary>
public sealed record ScanProgress(int Percent, string Message);

/// <summary>导入与扫描编排。</summary>
public interface ICaseUploadService
{
    /// <summary>接收 ZIP 上传并安全解包，返回面向用户的中文摘要。</summary>
    Task<UploadSummary> UploadAsync(Guid caseId, string fileName, Stream content, CancellationToken ct);
}

/// <summary>扫描编排。</summary>
public interface ICaseScanWorkflow
{
    Task<ScanSummary> ScanAsync(Guid caseId, Func<ScanProgress, Task>? progress, CancellationToken ct);
}

/// <summary>（可选）模型增强分析编排。</summary>
public interface ICaseAnalyzeWorkflow
{
    Task<ScanSummary> AnalyzeAsync(Guid caseId, Func<ScanProgress, Task>? progress, CancellationToken ct);
}

/// <summary>材料生成编排。</summary>
public interface ICaseGenerationWorkflow
{
    Task<GenerationSummary> GenerateAsync(Guid caseId, Func<ScanProgress, Task>? progress, CancellationToken ct);
}

/// <summary>上传摘要。</summary>
public sealed record UploadSummary(
    string FileName,
    int EntryCount,
    long TotalBytes,
    int ExtractedFileCount,
    IReadOnlyList<string> SkippedEntries,
    IReadOnlyList<string> Warnings,
    string SecurityNote);

/// <summary>扫描摘要。</summary>
public sealed record ScanSummary(
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

/// <summary>生成摘要。</summary>
public sealed record GenerationSummary(
    int ArtifactCount,
    long PackageBytes,
    Guid PackageArtifactId,
    IReadOnlyList<string> Warnings);
