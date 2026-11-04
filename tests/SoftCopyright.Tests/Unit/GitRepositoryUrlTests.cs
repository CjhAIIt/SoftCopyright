using SoftCopyright.Domain.Git;
using SoftCopyright.Domain.Options;

namespace SoftCopyright.Tests.Unit;

/// <summary>Git 仓库地址解析与安全校验测试。</summary>
public sealed class GitRepositoryUrlTests
{
    private static GitImportOptions Options(
        bool allowAnyHost = false,
        bool allowLocal = false,
        string[]? hosts = null) => new()
        {
            AllowAnyHost = allowAnyHost,
            AllowLocalFileUrls = allowLocal,
            AllowedHosts = hosts ?? ["github.com", "gitee.com", "gitlab.com"]
        };

    [Theory]
    [InlineData("https://github.com/owner/repo", "github.com", "owner", "repo", "https://github.com/owner/repo.git")]
    [InlineData("https://github.com/owner/repo.git", "github.com", "owner", "repo", "https://github.com/owner/repo.git")]
    [InlineData("https://gitee.com/owner/repo", "gitee.com", "owner", "repo", "https://gitee.com/owner/repo.git")]
    [InlineData("https://gitlab.com/group/sub/repo", "gitlab.com", "sub", "repo", "https://gitlab.com/group/sub/repo.git")]
    public void 网页地址会转换为克隆地址(string input, string host, string owner, string repository, string expectedCloneUrl)
    {
        var ok = GitRepositoryUrl.TryParse(input, Options(), out var result, out var error);

        Assert.True(ok, error);
        Assert.NotNull(result);
        Assert.Equal(host, result.Host);
        Assert.Equal(owner, result.Owner);
        Assert.Equal(repository, result.Repository);
        Assert.Equal(expectedCloneUrl, result.CloneUrl);
    }

    [Fact]
    public void scp风格地址可以解析()
    {
        var ok = GitRepositoryUrl.TryParse("git@github.com:owner/repo.git", Options(), out var result, out var error);

        Assert.True(ok, error);
        Assert.Equal("github.com", result!.Host);
        Assert.Equal("owner", result.Owner);
        Assert.Equal("repo", result.Repository);
    }

    [Theory]
    [InlineData("ext::sh -c evil")]
    [InlineData("--upload-pack=evil")]
    [InlineData("https://github.com/owner/repo --config core.pager=evil")]
    public void 可执行类协议与参数注入会被拒绝(string input)
    {
        var ok = GitRepositoryUrl.TryParse(input, Options(), out _, out var error);

        Assert.False(ok);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void 以短横线开头的地址会被拒绝()
    {
        var ok = GitRepositoryUrl.TryParse("--upload-pack=evil", Options(), out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void 白名单之外的域名会被拒绝()
    {
        var ok = GitRepositoryUrl.TryParse("https://git.example.com/owner/repo", Options(), out _, out var error);

        Assert.False(ok);
        Assert.Contains("git.example.com", error);
    }

    [Fact]
    public void 允许任意域名后可以解析自建服务()
    {
        var ok = GitRepositoryUrl.TryParse(
            "https://git.example.com/owner/repo",
            Options(allowAnyHost: true),
            out var result,
            out var error);

        Assert.True(ok, error);
        Assert.Equal("git.example.com", result!.Host);
    }

    [Fact]
    public void 本地路径默认被拒绝()
    {
        var ok = GitRepositoryUrl.TryParse("C:\\repos\\demo", Options(), out _, out var error);

        Assert.False(ok);
        Assert.Contains("本地目录", error);
    }

    [Fact]
    public void 显式允许后可以解析本地路径()
    {
        var ok = GitRepositoryUrl.TryParse("C:\\repos\\demo", Options(allowLocal: true), out var result, out var error);

        Assert.True(ok, error);
        Assert.True(result!.IsLocalFile);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void 空地址会被拒绝(string input)
    {
        var ok = GitRepositoryUrl.TryParse(input, Options(), out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
    }
}
