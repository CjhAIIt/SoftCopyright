using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.ProjectScan.Files;

/// <summary>从已解包目录读取文件的只读访问器。</summary>
public sealed class LocalProjectFileReader(string rootPath) : IProjectFileReader
{
    public async Task<string?> ReadTextAsync(string relativePath, long maxBytes, CancellationToken ct)
    {
        var full = ResolvePath(relativePath);
        if (full is null || !File.Exists(full))
        {
            return null;
        }

        var info = new FileInfo(full);
        if (info.Length > maxBytes)
        {
            return null;
        }

        try
        {
            var bytes = await File.ReadAllBytesAsync(full, ct);
            var (isText, encoding) = TextDetection.Detect(bytes);
            return isText ? TextDetection.Decode(bytes, encoding) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>把相对路径解析为绝对路径，并确保仍在解包根目录内。</summary>
    public string? ResolvePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var root = Path.GetFullPath(rootPath);
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string combined;
        try
        {
            combined = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }
        catch (ArgumentException)
        {
            return null;
        }

        return combined.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? combined : null;
    }
}
