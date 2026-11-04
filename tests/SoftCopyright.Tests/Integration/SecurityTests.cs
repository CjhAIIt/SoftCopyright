using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SoftCopyright.Tests.Fixtures;
using SoftCopyright.Tests.Support;

namespace SoftCopyright.Tests.Integration;

/// <summary>安全与隐私验收测试。</summary>
public sealed class SecurityTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task 恶意压缩包会被拒绝且不会在工作区外写入文件()
    {
        using var temp = new TestTempDirectory();
        var zipPath = RepoFixtureBuilder.CreateZipWithTraversal(temp.Combine("evil.zip"));
        var outsideMarker = temp.Combine("evil.txt");

        var caseId = await CreateCaseAsync("恶意压缩包测试");
        using (var form = new MultipartFormDataContent())
        {
            var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(zipPath));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            form.Add(fileContent, "file", "evil.zip");

            var response = await _client.PostAsync($"/api/cases/{caseId}/upload", form);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        Assert.False(File.Exists(outsideMarker));
        Assert.False(File.Exists(Path.Combine(factory.DataRoot, "evil.txt")));
    }

    [Fact]
    public async Task 导入过程从不执行仓库中的脚本()
    {
        using var temp = new TestTempDirectory();
        var markerName = temp.Combine("executed.marker");
        var entries = RepoFixtureBuilder.ExecutableScriptSample(markerName);

        var caseId = await CreateCaseAsync("脚本执行测试");
        using (var form = new MultipartFormDataContent())
        {
            var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(
                RepoFixtureBuilder.CreateZip(temp.Combine("repo.zip"), entries)));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            form.Add(fileContent, "file", "repo.zip");

            var response = await _client.PostAsync($"/api/cases/{caseId}/upload", form);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        await _client.PostAsync($"/api/cases/{caseId}/scan", null);
        await WaitForScanAsync(caseId);

        Assert.False(File.Exists(markerName), "导入过程执行了仓库中的脚本。");
    }

    [Fact]
    public async Task 提示注入文本只被当作证据内容处理()
    {
        var caseId = await CreateCaseAsync("提示注入测试");
        using (var temp = new TestTempDirectory())
        using (var form = new MultipartFormDataContent())
        {
            var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(
                RepoFixtureBuilder.CreateZip(temp.Combine("repo.zip"), RepoFixtureBuilder.PromptInjectionSample())));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            form.Add(fileContent, "file", "repo.zip");

            var response = await _client.PostAsync($"/api/cases/{caseId}/upload", form);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        await _client.PostAsync($"/api/cases/{caseId}/scan", null);
        await WaitForScanAsync(caseId);

        var analysisResponse = await _client.GetAsync($"/api/cases/{caseId}/analysis");
        analysisResponse.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await analysisResponse.Content.ReadAsStringAsync());

        // 注入文本可以作为证据出现，但不能变成系统指令或触发任何工具调用。
        var evidenceCount = document.RootElement.GetProperty("evidences").GetArrayLength();
        Assert.True(evidenceCount > 0);
        Assert.DoesNotContain("tool_call", document.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task 申请数据与确认记录在重启后仍然存在()
    {
        var caseId = await CreateCaseAsync("持久化测试");
        await UploadAsync(caseId, RepoFixtureBuilder.NodeSample());
        await _client.PostAsync($"/api/cases/{caseId}/scan", null);
        await WaitForScanAsync(caseId);

        await _client.PostAsJsonAsync($"/api/cases/{caseId}/confirm/ProjectProfile", new { note = "持久化" });

        // 使用同一个数据目录与同一个数据库重新创建宿主，模拟应用重启。
        using var restarted = new ApiTestFactory
        {
            DataRoot = factory.DataRoot,
            MySqlConnectionString = factory.MySqlConnectionString,
            PreserveDataOnDispose = true
        };

        using var client = restarted.CreateClient();
        var response = await client.GetAsync($"/api/cases/{caseId}");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("持久化测试", document.RootElement.GetProperty("summary").GetProperty("name").GetString());

        var confirmations = await client.GetAsync($"/api/cases/{caseId}/confirmations");
        confirmations.EnsureSuccessStatusCode();
        using var confirmationDocument = JsonDocument.Parse(await confirmations.Content.ReadAsStringAsync());
        var profileConfirmation = confirmationDocument.RootElement
            .EnumerateArray()
            .First(c => c.GetProperty("stage").GetString() == "ProjectProfile");

        Assert.True(profileConfirmation.GetProperty("confirmed").GetBoolean());
    }

    private async Task<Guid> CreateCaseAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/cases", new { name });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetGuid();
    }

    private async Task UploadAsync(Guid caseId, IReadOnlyDictionary<string, string> entries)
    {
        using var temp = new TestTempDirectory();
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(
            RepoFixtureBuilder.CreateZip(temp.Combine("repo.zip"), entries)));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        form.Add(fileContent, "file", "repo.zip");

        var response = await _client.PostAsync($"/api/cases/{caseId}/upload", form);
        response.EnsureSuccessStatusCode();
    }

    private async Task WaitForScanAsync(Guid caseId)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            var response = await _client.GetAsync($"/api/cases/{caseId}/jobs");
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            foreach (var job in document.RootElement.EnumerateArray())
            {
                if (job.GetProperty("type").GetString() != "Scan")
                {
                    continue;
                }

                var status = job.GetProperty("status").GetString();
                if (status == "Succeeded")
                {
                    return;
                }

                if (status == "Failed")
                {
                    throw new InvalidOperationException($"扫描任务失败：{job.GetProperty("errorMessage").GetString()}");
                }
            }

            await Task.Delay(500);
        }

        throw new TimeoutException("等待扫描任务超时。");
    }
}
