using Microsoft.Extensions.Options;
using SoftCopyright.Application.Validation;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Options;

namespace SoftCopyright.Tests.Unit;

/// <summary>软著申请选项目录的单元测试。</summary>
public sealed class ApplicantOptionCatalogTests
{
    [Fact]
    public void 目录覆盖官方规定的全部选项型字段()
    {
        var fields = ApplicantOptionCatalog.All.Select(f => f.Field).ToList();

        Assert.Equal(8, fields.Count);
        Assert.Contains(nameof(ApplicantInfo.SoftwareSource), fields);
        Assert.Contains(nameof(ApplicantInfo.DevelopmentMode), fields);
        Assert.Contains(nameof(ApplicantInfo.FirstPublicationStatus), fields);
        Assert.Contains(nameof(ApplicantInfo.RightsAcquisitionMethod), fields);
        Assert.Contains(nameof(ApplicantInfo.RightsScope), fields);
        Assert.Contains(nameof(ApplicantInfo.OwnerType), fields);
        Assert.Contains(nameof(ApplicantInfo.ApplicationHandlingMethod), fields);
        Assert.Contains(nameof(ApplicantInfo.DepositMode), fields);
        Assert.All(ApplicantOptionCatalog.All, f => Assert.True(f.Required));
        Assert.All(ApplicantOptionCatalog.All, f => Assert.True(f.Options.Count >= 2));
    }

    [Fact]
    public void 同一字段内的候选项取值不重复()
    {
        foreach (var field in ApplicantOptionCatalog.All)
        {
            var values = field.Options.Select(o => o.Value).ToList();
            Assert.Equal(values.Count, values.Distinct(StringComparer.OrdinalIgnoreCase).Count());

            var labels = field.Options.Select(o => o.Label).ToList();
            Assert.Equal(labels.Count, labels.Distinct(StringComparer.Ordinal).Count());
        }
    }

    [Theory]
    [InlineData(nameof(ApplicantInfo.DevelopmentMode), "Solo")]
    [InlineData(nameof(ApplicantInfo.DevelopmentMode), "单独开发")]
    [InlineData(nameof(ApplicantInfo.DevelopmentMode), "独立开发")]
    [InlineData(nameof(ApplicantInfo.RightsAcquisitionMethod), "OriginalAcquisition")]
    [InlineData(nameof(ApplicantInfo.RightsAcquisitionMethod), "原始取得")]
    [InlineData(nameof(ApplicantInfo.RightsAcquisitionMethod), "受让")]
    [InlineData(nameof(ApplicantInfo.FirstPublicationStatus), "未发表")]
    [InlineData(nameof(ApplicantInfo.OwnerType), "非法人组织")]
    [InlineData(nameof(ApplicantInfo.DepositMode), "例外交存 a：源程序前后各 30 页，机密部分遮盖")]
    public void 历史写法与中文标签都能归一化为规范选项码(string field, string raw)
    {
        var normalized = ApplicantOptionCatalog.Normalize(field, raw);

        Assert.NotNull(normalized);
        Assert.True(ApplicantOptionCatalog.IsValid(field, normalized));
        Assert.NotNull(ApplicantOptionCatalog.LabelOf(field, normalized));
    }

    [Fact]
    public void 不在候选项之内的写法无法归一化()
    {
        Assert.Null(ApplicantOptionCatalog.Normalize(nameof(ApplicantInfo.DevelopmentMode), "自己瞎写的"));
        Assert.Null(ApplicantOptionCatalog.Normalize(nameof(ApplicantInfo.DevelopmentMode), "独立开发加测试"));
        Assert.Null(ApplicantOptionCatalog.Normalize(nameof(ApplicantInfo.OwnerType), null));
        Assert.Null(ApplicantOptionCatalog.Normalize(nameof(ApplicantInfo.OwnerType), "  "));
    }

    [Fact]
    public void 条件必填规则只引用已知字段()
    {
        var known = ApplicantOptionCatalog.FieldLabels.Keys.ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(ApplicantOptionCatalog.Conditions);
        foreach (var condition in ApplicantOptionCatalog.Conditions)
        {
            Assert.True(known.Contains(condition.Field), $"未知字段：{condition.Field}");
            Assert.True(known.Contains(condition.RequiredField), $"未知字段：{condition.RequiredField}");
            Assert.NotNull(ApplicantOptionCatalog.LabelOf(condition.Field, condition.Value));
        }
    }

    [Fact]
    public void 跨字段约束指向真实字段且原始取得要求单独开发()
    {
        var known = ApplicantOptionCatalog.FieldLabels.Keys.ToHashSet(StringComparer.Ordinal);
        var constraint = ApplicantOptionCatalog.Constraints
            .Single(c => c.Field == nameof(ApplicantInfo.RightsAcquisitionMethod)
                         && c.Value == "OriginalAcquisition");

        Assert.Contains(constraint.RelatedField, known);
        Assert.Equal(nameof(ApplicantInfo.DevelopmentMode), constraint.RelatedField);
        Assert.Contains("Solo", constraint.AllowedValues);
    }

    [Fact]
    public void 检查器在取值合法时不报任何问题()
    {
        var issues = ApplicantOptionChecker.Check(Read);

        Assert.Empty(issues);
    }

    [Fact]
    public void 检查器报出缺失的必填选项()
    {
        var issues = ApplicantOptionChecker.Check(field => field == nameof(ApplicantInfo.OwnerType) ? null : Read(field));

        Assert.Contains(issues, i => i.Field == nameof(ApplicantInfo.OwnerType) && !i.Blocking);
    }

    [Fact]
    public void 检查器把越界取值标记为阻断项()
    {
        var issues = ApplicantOptionChecker.Check(field =>
            field == nameof(ApplicantInfo.DevelopmentMode) ? "自己写的开发方式" : Read(field));

        var issue = Assert.Single(issues, i => i.Field == nameof(ApplicantInfo.DevelopmentMode));
        Assert.True(issue.Blocking);
    }

    [Fact]
    public void 检查器按选项触发条件必填()
    {
        var issues = ApplicantOptionChecker.Check(field => field switch
        {
            nameof(ApplicantInfo.SoftwareSource) => "Modified",
            nameof(ApplicantInfo.ModificationDescription) => null,
            _ => Read(field)
        });

        Assert.Contains(issues, i => i.Field == nameof(ApplicantInfo.ModificationDescription));
    }

    [Fact]
    public void 检查器报出原始取得与合作开发的冲突()
    {
        var issues = ApplicantOptionChecker.Check(field => field switch
        {
            nameof(ApplicantInfo.RightsAcquisitionMethod) => "OriginalAcquisition",
            nameof(ApplicantInfo.DevelopmentMode) => "Cooperative",
            _ => Read(field)
        });

        var issue = Assert.Single(issues, i => i.Field == nameof(ApplicantInfo.DevelopmentMode));
        Assert.Contains("独立开发", issue.Message);
    }

    [Fact]
    public void 检查器校验分类号格式()
    {
        var bad = ApplicantOptionChecker.Check(field =>
            field == nameof(ApplicantInfo.ClassificationCode) ? "123-45" : Read(field));

        Assert.Contains(bad, i => i.Field == nameof(ApplicantInfo.ClassificationCode) && i.Blocking);

        var good = ApplicantOptionChecker.Check(field =>
            field == nameof(ApplicantInfo.ClassificationCode) ? "12345-1234" : Read(field));

        Assert.DoesNotContain(good, i => i.Field == nameof(ApplicantInfo.ClassificationCode));
    }

    /// <summary>一组全部合法、互不冲突的申请信息取值。</summary>
    private static string? Read(string field) => field switch
    {
        nameof(ApplicantInfo.OwnerType) => "LegalPerson",
        nameof(ApplicantInfo.SoftwareSource) => "Original",
        nameof(ApplicantInfo.DevelopmentMode) => "Solo",
        nameof(ApplicantInfo.FirstPublicationStatus) => "Unpublished",
        nameof(ApplicantInfo.RightsAcquisitionMethod) => "OriginalAcquisition",
        nameof(ApplicantInfo.RightsScope) => "All",
        nameof(ApplicantInfo.ApplicationHandlingMethod) => "ByOwner",
        nameof(ApplicantInfo.DepositMode) => "General",
        nameof(ApplicantInfo.ClassificationCode) => "12345-1234",
        _ => null
    };
}

/// <summary>软著校验规则集中选项相关规则的单元测试。</summary>
public sealed class SoftCopyrightOptionRuleTests
{
    private static SoftCopyrightValidationRuleSet CreateRuleSet() =>
        new(Options.Create(new MaterialRules()));

    private static MaterialValidationContext CreateContext(ApplicantInfo? info) =>
        new(
            new ApplicationCase
            {
                Id = Guid.NewGuid(),
                Name = "样例案例",
                MaterialKind = MaterialKind.SoftwareCopyright
            },
            info,
            [],
            [],
            [],
            null,
            null,
            [],
            false);

    private static ApplicantInfo CreateApplicantInfo() => new()
    {
        Id = Guid.NewGuid(),
        CaseId = Guid.NewGuid(),
        SoftwareFullName = "样例软件",
        VersionNumber = "V1.0",
        CopyrightOwner = "样例著作权人",
        OwnerType = "LegalPerson",
        SoftwareSource = "Original",
        DevelopmentMode = "Solo",
        RightsAcquisitionMethod = "OriginalAcquisition",
        RightsScope = "All",
        FirstPublicationStatus = "Unpublished",
        ApplicationHandlingMethod = "ByOwner",
        DepositMode = "General",
        DevelopmentEnvironment = "Windows 11",
        RuntimeEnvironment = "Windows 10",
        ProgrammingLanguages = "C#",
        SourceProgramAmount = "20 行",
        FunctionSummary = "功能简介"
    };

    [Fact]
    public void 选项齐全时规则集不出具选项问题()
    {
        var results = CreateRuleSet().Evaluate(CreateContext(CreateApplicantInfo()));

        var optionRule = Assert.Single(results, r => r.RuleId == "R-FIELDS-02");
        Assert.Equal(ValidationLevel.Pass, optionRule.Level);
    }

    [Fact]
    public void 选项缺失时规则集要求申请人补全()
    {
        var info = CreateApplicantInfo();
        info.OwnerType = string.Empty;
        info.RightsScope = string.Empty;

        var results = CreateRuleSet().Evaluate(CreateContext(info));

        var optionRule = Assert.Single(results, r => r.RuleId == "R-FIELDS-02");
        Assert.Equal(ValidationLevel.ActionRequired, optionRule.Level);
        Assert.Contains("著作权人类别", optionRule.Message);
    }

    [Fact]
    public void 取值越界时规则集要求改正()
    {
        var info = CreateApplicantInfo();
        info.DevelopmentMode = "随便写的开发方式";

        var results = CreateRuleSet().Evaluate(CreateContext(info));

        var optionRule = Assert.Single(results, r => r.RuleId == "R-FIELDS-02");
        Assert.Equal(ValidationLevel.ActionRequired, optionRule.Level);
        Assert.Contains("开发方式", optionRule.Message);
    }

    [Fact]
    public void 条件必填未满足时规则集提示补全()
    {
        var info = CreateApplicantInfo();
        info.SoftwareSource = "Modified";
        info.ModificationDescription = null;

        var results = CreateRuleSet().Evaluate(CreateContext(info));

        var optionRule = Assert.Single(results, r => r.RuleId == "R-FIELDS-02");
        Assert.Contains("修改说明", optionRule.Message);
    }
}
