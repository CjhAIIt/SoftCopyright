using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SoftCopyright.Client.Services;

/// <summary>打开本机目录的辅助方法。</summary>
/// <remarks>
/// 只用于打开**应用自己的数据目录**，不会执行任何来自被导入仓库的内容。
/// </remarks>
public static class OsLauncher
{
    /// <summary>用系统默认的文件管理器打开目录。</summary>
    public static void OpenFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Start("explorer.exe", path);
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Start("open", path);
            return;
        }

        Start("xdg-open", path);
    }

    private static void Start(string fileName, string argument)
    {
        var info = new ProcessStartInfo(fileName, argument) { UseShellExecute = false };
        using var process = Process.Start(info);
        process?.Dispose();
    }
}
