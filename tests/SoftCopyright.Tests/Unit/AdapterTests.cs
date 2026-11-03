using SoftCopyright.Domain;
using SoftCopyright.Tests.Fixtures;
using SoftCopyright.Tests.Support;

namespace SoftCopyright.Tests.Unit;

/// <summary>适配器与通用扫描测试。</summary>
public sealed class AdapterTests
{
    [Fact]
    public async Task dotnet样例会被识别出项目名与版本()
    {
        var result = await ScanTestHelper.ScanAsync(RepoFixtureBuilder.DotNetSample());

        Assert.Contains(result.AdapterResults, a => a.AdapterId == "dotnet");
        Assert.Equal("Sample.App", result.Profile.Name.Value);
        Assert.Equal("1.2.0", result.Profile.Version.Value);
        Assert.Contains(result.Profile.DetectedEntrypoints, p => p.Contains("Program.cs"));
    }

    [Fact]
    public async Task node样例会被识别出包名与版本()
    {
        var result = await ScanTestHelper.ScanAsync(RepoFixtureBuilder.NodeSample());

        Assert.Contains(result.AdapterResults, a => a.AdapterId == "node");
        Assert.Equal("node-web-sample", result.Profile.Name.Value);
        Assert.Equal("2.0.1", result.Profile.Version.Value);
    }

    [Fact]
    public async Task python样例会被识别出项目名()
    {
        var result = await ScanTestHelper.ScanAsync(RepoFixtureBuilder.PythonSample());

        Assert.Contains(result.AdapterResults, a => a.AdapterId == "python");
        Assert.Equal("python-sample", result.Profile.Name.Value);
    }

    [Fact]
    public async Task java样例会被识别出坐标()
    {
        var entries = new Dictionary<string, string>
        {
            ["pom.xml"] = """
                <?xml version="1.0" encoding="UTF-8"?>
                <project xmlns="http://maven.apache.org/POM/4.0.0">
                  <modelVersion>4.0.0</modelVersion>
                  <groupId>com.example</groupId>
                  <artifactId>java-sample</artifactId>
                  <version>1.1.0</version>
                  <dependencies>
                    <dependency><groupId>junit</groupId><artifactId>junit</artifactId></dependency>
                  </dependencies>
                </project>
                """,
            ["README.md"] = "# Java 样例\n\n## 功能说明\n\n### 入口\n\n由 Main 类启动。\n"
        };

        var result = await ScanTestHelper.ScanAsync(entries);

        Assert.Contains(result.AdapterResults, a => a.AdapterId == "java");
        Assert.Equal("java-sample", result.Profile.Name.Value);
    }

    [Fact]
    public async Task 小程序样例会被识别出页面列表()
    {
        var entries = new Dictionary<string, string>
        {
            ["app.json"] = """
                { "pages": ["pages/index/index", "pages/about/about"], "window": { "navigationBarTitleText": "演示小程序" } }
                """,
            ["pages/index/index.wxml"] = "<view>hello</view>",
            ["project.config.json"] = """{ "appid": "wx0000000000000000", "projectname": "demo-mini" }""",
            ["README.md"] = "# 小程序样例\n\n## 功能说明\n\n### 首页\n\n首页展示欢迎信息。\n"
        };

        var result = await ScanTestHelper.ScanAsync(entries);

        Assert.Contains(result.AdapterResults, a => a.AdapterId == "miniprogram");
        Assert.Contains(result.Profile.DetectedEntrypoints, p => p.Contains("pages/index/index"));
    }

    [Fact]
    public async Task 未知扩展名的仓库仍会产出文件清单与代码候选()
    {
        var result = await ScanTestHelper.ScanAsync(RepoFixtureBuilder.UnknownExtensionSample());

        Assert.NotEmpty(result.Files);
        Assert.Contains(result.Files, f => f.RelativePath == "scripts/runner.foo");
        Assert.Contains(result.Files, f => f.IsCodeCandidate && f.RelativePath == "scripts/runner.foo");
        Assert.Contains(result.AdapterResults, a => a.AdapterId == "generic");
    }

    [Fact]
    public async Task 扫描会排除依赖目录并给出原因()
    {
        var entries = new Dictionary<string, string>(RepoFixtureBuilder.NodeSample())
        {
            ["node_modules/left-pad/index.js"] = "module.exports = () => {};"
        };

        var result = await ScanTestHelper.ScanAsync(entries);

        var excluded = result.Files.Single(f => f.RelativePath == "node_modules/left-pad/index.js");
        Assert.True(excluded.IsExcluded);
        Assert.False(string.IsNullOrWhiteSpace(excluded.ExclusionReason));
    }

    [Fact]
    public async Task 疑似凭据文件会被标记为排除()
    {
        var entries = new Dictionary<string, string>(RepoFixtureBuilder.NodeSample())
        {
            [".env"] = "API_KEY=super-secret-value\n"
        };

        var result = await ScanTestHelper.ScanAsync(entries);

        var secret = result.Files.Single(f => f.RelativePath == ".env");
        Assert.Equal(SecretRisk.Suspected, secret.SecretRisk);
        Assert.True(secret.IsExcluded);
    }

    [Fact]
    public async Task 每个证据都能对应到仓库中的真实文件()
    {
        var result = await ScanTestHelper.ScanAsync(RepoFixtureBuilder.DotNetSample());
        var paths = result.Files.Select(f => f.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.NotEmpty(result.Evidences);
        Assert.All(result.Evidences, e => Assert.Contains(e.RelativePath, paths));
    }

    [Fact]
    public async Task 提示注入样例仍会产出文件与证据()
    {
        var result = await ScanTestHelper.ScanAsync(RepoFixtureBuilder.PromptInjectionSample());

        Assert.NotEmpty(result.Files);
        Assert.NotEmpty(result.Evidences);
        Assert.All(result.Files, f => Assert.False(f.IsExcluded, $"文件 {f.RelativePath} 被意外排除：{f.ExclusionReason}"));
    }

    [Fact]
    public async Task 未识别的信息会进入待补充问题()
    {
        var entries = new Dictionary<string, string>
        {
            ["data.bin"] = "binary"
        };

        var result = await ScanTestHelper.ScanAsync(entries);

        Assert.NotEmpty(result.OpenQuestions);
        Assert.Equal(FactStatus.Unknown, result.Profile.Version.Status);
    }
}
