using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoftCopyright.AI;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Domain.Options;
using SoftCopyright.ProjectScan.Files;
using SoftCopyright.Tests.Fixtures;
using SoftCopyright.Tests.Support;

namespace SoftCopyright.Tests.Unit;

/// <summary>AI 抽象层测试：证据包构建与模型输出校验。</summary>
public sealed class AiProviderTests
{
    private static EvidencePack CreatePack(params EvidencePackItem[] items) => new(items, [], 0);

    [Fact]
    public void 模型输出缺少证据引用时功能状态会被降级为推断()
    {
        var pack = CreatePack(new EvidencePackItem("README.md", "README 标题", "abc", "# 标题", 1, 1));
        var validator = new AiOutputValidator(NullLogger<AiOutputValidator>.Instance);

        var json = """
            {
              "features": [
                { "value": "某项功能", "status": "Observed", "explanation": "自称已验证" }
              ],
              "operationSections": ["一、概述"]
            }
            """;

        var result = validator.Validate(json, pack);

        Assert.True(result.IsValid);
        Assert.Single(result.Features);
        Assert.Equal(FactStatus.Inferred, result.Features[0].Status);
        Assert.Contains(result.Warnings, w => w.Contains("缺少有效证据引用"));
    }

    [Fact]
    public void 模型引用不存在的证据路径时该引用会被丢弃()
    {
        var pack = CreatePack(new EvidencePackItem("README.md", "README 标题", "abc", "# 标题", 1, 1));
        var validator = new AiOutputValidator(NullLogger<AiOutputValidator>.Instance);

        var json = """
            {
              "features": [
                {
                  "value": "某项功能",
                  "status": "Observed",
                  "evidence": [ { "relativePath": "不存在的文件.cs", "sha256": "abc" } ]
                }
              ],
              "operationSections": []
            }
            """;

        var result = validator.Validate(json, pack);

        Assert.True(result.IsValid);
        Assert.Empty(result.Features[0].Evidence);
        Assert.Equal(FactStatus.Inferred, result.Features[0].Status);
    }

    [Fact]
    public void 模型引用的哈希不匹配时该引用会被丢弃()
    {
        var pack = CreatePack(new EvidencePackItem("README.md", "README 标题", "abc", "# 标题", 1, 1));
        var validator = new AiOutputValidator(NullLogger<AiOutputValidator>.Instance);

        var json = """
            {
              "features": [
                {
                  "value": "某项功能",
                  "status": "Observed",
                  "evidence": [ { "relativePath": "README.md", "sha256": "deadbeef" } ]
                }
              ],
              "operationSections": []
            }
            """;

        var result = validator.Validate(json, pack);

        Assert.Empty(result.Features[0].Evidence);
        Assert.Contains(result.Warnings, w => w.Contains("哈希"));
    }

    [Fact]
    public void 非法JSON会导致校验失败并回退()
    {
        var validator = new AiOutputValidator(NullLogger<AiOutputValidator>.Instance);
        var result = validator.Validate("{ 不是合法 JSON", CreatePack());

        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task 证据包会剔除疑似凭据文件并遵守总量上限()
    {
        using var temp = new TestTempDirectory();
        var root = temp.Path;

        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# 标题\n");
        await File.WriteAllTextAsync(Path.Combine(root, ".env"), "API_KEY=secret\n");
        await File.WriteAllTextAsync(Path.Combine(root, "a.cs"), "var a = 1;\n");

        var reader = new LocalProjectFileReader(root);
        var files = new List<ProjectFileEntry>
        {
            new("README.md", 10, "sha-readme", true, "utf-8", "文本", FileCategory.Doc, false, null, 2, SecretRisk.None, false),
            new(".env", 10, "sha-env", true, "utf-8", "文本", FileCategory.Config, true, "疑似凭据文件", 1, SecretRisk.Suspected, false),
            new("a.cs", 10, "sha-a", true, "utf-8", "C#", FileCategory.Code, false, null, 1, SecretRisk.None, true)
        };

        var evidences = new List<EvidenceEntry>
        {
            new("README.md", 1, 1, "README 标题", "sha-readme", "# 标题", EvidenceSource.Scanner),
            new(".env", null, null, "项目配置", "sha-env", "API_KEY=secret", EvidenceSource.Scanner)
        };

        var options = Options.Create(new AiOptions { MaxEvidenceItems = 10, MaxEvidenceBytes = 4096, MaxItemBytes = 1024 });
        var builder = new EvidencePackBuilder(options);

        var pack = await builder.BuildAsync(files, evidences, reader, CancellationToken.None);

        Assert.DoesNotContain(pack.Items, i => i.RelativePath == ".env");
        Assert.Contains(pack.OmittedPaths, p => p == ".env");
        Assert.All(pack.Items, i => Assert.DoesNotContain("secret", i.Content, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task 文档类证据使用精确摘录而不是整份文件()
    {
        using var temp = new TestTempDirectory();
        var root = temp.Path;

        // README 正文很长，但证据只指向标题行。
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# 标题\n" + new string('甲', 500) + "\n");

        var reader = new LocalProjectFileReader(root);
        var files = new List<ProjectFileEntry>
        {
            new("README.md", 10, "sha-readme", true, "utf-8", "文本", FileCategory.Doc, false, null, 2, SecretRisk.None, false)
        };

        var evidences = new List<EvidenceEntry>
        {
            new("README.md", 1, 1, "README 标题", "sha-readme", "# 标题", EvidenceSource.Scanner)
        };

        var options = Options.Create(new AiOptions { MaxEvidenceItems = 10, MaxEvidenceBytes = 4096, MaxItemBytes = 1024 });
        var builder = new EvidencePackBuilder(options);

        var pack = await builder.BuildAsync(files, evidences, reader, CancellationToken.None);

        // 整份文档会被当成技术特征写进申请文件，这里必须只保留精确摘录。
        Assert.Equal("# 标题", pack.Items.Single().Content);
    }

    [Fact]
    public async Task 规则分析器不会编造代码内容()
    {
        var pack = CreatePack(new EvidencePackItem("README.md", "功能条目", "abc", "用户管理", 3, 3));
        var baseline = new ProjectProfile(
            Fact<string>.Unknown("未知"),
            Fact<string>.Unknown("未知"),
            Fact<string>.Unknown("未知"),
            [],
            [],
            [],
            [],
            "1.0.0");

        var analyzer = new RuleBasedProjectAnalyzer();
        var result = await analyzer.AnalyzeAsync(pack, baseline, CancellationToken.None);

        Assert.Single(result.Profile.Features);
        Assert.Equal("用户管理", result.Profile.Features[0].Value);
        Assert.Equal(FactStatus.Observed, result.Profile.Features[0].Status);
        Assert.Single(result.Profile.Features[0].Evidence);
    }
}
