using SoftCopyright.Domain.Contracts;
using SoftCopyright.ProjectScan.Zip;

namespace SoftCopyright.Tests.Unit;

/// <summary>归档路径规范化测试。</summary>
public sealed class PathNormalizationTests
{
    [Theory]
    [InlineData("a/b/c.txt", "a/b/c.txt")]
    [InlineData("a\\b\\c.txt", "a/b/c.txt")]
    [InlineData("./a/b.txt", "a/b.txt")]
    [InlineData("a//b.txt", "a/b.txt")]
    [InlineData("a/b.txt/", "a/b.txt")]
    public void 合法路径会被规范化(string raw, string expected)
    {
        Assert.Equal(expected, ArchivePathValidator.Normalize(raw));
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("a/../../evil.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("C:/Windows/evil.dll")]
    [InlineData("C:\\Windows\\evil.dll")]
    [InlineData("\\\\server\\share\\x.txt")]
    [InlineData("//server/share/x.txt")]
    public void 可疑路径会被拒绝(string raw)
    {
        Assert.Throws<ArchiveRejectedException>(() => ArchivePathValidator.Normalize(raw));
    }

    [Fact]
    public void 结尾空格与点的文件名会被规整()
    {
        Assert.Equal("readme.md", ArchivePathValidator.Normalize("readme.md. "));
    }

    [Fact]
    public void Windows保留设备名会被拒绝()
    {
        Assert.Throws<ArchiveRejectedException>(() => ArchivePathValidator.Normalize("CON.txt"));
        Assert.Throws<ArchiveRejectedException>(() => ArchivePathValidator.Normalize("dir/NUL"));
    }
}
