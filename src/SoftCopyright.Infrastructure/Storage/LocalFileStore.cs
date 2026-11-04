using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Domain.Options;

namespace SoftCopyright.Infrastructure.Storage;

/// <summary>本地私有目录文件存储。路径完全由服务端生成，客户端无法控制。</summary>
public sealed class LocalFileStore : IFileStore
{
    private readonly StorageOptions _options;
    private readonly ILogger<LocalFileStore> _logger;

    public LocalFileStore(IOptions<StorageOptions> options, ILogger<LocalFileStore> logger)
    {
        _options = options.Value;
        _logger = logger;
        DataRoot = StoragePaths.ResolveDataRoot(_options.DataRoot);
        Directory.CreateDirectory(DataRoot);
    }

    public string DataRoot { get; }

    public string GetCaseDirectory(Guid caseId) =>
        Path.Combine(DataRoot, "cases", caseId.ToString("D"));

    public string GetUploadDirectory(Guid caseId)
    {
        var dir = Path.Combine(GetCaseDirectory(caseId), "upload");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public string GetExtractedRoot(Guid caseId)
    {
        var dir = Path.Combine(GetCaseDirectory(caseId), "extracted");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public string GetArtifactDirectory(Guid caseId, Guid artifactId)
    {
        var dir = Path.Combine(GetCaseDirectory(caseId), "artifacts", artifactId.ToString("D"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    public async Task<string> SaveUploadAsync(Guid caseId, string fileName, Stream content, CancellationToken ct)
    {
        var safeName = SanitizeFileName(fileName);
        var dir = GetUploadDirectory(caseId);
        var full = Path.Combine(dir, safeName);
        await using (var fs = new FileStream(full, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await content.CopyToAsync(fs, ct);
        }

        _logger.LogInformation("已保存上传包 {FileName}（大小 {Bytes} 字节）", safeName, new FileInfo(full).Length);
        return Path.GetRelativePath(DataRoot, full).Replace('\\', '/');
    }

    public async Task<string> SaveArtifactAsync(Guid caseId, Guid artifactId, string relativePath, byte[] content, CancellationToken ct)
    {
        var safeRelative = SanitizeRelativePath(relativePath);
        var full = Path.Combine(GetArtifactDirectory(caseId, artifactId), safeRelative);
        var directory = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // 先写临时文件再原子重命名，避免半截文件被下载。
        var temp = full + ".tmp";
        await File.WriteAllBytesAsync(temp, content, ct);
        File.Move(temp, full, overwrite: true);

        return Path.GetRelativePath(DataRoot, full).Replace('\\', '/');
    }

    public string ResolveSafePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidOperationException("文件路径为空。");
        }

        var combined = Path.GetFullPath(Path.Combine(DataRoot, relativePath));
        var root = DataRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("文件路径超出数据目录范围。");
        }

        return combined;
    }

    public void DeleteCaseDirectory(Guid caseId)
    {
        var dir = GetCaseDirectory(caseId);
        if (!Directory.Exists(dir))
        {
            return;
        }

        SoftCopyright.Domain.SafeFileSystem.DeleteDirectory(dir);
        _logger.LogInformation("已删除案例目录 {CaseId}", caseId);
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return "upload.zip";
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return name;
    }

    /// <summary>规范化产物相对路径：只允许安全的子目录与文件名。</summary>
    private static string SanitizeRelativePath(string relativePath)
    {
        var segments = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var safe = new List<string>();

        foreach (var segment in segments)
        {
            if (segment == "." || segment == "..")
            {
                throw new InvalidOperationException("产物路径包含非法片段。");
            }

            safe.Add(SanitizeFileName(segment));
        }

        if (safe.Count == 0)
        {
            throw new InvalidOperationException("产物路径为空。");
        }

        return Path.Combine(safe.ToArray());
    }
}
