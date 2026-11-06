using SoftCopyright.Application;
using SoftCopyright.Application.Dtos;
using SoftCopyright.Client.Services;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Tests.Fixtures;
using SoftCopyright.Tests.Support;

namespace SoftCopyright.Tests.Integration;

/// <summary>
/// 端侧引擎宿主的集成测试。
///
/// 桌面端不依赖服务器：这里直接驱动 <see cref="DesktopEngine" />，验证
/// 「导入 → 扫描 → 分析 → 申请信息 → 代码范围 → 生成材料」整条链路在
/// 没有 ASP.NET、没有 HTTP 的情况下也能跑通。
/// </summary>
public sealed class DesktopEngineTests
{
    [Fact]
    public async Task 端侧引擎在不启动服务器的情况下跑通完整软著闭环()
    {
        using var temp = new TestTempDirectory("sc-desktop");
        await using var engine = await DesktopEngine.StartAsync([], temp.Path);

        // 1. 新建案例
        var caseEntity = await engine.CreateCaseAsync("端侧闭环测试", MaterialKind.SoftwareCopyright, default);
        Assert.NotEqual(Guid.Empty, caseEntity.Id);

        // 2. 导入本机 ZIP 并扫描
        var zipPath = RepoFixtureBuilder.CreateZip(temp.Combine("repo.zip"), RepoFixtureBuilder.DotNetSample());
        var upload = await engine.ImportZipAsync(caseEntity.Id, zipPath, default);
        Assert.True(upload.ExtractedFileCount > 0, "ZIP 应当解出文件");

        var scan = await engine.ScanAsync(caseEntity.Id, default);
        Assert.True(scan.CodeCandidates > 0, "应当识别出代码候选");
        await engine.AnalyzeAsync(caseEntity.Id, default);

        var analysis = await engine.GetAnalysisAsync(caseEntity.Id, default);
        Assert.NotNull(analysis);
        Assert.False(string.IsNullOrWhiteSpace(analysis.Name));

        // 3. 确认项目画像
        var profile = await engine.ConfirmAsync(caseEntity.Id, ConfirmationStage.ProjectProfile, "测试", default);
        Assert.True(profile.Confirmed);

        // 4. 填写申请信息：选项字段用规范选项码
        var request = new UpdateApplicantInfoRequest(
            "端侧闭环软件",
            "端侧",
            "V1.0",
            "示例著作权人",
            "Solo",
            "OriginalAcquisition",
            "2026-11-01",
            "Unpublished",
            null,
            "Windows 11",
            "Windows 10",
            "C#",
            "20 行",
            "端侧离线的材料生成验证。",
            "LegalPerson",
            "Original",
            null,
            null,
            "All",
            null,
            null,
            "General",
            "ByOwner",
            "12345-1234",
            0);

        var saved = await engine.SaveApplicantAsync(caseEntity.Id, request, default);
        Assert.Equal("Solo", saved.DevelopmentMode);
        Assert.Equal("OriginalAcquisition", saved.RightsAcquisitionMethod);
        Assert.Equal("LegalPerson", saved.OwnerType);

        await engine.ConfirmAsync(caseEntity.Id, ConfirmationStage.ApplicantInfo, "测试", default);

        // 5. 选择代码范围
        var candidates = await engine.ListCodeCandidatesAsync(caseEntity.Id, default);
        var paths = candidates.Take(3).Select(c => c.RelativePath).ToList();
        await engine.SaveCodeSelectionAsync(caseEntity.Id, paths, default);

        var scope = await engine.ConfirmAsync(caseEntity.Id, ConfirmationStage.CodeScope, "测试", default);
        Assert.True(scope.Confirmed);

        // 6. 生成材料
        var generation = await engine.GenerateAsync(caseEntity.Id, default);
        Assert.True(generation.ArtifactCount > 0, "应当产出材料");

        var artifacts = await engine.ListArtifactsAsync(caseEntity.Id, default);
        Assert.Contains(artifacts, a => a.Kind == ArtifactKind.ApplicationFormInfo);
        Assert.Contains(artifacts, a => a.Kind == ArtifactKind.Package);

        var package = artifacts.Single(a => a.Kind == ArtifactKind.Package);
        var bytes = await engine.ReadArtifactAsync(package.Id, default);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public async Task 端侧引擎拒绝不在候选项之内的申请信息()
    {
        using var temp = new TestTempDirectory("sc-desktop");
        await using var engine = await DesktopEngine.StartAsync([], temp.Path);

        var caseEntity = await engine.CreateCaseAsync("非法选项", MaterialKind.SoftwareCopyright, default);

        var request = new UpdateApplicantInfoRequest(
            "名称", null, "V1.0", "著作权人",
            "自己写的开发方式", "原始取得", null, "未发表", null,
            "Windows 11", "Windows 10", "C#", "20 行", "简介",
            "法人", "原创软件", null, null, "全部权利", null, null,
            "一般交存", "著作权人申请办理", null, 0);

        await Assert.ThrowsAsync<InputValidationException>(
            () => engine.SaveApplicantAsync(caseEntity.Id, request, default));
    }

    [Fact]
    public async Task 端侧引擎拒绝格式非法的分类号()
    {
        using var temp = new TestTempDirectory("sc-desktop");
        await using var engine = await DesktopEngine.StartAsync([], temp.Path);

        var caseEntity = await engine.CreateCaseAsync("非法分类号", MaterialKind.SoftwareCopyright, default);

        var request = new UpdateApplicantInfoRequest(
            "名称", null, "V1.0", "著作权人",
            "单独开发", "原始取得", null, "未发表", null,
            "Windows 11", "Windows 10", "C#", "20 行", "简介",
            "法人", "原创软件", null, null, "全部权利", null, null,
            "一般交存", "著作权人申请办理", "123-45", 0);

        await Assert.ThrowsAsync<InputValidationException>(
            () => engine.SaveApplicantAsync(caseEntity.Id, request, default));
    }

    [Fact]
    public async Task 端侧引擎在无服务器时也能解析本机账号与模型接入状态()
    {
        using var temp = new TestTempDirectory("sc-desktop");
        await using var engine = await DesktopEngine.StartAsync([], temp.Path);

        var account = await engine.GetAccountAsync(default);
        Assert.NotNull(account);
        Assert.True(account.IsAdmin);

        // 未配置密钥时必须处于完全离线状态，不产生任何外部请求。
        var settings = await engine.GetAiSettingsAsync(default);
        Assert.Null(settings);
    }

    [Fact]
    public void 启动参数可以指定案例与步骤()
    {
        var caseId = Guid.NewGuid();
        var options = DesktopLaunchOptions.Parse(["--case", caseId.ToString(), "--step", "Applicant"]);

        Assert.Equal(caseId, options.CaseId);
        Assert.Equal(SoftCopyright.Client.ViewModels.ShellStep.Applicant, options.Step);
        Assert.False(options.SeedDemo);

        // 无法识别的参数一律忽略，不阻断启动。
        var fallback = DesktopLaunchOptions.Parse(["--unknown", "x"]);
        Assert.Null(fallback.CaseId);
        Assert.Null(fallback.Step);

        var demo = DesktopLaunchOptions.Parse(["--demo"]);
        Assert.True(demo.SeedDemo);
    }

    [Fact]
    public async Task 演示数据能生成带样例申请信息的案例()
    {
        using var temp = new TestTempDirectory("sc-desktop");
        await using var engine = await DesktopEngine.StartAsync([], temp.Path);

        var seeder = new SoftCopyright.Client.Services.DemoSeeder(engine);
        var caseId = await seeder.SeedAsync();

        var info = await engine.GetApplicantAsync(caseId, default);
        Assert.NotNull(info);
        Assert.Equal("Modified", info.SoftwareSource);
        Assert.Equal("Partial", info.RightsScope);
        Assert.Equal("Published", info.FirstPublicationStatus);
        Assert.True(info.IsConfirmed);
    }
}
