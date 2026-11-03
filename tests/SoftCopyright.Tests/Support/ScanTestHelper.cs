using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Domain.Options;
using SoftCopyright.ProjectScan.Adapters;
using SoftCopyright.ProjectScan.Scanners;
using SoftCopyright.ProjectScan.Zip;
using SoftCopyright.Tests.Fixtures;
using SoftCopyright.Tests.Support;

namespace SoftCopyright.Tests.Support;

/// <summary>扫描测试辅助：把合成仓库落盘后执行一次完整扫描。</summary>
public static class ScanTestHelper
{
    public static async Task<ProjectScanResult> ScanAsync(
        IReadOnlyDictionary<string, string> entries,
        CancellationToken ct = default)
    {
        using var temp = new TestTempDirectory();
        var archivePath = RepoFixtureBuilder.CreateZip(temp.Combine("repo.zip"), entries);
        var destination = temp.Combine("extracted");

        var extractor = new ZipExtractionService(Options.Create(new ScanOptions()), NullLogger<ZipExtractionService>.Instance);
        await extractor.ExtractAsync(archivePath, destination, ct);

        var adapters = new IProjectAdapter[]
        {
            new DotNetAdapter(),
            new NodeAdapter(),
            new PythonAdapter(),
            new JavaAdapter(),
            new MiniProgramAdapter(),
            new GenericAdapter()
        };

        var scanner = new ProjectScanService(Options.Create(new ScanOptions()), adapters, NullLogger<ProjectScanService>.Instance);
        return await scanner.ScanAsync(new ProjectScanRequest(Guid.NewGuid(), destination, []), ct);
    }
}
