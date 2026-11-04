namespace SoftCopyright.Domain;

/// <summary>
/// 文件系统安全删除：git 仓库中的对象文件是只读的，直接递归删除会抛 UnauthorizedAccessException。
/// </summary>
public static class SafeFileSystem
{
    /// <summary>递归删除目录，忽略只读属性与个别失败项。</summary>
    public static void DeleteDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return;
        }

        NormalizeAttributes(path);

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (UnauthorizedAccessException)
        {
            // 再次规整属性后重试一次。
            NormalizeAttributes(path);
            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch (Exception)
            {
                // 忽略清理失败。
            }
        }
        catch (IOException)
        {
            // 忽略清理失败。
        }
    }

    private static void NormalizeAttributes(string root)
    {
        try
        {
            var directory = new DirectoryInfo(root);
            foreach (var file in directory.GetFiles("*", SearchOption.AllDirectories))
            {
                TrySetNormal(file);
            }

            foreach (var child in directory.GetDirectories("*", SearchOption.AllDirectories))
            {
                TrySetNormal(child);
            }

            TrySetNormal(directory);
        }
        catch (Exception)
        {
            // 属性规整失败不影响后续删除尝试。
        }
    }

    private static void TrySetNormal(FileSystemInfo info)
    {
        try
        {
            info.Attributes = FileAttributes.Normal;
        }
        catch (Exception)
        {
            // 忽略单个文件的属性设置失败。
        }
    }
}
