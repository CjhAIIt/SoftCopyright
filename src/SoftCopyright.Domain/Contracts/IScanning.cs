using SoftCopyright.Domain.Options;

namespace SoftCopyright.Domain.Contracts;

/// <summary>读取仓库内文件内容的只读访问器。</summary>
public interface IProjectFileReader
{
    /// <summary>读取相对路径下的文本内容；文件不存在或超限时返回 null。</summary>
    Task<string?> ReadTextAsync(string relativePath, long maxBytes, CancellationToken ct);
}

/// <summary>按案例解包根目录创建文件读取器。</summary>
public interface IProjectFileReaderFactory
{
    IProjectFileReader Create(string extractedRoot);
}

/// <summary>仓库文件索引，供适配器判断是否适用。</summary>
public sealed class ProjectFileIndex
{
    public required IReadOnlyList<ProjectFileEntry> Files { get; init; }

    public bool Contains(string relativePath) =>
        Files.Any(f => string.Equals(f.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<ProjectFileEntry> WithExtension(string extension) =>
        Files.Where(f => f.RelativePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<ProjectFileEntry> WithFileName(string fileName) =>
        Files.Where(f => string.Equals(Path.GetFileName(f.RelativePath), fileName, StringComparison.OrdinalIgnoreCase));

    public bool HasAny(params string[] fileNames) => fileNames.Any(name => WithFileName(name).Any());
}

/// <summary>扫描上下文。</summary>
public sealed class ProjectScanContext
{
    public required ProjectFileIndex Index { get; init; }

    /// <summary>已解包的仓库根目录。</summary>
    public required string RootPath { get; init; }

    public required string CaseId { get; init; }

    public required IProjectFileReader Reader { get; init; }

    public required ScanOptions Options { get; init; }
}

/// <summary>项目适配器：只解析文件，不运行任何工具链。</summary>
public interface IProjectAdapter
{
    /// <summary>适配器标识。</summary>
    string Id { get; }

    /// <summary>优先级，数字越小越先匹配；通用适配器使用最大值兜底。</summary>
    int Priority { get; }

    /// <summary>判断该适配器是否适用于当前仓库。</summary>
    bool CanHandle(ProjectFileIndex index);

    /// <summary>执行分析。</summary>
    Task<AdapterAnalysis> AnalyzeAsync(ProjectScanContext context, CancellationToken ct);
}

/// <summary>扫描请求。</summary>
public sealed record ProjectScanRequest(
    Guid CaseId,
    string ExtractedRoot,
    IReadOnlyList<string> UserExcludedDirectories);

/// <summary>扫描结果。</summary>
public sealed record ProjectScanResult(
    IReadOnlyList<ProjectFileEntry> Files,
    IReadOnlyList<EvidenceEntry> Evidences,
    IReadOnlyList<AdapterAnalysis> AdapterResults,
    ProjectProfile Profile,
    CoverageSummary Coverage,
    IReadOnlyList<string> OpenQuestions,
    string AnalyzerVersion,
    IReadOnlyList<string> Warnings);

/// <summary>仓库扫描服务。</summary>
public interface IProjectScanService
{
    Task<ProjectScanResult> ScanAsync(ProjectScanRequest request, CancellationToken ct);
}

/// <summary>安全解包服务。</summary>
public interface ISafeArchiveExtractor
{
    /// <summary>解压 ZIP 到隔离目录；拒绝时抛出 <see cref="ArchiveRejectedException"/>。</summary>
    Task<ArchiveExtractionResult> ExtractAsync(string archivePath, string destinationRoot, CancellationToken ct);
}

/// <summary>解压结果。</summary>
public sealed record ArchiveExtractionResult(
    int EntryCount,
    long TotalBytes,
    IReadOnlyList<string> SkippedEntries,
    IReadOnlyList<string> Warnings);

/// <summary>归档被拒绝时抛出，携带面向用户的中文原因。</summary>
public sealed class ArchiveRejectedException(string reason) : Exception(reason);
