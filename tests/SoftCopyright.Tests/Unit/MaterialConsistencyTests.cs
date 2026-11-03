using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoftCopyright.Application;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Domain.Options;
using SoftCopyright.Documents;
using SoftCopyright.Tests.Fixtures;
using SoftCopyright.Tests.Support;

namespace SoftCopyright.Tests.Unit;

/// <summary>材料生成与一致性测试。</summary>
public sealed class MaterialConsistencyTests
{
    private static MaterialDocumentGenerator CreateGenerator(MaterialRules? rules = null) =>
        new(Options.Create(rules ?? new MaterialRules()), NullLogger<MaterialDocumentGenerator>.Instance);

    private static MaterialGenerationRequest CreateRequest(IReadOnlyList<CodeLineBlock>? blocks = null) =>
        new(
            Guid.NewGuid(),
            "示例软件",
            "V1.0.0",
            "用于测试的软件",
            "提供示例功能",
            "示例著作权人",
            "独立开发",
            "原始取得",
            "2026-11-01",
            "未发表",
            null,
            "Windows 11 + .NET 10",
            "Windows 10 及以上",
            "C#",
            "12 行",
            [new Fact<string>("示例功能", FactStatus.Observed, [], "测试用")],
            ["C#"],
            ["src/Program.cs"],
            blocks ?? [],
            [],
            [],
            "2026.11-v1",
            "本工具内置工程配置，非官方权威解释，请按官方最新要求核对。",
            DateTimeOffset.UtcNow);

    [Fact]
    public async Task 生成的产品包含全部约定产物()
    {
        var documents = await CreateGenerator().GenerateAsync(CreateRequest(), CancellationToken.None);
        var names = documents.Select(d => d.RelativePath).ToList();

        Assert.Contains("正式资料/申请表信息.txt", names);
        Assert.Contains("正式资料/操作手册.docx", names);
        Assert.Contains("正式资料/代码鉴别材料.docx", names);
        Assert.Contains("草稿/项目画像.json", names);
        Assert.Contains("草稿/证据清单.json", names);
        Assert.Contains("草稿/申请信息草稿.json", names);
        Assert.Contains("校验报告.html", names);
        Assert.Contains("生成清单.json", names);

        // MVP 明确不产出 PDF，界面上也不提供假下载入口。
        Assert.DoesNotContain(names, n => n.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task 申请表信息包含软件全称与版本号()
    {
        var documents = await CreateGenerator().GenerateAsync(CreateRequest(), CancellationToken.None);
        var form = documents.Single(d => d.RelativePath == "正式资料/申请表信息.txt");
        var text = Encoding.UTF8.GetString(form.Content);

        Assert.Contains("示例软件", text);
        Assert.Contains("V1.0.0", text);
        Assert.Contains("示例著作权人", text);
        Assert.Contains("规则版本", text);
    }

    [Fact]
    public async Task 代码材料逐行来自真实源码()
    {
        var sourceLines = new[] { "var a = 1;", "var b = 2;", "// 结束" };
        var sha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", sourceLines)))).ToLowerInvariant();
        var block = new CodeLineBlock("src/sample.cs", sha, 1, sourceLines);

        var request = CreateRequest([block]);
        var documents = await CreateGenerator().GenerateAsync(request, CancellationToken.None);
        var code = documents.Single(d => d.RelativePath == "正式资料/代码鉴别材料.docx");

        var text = ExtractDocumentText(code.Content);
        Assert.Contains("var a = 1;", text);
        Assert.Contains("// 结束", text);
        Assert.Contains("src/sample.cs", text);
        Assert.Contains(sha[..16], text);
    }

    [Fact]
    public void 代码分页按配置行数切分并保留真实行数()
    {
        var lines = Enumerable.Range(1, 120).Select(i => $"line {i}").ToArray();
        var block = new CodeLineBlock("a.cs", "hash", 1, lines);
        var request = CreateRequest([block]);

        var pages = MaterialDocumentGenerator.BuildCodePages(request, 50, includeMarkers: true);

        Assert.Equal(3, pages.Count);
        Assert.Equal(50, pages[0].Lines.Count);
        Assert.Equal(20, pages[2].Lines.Count);
        Assert.Contains("文件：a.cs", pages[0].Marker ?? string.Empty);
    }

    [Fact]
    public async Task 校验报告区分三级结论并写明规则版本()
    {
        var request = CreateRequest() with
        {
            ValidationResults =
            [
                new ValidationResult("R-1", ValidationLevel.Pass, "通过项", "已通过。"),
                new ValidationResult("R-2", ValidationLevel.Warning, "警告项", "需要注意。"),
                new ValidationResult("R-3", ValidationLevel.ActionRequired, "待处理项", "需要申请人处理。")
            ]
        };

        var documents = await CreateGenerator().GenerateAsync(request, CancellationToken.None);
        var report = documents.Single(d => d.RelativePath == "校验报告.html");
        var html = Encoding.UTF8.GetString(report.Content);

        Assert.Contains("通过", html);
        Assert.Contains("警告", html);
        Assert.Contains("需申请人处理", html);
        Assert.Contains("2026.11-v1", html);
        Assert.Contains("不代表登记审查结论", html);
    }

    [Fact]
    public async Task 材料包为每个产物创建条目()
    {
        var documents = await CreateGenerator().GenerateAsync(CreateRequest(), CancellationToken.None);
        var package = await new MaterialPackageBuilder().BuildAsync(documents, CancellationToken.None);

        using var stream = new MemoryStream(package);
        using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);
        var entries = archive.Entries.Select(e => e.FullName).ToList();

        Assert.Contains("申请案例/正式资料/申请表信息.txt", entries);
        Assert.Contains("申请案例/正式资料/操作手册.docx", entries);
        Assert.Contains("申请案例/校验报告.html", entries);
    }

    private static string ExtractDocumentText(byte[] docxBytes)
    {
        using var stream = new MemoryStream(docxBytes);
        using var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(stream, false);
        return document.MainDocumentPart?.Document?.InnerText ?? string.Empty;
    }
}
