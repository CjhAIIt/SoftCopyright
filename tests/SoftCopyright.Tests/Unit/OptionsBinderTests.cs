using Microsoft.Extensions.Configuration;
using SoftCopyright.Domain.Options;
using SoftCopyright.Infrastructure;

namespace SoftCopyright.Tests.Unit;

/// <summary>配置绑定测试：数组属性不能与默认值叠加成重复项。</summary>
public sealed class OptionsBinderTests
{
    [Fact]
    public void 配置项会覆盖默认值而不是追加()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GitImport:AllowedHosts:0"] = "git.example.com"
            })
            .Build();

        var options = new GitImportOptions();
        OptionsBinder.Bind(options, configuration.GetSection(GitImportOptions.SectionName));

        // 默认值是 github/gitee/gitlab，配置里只有 git.example.com；
        // 正确结果是"配置覆盖默认"，而不是两者叠加。
        Assert.Equal(["git.example.com"], options.AllowedHosts);
    }

    [Fact]
    public void 配置未提供该项时沿用默认值()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GitImport:TimeoutSeconds"] = "120"
            })
            .Build();

        var options = new GitImportOptions();
        OptionsBinder.Bind(options, configuration.GetSection(GitImportOptions.SectionName));

        Assert.Equal(120, options.TimeoutSeconds);
        Assert.Contains("github.com", options.AllowedHosts);
        Assert.Equal(3, options.AllowedHosts.Length);
    }

    [Fact]
    public void 嵌套选项对象里的数组同样不会被叠加()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MaterialRules:Fields:RequiredFields:0"] = "SoftwareFullName"
            })
            .Build();

        var options = new MaterialRules();
        OptionsBinder.Bind(options, configuration.GetSection(MaterialRules.SectionName));

        Assert.Single(options.Fields.RequiredFields);
        Assert.Equal("SoftwareFullName", options.Fields.RequiredFields[0]);
    }
}
