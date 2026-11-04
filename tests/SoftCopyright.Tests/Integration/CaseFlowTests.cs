using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SoftCopyright.Tests.Fixtures;
using SoftCopyright.Tests.Support;

namespace SoftCopyright.Tests.Integration;

/// <summary>端到端流程集成测试。</summary>
public sealed class CaseFlowTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task 完整流程可以跑通并产出可下载的材料包()
    {
        // 1. 创建案例
        var createResponse = await _client.PostAsJsonAsync("/api/softcopyright/cases", new { name = "集成测试案例" });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using var createJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var caseId = createJson.RootElement.GetProperty("id").GetGuid();

        // 2. 上传 ZIP
        using var temp = new TestTempDirectory();
        var zipPath = RepoFixtureBuilder.CreateZip(temp.Combine("repo.zip"), RepoFixtureBuilder.DotNetSample());
        using (var form = new MultipartFormDataContent())
        {
            var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(zipPath));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            form.Add(fileContent, "file", "repo.zip");

            var uploadResponse = await _client.PostAsync($"/api/cases/{caseId}/upload", form);
            Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        }

        // 3. 触发扫描并等待完成
        var scanResponse = await _client.PostAsync($"/api/cases/{caseId}/scan", null);
        Assert.Equal(HttpStatusCode.Accepted, scanResponse.StatusCode);
        await WaitForJobAsync(caseId, "Scan");

        // 4. 读取分析结果
        var analysis = await GetJsonAsync($"/api/cases/{caseId}/analysis");
        Assert.Equal("Sample.App", analysis.GetProperty("name").GetString());
        Assert.Equal("1.2.0", analysis.GetProperty("version").GetString());
        Assert.True(analysis.GetProperty("evidences").GetArrayLength() > 0);

        // 5. 确认项目画像
        var confirmProfile = await _client.PostAsJsonAsync(
            $"/api/cases/{caseId}/confirm/ProjectProfile", new { note = "集成测试" });
        Assert.Equal(HttpStatusCode.OK, confirmProfile.StatusCode);

        // 6. 保存并确认申请信息
        var applicantRequest = new
        {
            softwareFullName = "样例应用系统",
            softwareAbbreviation = "样例系统",
            versionNumber = "V1.0.0",
            copyrightOwner = "示例著作权人",
            developmentMode = "独立开发",
            rightsAcquisitionMethod = "原始取得",
            completionDate = "2026-11-01",
            firstPublicationStatus = "未发表",
            firstPublicationDate = (string?)null,
            developmentEnvironment = "Windows 11 + .NET 10",
            runtimeEnvironment = "Windows 10 及以上",
            programmingLanguages = "C#",
            sourceProgramAmount = "20 行",
            functionSummary = "用于集成测试的样例软件，提供基础示例功能。",
            expectedRowVersion = 0
        };

        var saveResponse = await _client.PutAsJsonAsync($"/api/cases/{caseId}/applicant-info", applicantRequest);
        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);

        var confirmInfo = await _client.PostAsJsonAsync(
            $"/api/cases/{caseId}/confirm/ApplicantInfo", new { note = "集成测试" });
        Assert.Equal(HttpStatusCode.OK, confirmInfo.StatusCode);

        // 7. 选择真实源文件
        var candidates = await GetJsonAsync($"/api/cases/{caseId}/code-candidates");
        var selectedPath = candidates.EnumerateArray()
            .First(c => c.GetProperty("relativePath").GetString()!.EndsWith("Program.cs", StringComparison.Ordinal))
            .GetProperty("relativePath")
            .GetString()!;

        var selectionResponse = await _client.PutAsJsonAsync(
            $"/api/cases/{caseId}/code-selection", new { selectedPaths = new[] { selectedPath } });
        Assert.Equal(HttpStatusCode.OK, selectionResponse.StatusCode);

        var confirmScope = await _client.PostAsJsonAsync(
            $"/api/cases/{caseId}/confirm/CodeScope", new { note = "集成测试" });
        Assert.Equal(HttpStatusCode.OK, confirmScope.StatusCode);

        // 7.5 确认文档草稿（正式材料生成前的最后一道前置门禁）
        var confirmDraft = await _client.PostAsJsonAsync(
            $"/api/cases/{caseId}/confirm/DocumentDraft", new { note = "集成测试" });
        Assert.Equal(HttpStatusCode.OK, confirmDraft.StatusCode);

        // 8. 生成材料
        var generateResponse = await _client.PostAsync($"/api/cases/{caseId}/generate", null);
        Assert.Equal(HttpStatusCode.Accepted, generateResponse.StatusCode);
        await WaitForJobAsync(caseId, "Generate");

        // 9. 校验与产物
        var validation = await GetJsonAsync($"/api/cases/{caseId}/validation");
        Assert.True(validation.GetArrayLength() > 0);

        var artifacts = await GetJsonAsync($"/api/cases/{caseId}/artifacts");
        var artifactArray = artifacts.EnumerateArray().ToList();
        Assert.Contains(artifactArray, a => a.GetProperty("kind").GetString() == "Package");

        // 10. 下载材料包
        var packageResponse = await _client.GetAsync($"/api/cases/{caseId}/package/download");
        Assert.Equal(HttpStatusCode.OK, packageResponse.StatusCode);
        var packageBytes = await packageResponse.Content.ReadAsByteArrayAsync();
        Assert.True(packageBytes.Length > 0);

        using var packageStream = new MemoryStream(packageBytes);
        using var archive = new System.IO.Compression.ZipArchive(packageStream, System.IO.Compression.ZipArchiveMode.Read);
        var entryNames = archive.Entries.Select(e => e.FullName).ToList();
        Assert.Contains("申请案例/正式资料/代码鉴别材料.docx", entryNames);
        Assert.Contains("申请案例/正式资料/操作手册.docx", entryNames);
        Assert.Contains("申请案例/正式资料/申请表信息.txt", entryNames);
        Assert.Contains("申请案例/校验报告.html", entryNames);

        // 11. 最终确认
        var confirmFinal = await _client.PostAsJsonAsync(
            $"/api/cases/{caseId}/confirm/FinalPackage", new { note = "集成测试" });
        Assert.Equal(HttpStatusCode.OK, confirmFinal.StatusCode);

        var detail = await GetJsonAsync($"/api/cases/{caseId}");
        Assert.Equal("Archived", detail.GetProperty("summary").GetProperty("stage").GetString());
    }

    [Fact]
    public async Task 修改申请信息后下游确认会失效()
    {
        var caseId = await CreateCaseAsync("失效测试案例");
        await UploadAsync(caseId, RepoFixtureBuilder.DotNetSample());
        await _client.PostAsync($"/api/cases/{caseId}/scan", null);
        await WaitForJobAsync(caseId, "Scan");

        await _client.PostAsJsonAsync($"/api/cases/{caseId}/confirm/ProjectProfile", new { note = "x" });

        var save = await _client.PutAsJsonAsync($"/api/cases/{caseId}/applicant-info", new
        {
            softwareFullName = "样例应用系统",
            softwareAbbreviation = "",
            versionNumber = "V1.0.0",
            copyrightOwner = "示例著作权人",
            developmentMode = "独立开发",
            rightsAcquisitionMethod = "原始取得",
            completionDate = "2026-11-01",
            firstPublicationStatus = "未发表",
            firstPublicationDate = (string?)null,
            developmentEnvironment = "Windows 11",
            runtimeEnvironment = "Windows 10",
            programmingLanguages = "C#",
            sourceProgramAmount = "20 行",
            functionSummary = "功能简介",
            expectedRowVersion = 0
        });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        await _client.PostAsJsonAsync($"/api/cases/{caseId}/confirm/ApplicantInfo", new { note = "x" });

        var before = await GetJsonAsync($"/api/cases/{caseId}/confirmations");
        Assert.True(FindConfirmation(before, "ApplicantInfo").GetProperty("isStale").GetBoolean() == false);

        // 再次保存会改变内容哈希，申请人信息确认随之失效。
        await _client.PutAsJsonAsync($"/api/cases/{caseId}/applicant-info", new
        {
            softwareFullName = "样例应用系统（改名）",
            softwareAbbreviation = "",
            versionNumber = "V1.0.0",
            copyrightOwner = "示例著作权人",
            developmentMode = "独立开发",
            rightsAcquisitionMethod = "原始取得",
            completionDate = "2026-11-01",
            firstPublicationStatus = "未发表",
            firstPublicationDate = (string?)null,
            developmentEnvironment = "Windows 11",
            runtimeEnvironment = "Windows 10",
            programmingLanguages = "C#",
            sourceProgramAmount = "20 行",
            functionSummary = "功能简介",
            expectedRowVersion = 1
        });

        var after = await GetJsonAsync($"/api/cases/{caseId}/confirmations");
        Assert.True(FindConfirmation(after, "ApplicantInfo").GetProperty("isStale").GetBoolean());
    }

    [Fact]
    public async Task 未通过门禁时生成材料会被拒绝()
    {
        var caseId = await CreateCaseAsync("门禁测试案例");
        await UploadAsync(caseId, RepoFixtureBuilder.DotNetSample());

        var response = await _client.PostAsync($"/api/cases/{caseId}/generate", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task 删除案例后下载链接返回404()
    {
        var caseId = await CreateCaseAsync("删除测试案例");
        await UploadAsync(caseId, RepoFixtureBuilder.DotNetSample());
        await _client.PostAsync($"/api/cases/{caseId}/scan", null);
        await WaitForJobAsync(caseId, "Scan");

        var deleteResponse = await _client.DeleteAsync($"/api/cases/{caseId}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var detailResponse = await _client.GetAsync($"/api/cases/{caseId}");
        Assert.Equal(HttpStatusCode.NotFound, detailResponse.StatusCode);

        var packageResponse = await _client.GetAsync($"/api/cases/{caseId}/package/download");
        Assert.Equal(HttpStatusCode.NotFound, packageResponse.StatusCode);
    }

    [Fact]
    public async Task 健康检查可用()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<Guid> CreateCaseAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/softcopyright/cases", new { name });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetGuid();
    }

    private async Task UploadAsync(Guid caseId, IReadOnlyDictionary<string, string> entries)
    {
        using var temp = new TestTempDirectory();
        var zipPath = RepoFixtureBuilder.CreateZip(temp.Combine("repo.zip"), entries);

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(zipPath));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        form.Add(fileContent, "file", "repo.zip");

        var response = await _client.PostAsync($"/api/cases/{caseId}/upload", form);
        response.EnsureSuccessStatusCode();
    }

    private async Task WaitForJobAsync(Guid caseId, string jobType)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            var jobs = await GetJsonAsync($"/api/cases/{caseId}/jobs");
            var job = jobs.EnumerateArray()
                .FirstOrDefault(j => j.GetProperty("type").GetString() == jobType);

            if (job.ValueKind == JsonValueKind.Object)
            {
                var status = job.GetProperty("status").GetString();
                if (status == "Succeeded")
                {
                    return;
                }

                if (status == "Failed")
                {
                    var error = job.GetProperty("errorMessage").GetString();
                    throw new InvalidOperationException($"后台任务失败：{error}");
                }
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"等待 {jobType} 任务超时。");
    }

    private async Task<JsonElement> GetJsonAsync(string url)
    {
        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static JsonElement FindConfirmation(JsonElement confirmations, string stage)
    {
        foreach (var item in confirmations.EnumerateArray())
        {
            if (item.GetProperty("stage").GetString() == stage)
            {
                return item;
            }
        }

        throw new InvalidOperationException($"未找到阶段 {stage} 的确认记录。");
    }
}
