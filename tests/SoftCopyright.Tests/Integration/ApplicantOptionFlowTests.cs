using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SoftCopyright.Tests.Support;

namespace SoftCopyright.Tests.Integration;

/// <summary>申请信息选项目录的接口集成测试。</summary>
public sealed class ApplicantOptionFlowTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task 选项目录端点返回官方规定的候选项()
    {
        var response = await _client.GetAsync("/api/softcopyright/applicant-options");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var fields = json.RootElement.GetProperty("fields").EnumerateArray().ToList();

        Assert.Equal(8, fields.Count);

        var developmentMode = fields.Single(f => f.GetProperty("field").GetString() == "DevelopmentMode");
        var labels = developmentMode.GetProperty("options").EnumerateArray()
            .Select(o => o.GetProperty("label").GetString())
            .ToList();

        Assert.Contains("单独开发", labels);
        Assert.Contains("合作开发", labels);
        Assert.Contains("委托开发", labels);
        Assert.Contains("下达任务开发", labels);
        Assert.True(developmentMode.GetProperty("required").GetBoolean());
    }

    [Fact]
    public async Task 保存申请信息时非法选项取值被拒绝()
    {
        var caseId = await CreateCaseAsync("非法选项案例");

        var response = await _client.PutAsJsonAsync($"/api/cases/{caseId}/applicant-info", ApplicantPayload(
            developmentMode: "自己写的开发方式"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("开发方式", json.RootElement.GetProperty("error").GetProperty("message").GetString());
    }

    [Fact]
    public async Task 分类号格式非法时保存被拒绝()
    {
        var caseId = await CreateCaseAsync("分类号案例");

        var response = await _client.PutAsJsonAsync($"/api/cases/{caseId}/applicant-info", ApplicantPayload(
            classificationCode: "123-45"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 中文写法被归一化为规范选项码()
    {
        var caseId = await CreateCaseAsync("归一化案例");

        var save = await _client.PutAsJsonAsync($"/api/cases/{caseId}/applicant-info", ApplicantPayload());
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        using var json = JsonDocument.Parse(await save.Content.ReadAsStringAsync());

        Assert.Equal("Solo", json.RootElement.GetProperty("developmentMode").GetString());
        Assert.Equal("OriginalAcquisition", json.RootElement.GetProperty("rightsAcquisitionMethod").GetString());
        Assert.Equal("LegalPerson", json.RootElement.GetProperty("ownerType").GetString());
        Assert.Equal("Original", json.RootElement.GetProperty("softwareSource").GetString());
        Assert.Equal("All", json.RootElement.GetProperty("rightsScope").GetString());
        Assert.Equal("General", json.RootElement.GetProperty("depositMode").GetString());
        Assert.Equal("ByOwner", json.RootElement.GetProperty("applicationHandlingMethod").GetString());
    }

    [Fact]
    public async Task 选项字段来源标注为申请人选择()
    {
        var caseId = await CreateCaseAsync("字段来源案例");

        var save = await _client.PutAsJsonAsync($"/api/cases/{caseId}/applicant-info", ApplicantPayload());
        using var json = JsonDocument.Parse(await save.Content.ReadAsStringAsync());

        var sources = json.RootElement.GetProperty("fieldSources");
        Assert.Equal("申请人选择", sources.GetProperty("DevelopmentMode").GetString());
        Assert.Equal("申请人选择", sources.GetProperty("OwnerType").GetString());
        Assert.Equal("申请人输入", sources.GetProperty("CopyrightOwner").GetString());
    }

    private async Task<Guid> CreateCaseAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/softcopyright/cases", new { name });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetGuid();
    }

    private static object ApplicantPayload(
        string developmentMode = "单独开发",
        string classificationCode = "12345-1234") => new
        {
            softwareFullName = "样例应用系统",
            softwareAbbreviation = "样例系统",
            versionNumber = "V1.0.0",
            copyrightOwner = "示例著作权人",
            developmentMode,
            rightsAcquisitionMethod = "原始取得",
            completionDate = "2026-11-01",
            firstPublicationStatus = "未发表",
            firstPublicationDate = (string?)null,
            developmentEnvironment = "Windows 11",
            runtimeEnvironment = "Windows 10",
            programmingLanguages = "C#",
            sourceProgramAmount = "20 行",
            functionSummary = "用于测试的样例软件。",
            ownerType = "法人",
            softwareSource = "原创软件",
            modificationDescription = (string?)null,
            originalRegistrationNumber = (string?)null,
            rightsScope = "全部权利",
            partialRightsDescription = (string?)null,
            firstPublicationPlace = (string?)null,
            depositMode = "一般交存",
            applicationHandlingMethod = "著作权人申请办理",
            classificationCode,
            expectedRowVersion = 0
        };
}
