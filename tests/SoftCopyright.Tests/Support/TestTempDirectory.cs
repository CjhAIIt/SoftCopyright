namespace SoftCopyright.Tests.Support;

/// <summary>测试临时目录，自动清理。</summary>
public sealed class TestTempDirectory : IDisposable
{
    public TestTempDirectory(string prefix = "sc-test")
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Combine(string relative) => System.IO.Path.Combine(Path, relative);

    public void Dispose()
    {
        try
        {
            SoftCopyright.Domain.SafeFileSystem.DeleteDirectory(Path);
        }
        catch (IOException)
        {
            // 忽略清理失败。
        }
    }
}
