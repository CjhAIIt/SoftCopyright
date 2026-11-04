using SoftCopyright.AI.Patent;
using SoftCopyright.Application.Services;
using SoftCopyright.Domain;

namespace SoftCopyright.Tests.Unit;

/// <summary>专利草稿生成与校验规则测试。</summary>
public sealed class PatentDraftTests
{
    private static EvidencePack CreatePack() => new(
    [
        new EvidencePackItem("README.md", "功能条目", "sha-readme", "安全解包与路径校验", 3, 3),
        new EvidencePackItem("src/Zip/PathValidator.cs", "功能条目", "sha-validator", "路径规范化并校验是否越界", 10, 10),
        new EvidencePackItem("src/Zip/Extractor.cs", "入口点候选", "sha-extractor", "Extractor", 1, 1)
    ], [], 512);

    private static InventionDisclosure CreateDisclosure() => new()
    {
        Id = Guid.NewGuid(),
        Title = "一种安全解包方法",
        TechnicalField = "文件处理领域",
        BackgroundTechnology = "现有解包工具直接写出条目，存在路径穿越风险。",
        ProblemStatement = "如何在解包时阻止路径穿越与压缩炸弹",
        TechnicalSolution = "解包前规范化条目路径并校验根目录；拒绝符号链接与重复路径；对压缩比超限的条目直接丢弃",
        BeneficialEffects = "避免写出工作区外的文件",
        DrawingDescription = "图1为解包流程图",
        PriorArt = "现有解包工具",
        PriorArtSources = "CN123456789A 第 3 页",
        ApplicantName = "示例申请人",
        InventorNames = "张三"
    };

    [Fact]
    public async Task 技术特征会逐条绑定代码证据()
    {
        var generator = new RuleBasedPatentDraftGenerator();
        var result = await generator.DraftAsync(CreatePack(), CreateDisclosure(), CancellationToken.None);

        Assert.NotEmpty(result.Features);
        Assert.Contains(result.Features, f => f.Evidence.Count > 0);
        Assert.All(result.Features, f => Assert.NotNull(f.Title));
    }

    [Fact]
    public async Task 权利要求从1开始编号且从属项引用独立项()
    {
        var generator = new RuleBasedPatentDraftGenerator();
        var result = await generator.DraftAsync(CreatePack(), CreateDisclosure(), CancellationToken.None);

        Assert.NotEmpty(result.Claims);
        Assert.Equal(1, result.Claims[0].Number);
        Assert.Equal(ClaimType.Independent, result.Claims[0].Type);
        Assert.Null(result.Claims[0].DependsOn);

        foreach (var claim in result.Claims.Skip(1))
        {
            Assert.Equal(ClaimType.Dependent, claim.Type);
            Assert.Equal(1, claim.DependsOn);
        }
    }

    [Fact]
    public async Task 摘要不超过300字()
    {
        var generator = new RuleBasedPatentDraftGenerator();
        var result = await generator.DraftAsync(CreatePack(), CreateDisclosure(), CancellationToken.None);

        Assert.True(result.Abstract.Length <= 300);
    }

    [Fact]
    public async Task 缺少实施例时给出提示()
    {
        var generator = new RuleBasedPatentDraftGenerator();
        var pack = new EvidencePack([], [], 0);
        var result = await generator.DraftAsync(pack, CreateDisclosure(), CancellationToken.None);

        Assert.Contains(result.Warnings, w => w.Contains("实施例"));
    }

    [Fact]
    public async Task 权利要求正文不自带编号避免与编号表冲突()
    {
        var generator = new RuleBasedPatentDraftGenerator();
        var result = await generator.DraftAsync(CreatePack(), CreateDisclosure(), CancellationToken.None);

        // 编号由文档生成器统一输出，正文若再写一次编号会与编号表不一致。
        Assert.All(result.Claims, claim => Assert.False(
            claim.Text.StartsWith($"{claim.Number}.", StringComparison.Ordinal),
            $"权利要求 {claim.Number} 正文不应自带编号。"));
    }

    [Fact]
    public async Task 摘要不会出现领域措辞重复()
    {
        var generator = new RuleBasedPatentDraftGenerator();
        var disclosure = CreateDisclosure();
        disclosure.TechnicalField = "本申请涉及软件工程领域";

        var result = await generator.DraftAsync(CreatePack(), disclosure, CancellationToken.None);

        Assert.DoesNotContain("本申请涉及本申请涉及", result.Abstract, StringComparison.Ordinal);
        Assert.DoesNotContain("领域领域", result.Abstract, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 实施例不少于三个且互不重复()
    {
        var generator = new RuleBasedPatentDraftGenerator();
        var result = await generator.DraftAsync(CreatePack(), CreateDisclosure(), CancellationToken.None);

        Assert.True(result.Embodiments.Count >= 3);
        Assert.Equal(
            result.Embodiments.Count,
            result.Embodiments.Select(e => e.Description).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void 权利要求编号不连续会被校验发现()
    {
        var claims = new List<PatentClaim>
        {
            new() { Id = Guid.NewGuid(), Number = 1, Type = ClaimType.Independent, Text = "1. 一种方法" },
            new() { Id = Guid.NewGuid(), Number = 3, Type = ClaimType.Dependent, DependsOn = 1, Text = "3. 根据权利要求1" }
        };

        var results = PatentValidationRules.Evaluate(CreateDisclosure(), claims);
        var numbering = results.Single(r => r.RuleId == "P-CLAIM-01");

        Assert.Equal(ValidationLevel.ActionRequired, numbering.Level);
    }

    [Fact]
    public void 引用不存在的权利要求会被校验发现()
    {
        var claims = new List<PatentClaim>
        {
            new() { Id = Guid.NewGuid(), Number = 1, Type = ClaimType.Independent, Text = "1. 一种方法" },
            new() { Id = Guid.NewGuid(), Number = 2, Type = ClaimType.Dependent, DependsOn = 5, Text = "2. 根据权利要求5" }
        };

        var results = PatentValidationRules.Evaluate(CreateDisclosure(), claims);
        var dependency = results.Single(r => r.RuleId == "P-CLAIM-02");

        Assert.Equal(ValidationLevel.ActionRequired, dependency.Level);
        Assert.Contains("不存在", dependency.Message);
    }

    [Fact]
    public void 说明书缺少章节会被校验发现()
    {
        var disclosure = CreateDisclosure();
        disclosure.TechnicalField = string.Empty;
        disclosure.DrawingDescription = string.Empty;

        var results = PatentValidationRules.Evaluate(disclosure, []);
        var sections = results.Single(r => r.RuleId == "P-SPEC-01");

        Assert.Equal(ValidationLevel.ActionRequired, sections.Level);
        Assert.Contains("技术领域", sections.Message);
        Assert.Contains("附图说明", sections.Message);
    }

    [Fact]
    public void 现有技术没有来源会被校验发现()
    {
        var disclosure = CreateDisclosure();
        disclosure.PriorArtSources = string.Empty;

        var results = PatentValidationRules.Evaluate(disclosure, []);
        var priorArt = results.Single(r => r.RuleId == "P-PRIOR-01");

        Assert.Equal(ValidationLevel.ActionRequired, priorArt.Level);
    }

    [Fact]
    public void 摘要超长会被校验发现()
    {
        var disclosure = CreateDisclosure();
        disclosure.Abstract = new string('甲', 400);

        var results = PatentValidationRules.Evaluate(disclosure, []);
        var abstractResult = results.Single(r => r.RuleId == "P-ABS-01");

        Assert.Equal(ValidationLevel.ActionRequired, abstractResult.Level);
    }
}
