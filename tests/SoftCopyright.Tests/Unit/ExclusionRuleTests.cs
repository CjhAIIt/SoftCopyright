using SoftCopyright.Domain;
using SoftCopyright.Domain.Options;
using SoftCopyright.ProjectScan.Files;

namespace SoftCopyright.Tests.Unit;

/// <summary>排除规则与凭据检测测试。</summary>
public sealed class ExclusionRuleTests
{
    private static readonly ScanOptions Options = new();

    [Theory]
    [InlineData("node_modules/pkg/index.js")]
    [InlineData("src/bin/Debug/app.dll")]
    [InlineData("obj/Project.csproj.nuget.g.props")]
    [InlineData("dist/app.min.js")]
    public void 默认排除规则会命中依赖与构建产物(string path)
    {
        var (excluded, reason) = ExclusionRules.EvaluateUserExclusions(path, Options, []);
        Assert.True(excluded);
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    [Fact]
    public void 用户自定义排除目录生效()
    {
        var (excluded, reason) = ExclusionRules.EvaluateUserExclusions("docs/internal/notes.md", Options, ["docs"]);
        Assert.True(excluded);
        Assert.Contains("docs", reason);
    }

    [Fact]
    public void 普通源码不会被排除()
    {
        var (excluded, _) = ExclusionRules.EvaluateUserExclusions("src/app/Program.cs", Options, []);
        Assert.False(excluded);
    }

    [Theory]
    [InlineData(".env")]
    [InlineData("config/secrets.pem")]
    [InlineData("id_rsa")]
    [InlineData("deploy/private.key")]
    [InlineData("app.credentials.json")]
    public void 疑似凭据文件名会被识别(string fileName)
    {
        Assert.True(SecretDetector.IsSensitiveFileName(fileName));
    }

    [Theory]
    [InlineData("普通说明文件.md")]
    [InlineData("src/main.cs")]
    public void 普通文件名不会被误判(string fileName)
    {
        Assert.False(SecretDetector.IsSensitiveFileName(fileName));
    }

    [Theory]
    [InlineData("-----BEGIN RSA PRIVATE KEY-----\nabc\n")]
    [InlineData("AKIAIOSFODNN7EXAMPLE")]
    [InlineData("ghp_abcdefghijklmnopqrstuvwxyz0123")]
    [InlineData("api_key = \"super-secret-value\"")]
    public void 疑似凭据内容会被识别(string content)
    {
        Assert.True(SecretDetector.ContainsSecretContent(content));
    }

    [Fact]
    public void 正常内容不会被误判()
    {
        Assert.False(SecretDetector.ContainsSecretContent("这是一段普通的中文说明，用于说明功能。"));
    }
}
