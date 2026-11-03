using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Domain.Options;
using SoftCopyright.ProjectScan.Zip;
using SoftCopyright.Tests.Fixtures;
using SoftCopyright.Tests.Support;

namespace SoftCopyright.Tests.Unit;

/// <summary>ZIP 导入安全测试。</summary>
public sealed class ZipSecurityTests
{
    private static ZipExtractionService CreateService(ScanOptions? options = null) =>
        new(Options.Create(options ?? new ScanOptions()), NullLogger<ZipExtractionService>.Instance);

    [Fact]
    public async Task 路径穿越条目会被拒绝且不会写到目标目录之外()
    {
        using var temp = new TestTempDirectory();
        var archivePath = RepoFixtureBuilder.CreateZipWithTraversal(temp.Combine("evil.zip"));
        var destination = temp.Combine("extract");

        var exception = await Assert.ThrowsAsync<ArchiveRejectedException>(() =>
            CreateService().ExtractAsync(archivePath, destination, CancellationToken.None));

        Assert.Contains("路径穿越", exception.Message);
        Assert.False(File.Exists(temp.Combine("evil.txt")));
        Assert.False(File.Exists(Path.GetFullPath(Path.Combine(destination, "..", "evil.txt"))));
    }

    [Fact]
    public async Task 绝对路径条目会被拒绝()
    {
        using var temp = new TestTempDirectory();
        var archivePath = temp.Combine("abs.zip");
        RepoFixtureBuilder.CreateZip(archivePath, new Dictionary<string, string>
        {
            ["/etc/passwd"] = "root"
        });

        var exception = await Assert.ThrowsAsync<ArchiveRejectedException>(() =>
            CreateService().ExtractAsync(archivePath, temp.Combine("out"), CancellationToken.None));

        Assert.Contains("绝对路径", exception.Message);
    }

    [Fact]
    public async Task 驱动器路径条目会被拒绝()
    {
        using var temp = new TestTempDirectory();
        var archivePath = temp.Combine("drive.zip");
        RepoFixtureBuilder.CreateZip(archivePath, new Dictionary<string, string>
        {
            ["C:\\Windows\\system32\\evil.dll"] = "x"
        });

        var exception = await Assert.ThrowsAsync<ArchiveRejectedException>(() =>
            CreateService().ExtractAsync(archivePath, temp.Combine("out"), CancellationToken.None));

        Assert.Contains("磁盘驱动器", exception.Message);
    }

    [Fact]
    public async Task UNC路径条目会被拒绝()
    {
        using var temp = new TestTempDirectory();
        var archivePath = temp.Combine("unc.zip");
        RepoFixtureBuilder.CreateZip(archivePath, new Dictionary<string, string>
        {
            ["\\\\server\\share\\evil.txt"] = "x"
        });

        var exception = await Assert.ThrowsAsync<ArchiveRejectedException>(() =>
            CreateService().ExtractAsync(archivePath, temp.Combine("out"), CancellationToken.None));

        Assert.Contains("UNC", exception.Message);
    }

    [Fact]
    public async Task 重复路径条目会被拒绝()
    {
        using var temp = new TestTempDirectory();
        var archivePath = RepoFixtureBuilder.CreateZipWithDuplicateEntries(temp.Combine("dup.zip"));

        var exception = await Assert.ThrowsAsync<ArchiveRejectedException>(() =>
            CreateService().ExtractAsync(archivePath, temp.Combine("out"), CancellationToken.None));

        Assert.Contains("重复路径", exception.Message);
    }

    [Fact]
    public async Task 条目数量超限会被拒绝()
    {
        using var temp = new TestTempDirectory();
        var entries = new Dictionary<string, string>();
        for (var i = 0; i < 12; i++)
        {
            entries[$"file{i}.txt"] = "x";
        }

        var archivePath = RepoFixtureBuilder.CreateZip(temp.Combine("many.zip"), entries);
        var options = new ScanOptions { MaxEntryCount = 5 };

        var exception = await Assert.ThrowsAsync<ArchiveRejectedException>(() =>
            CreateService(options).ExtractAsync(archivePath, temp.Combine("out"), CancellationToken.None));

        Assert.Contains("条目数", exception.Message);
    }

    [Fact]
    public async Task 解压总量超限会被拒绝()
    {
        using var temp = new TestTempDirectory();
        var entries = new Dictionary<string, string>();
        for (var i = 0; i < 6; i++)
        {
            entries[$"file{i}.txt"] = new string('a', 2048);
        }

        var archivePath = RepoFixtureBuilder.CreateZip(temp.Combine("big.zip"), entries);
        var options = new ScanOptions { MaxTotalExtractedBytes = 1024 };

        var exception = await Assert.ThrowsAsync<ArchiveRejectedException>(() =>
            CreateService(options).ExtractAsync(archivePath, temp.Combine("out"), CancellationToken.None));

        Assert.Contains("总大小", exception.Message);
    }

    [Fact]
    public async Task 压缩炸弹条目会被拒绝()
    {
        using var temp = new TestTempDirectory();
        var entries = new Dictionary<string, string>
        {
            ["bomb.txt"] = new string('a', 4 * 1024 * 1024)
        };

        var archivePath = RepoFixtureBuilder.CreateZip(temp.Combine("bomb.zip"), entries);
        var options = new ScanOptions { CompressionRatioMinBytes = 1024, MaxCompressionRatio = 10 };

        var exception = await Assert.ThrowsAsync<ArchiveRejectedException>(() =>
            CreateService(options).ExtractAsync(archivePath, temp.Combine("out"), CancellationToken.None));

        Assert.Contains("压缩比", exception.Message);
    }

    [Fact]
    public async Task 目录层级过深会被拒绝()
    {
        using var temp = new TestTempDirectory();
        var deep = string.Join('/', Enumerable.Range(0, 40).Select(i => $"d{i}")) + "/file.txt";
        var archivePath = RepoFixtureBuilder.CreateZip(temp.Combine("deep.zip"), new Dictionary<string, string>
        {
            [deep] = "x"
        });

        var exception = await Assert.ThrowsAsync<ArchiveRejectedException>(() =>
            CreateService().ExtractAsync(archivePath, temp.Combine("out"), CancellationToken.None));

        Assert.Contains("目录", exception.Message);
    }

    [Fact]
    public async Task 非压缩包文件会被拒绝()
    {
        using var temp = new TestTempDirectory();
        var notZip = temp.Combine("not.zip");
        await File.WriteAllTextAsync(notZip, "this is not a zip archive");

        await Assert.ThrowsAsync<ArchiveRejectedException>(() =>
            CreateService().ExtractAsync(notZip, temp.Combine("out"), CancellationToken.None));
    }

    [Fact]
    public async Task 正常压缩包可以安全解包()
    {
        using var temp = new TestTempDirectory();
        var archivePath = RepoFixtureBuilder.CreateZip(temp.Combine("ok.zip"), RepoFixtureBuilder.DotNetSample());
        var destination = temp.Combine("out");

        var result = await CreateService().ExtractAsync(archivePath, destination, CancellationToken.None);

        Assert.True(result.EntryCount > 0);
        Assert.True(File.Exists(Path.Combine(destination, "README.md")));
        Assert.True(result.TotalBytes > 0);
    }
}
