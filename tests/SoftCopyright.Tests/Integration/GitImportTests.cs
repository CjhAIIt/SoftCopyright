using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Tests.Support;

namespace SoftCopyright.Tests.Integration;

/// <summary>Git 仓库导入通道的端到端测试。</summary>
public sealed class GitImportTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task 可以从本地Git仓库导入并自动完成扫描()
    {
        using var temp = new TestTempDirectory("sc-git");
        var repositoryPath = GitFixture.CreateRepository(Path.Combine(temp.Path, "repo"));

        var createResponse = await _client.PostAsJsonAsync("/api/softcopyright/cases", new { name = "Git 导入用例" });
        createResponse.EnsureSuccessStatusCode();
        using var createJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var caseId = createJson.RootElement.GetProperty("id").GetGuid();

        var importResponse = await _client.PostAsJsonAsync(
            $"/api/cases/{caseId}/import-git",
            new GitImportRequest(repositoryPath));

        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);

        using var importJson = JsonDocument.Parse(await importResponse.Content.ReadAsStringAsync());
        var import = importJson.RootElement;
        Assert.True(import.GetProperty("fileCount").GetInt32() > 0, "导入的文件数量应大于 0。");
        Assert.False(string.IsNullOrWhiteSpace(import.GetProperty("commit").GetString()));

        // 克隆完成后必须删除 .git，且不留下任何仓库脚本执行痕迹。
        var extractedRoot = Path.Combine(factory.DataRoot, "cases", caseId.ToString("D"), "extracted");
        Assert.True(Directory.Exists(extractedRoot));
        Assert.False(Directory.Exists(Path.Combine(extractedRoot, ".git")), "导入后不应保留 .git 目录。");
        Assert.True(File.Exists(Path.Combine(extractedRoot, "README.md")));

        // 导入后自动排队扫描，稍候应产出分析结果。
        await WaitForScanAsync(caseId);

        var analysisResponse = await _client.GetAsync($"/api/cases/{caseId}/analysis");
        analysisResponse.EnsureSuccessStatusCode();
        using var analysisJson = JsonDocument.Parse(await analysisResponse.Content.ReadAsStringAsync());
        var analysis = analysisJson.RootElement;

        Assert.Equal("git-import-demo", analysis.GetProperty("name").GetString());
        Assert.Equal("3.1.0", analysis.GetProperty("version").GetString());
        Assert.True(analysis.GetProperty("evidences").GetArrayLength() > 0);
    }

    [Fact]
    public async Task 导入后案例会记录来源仓库与提交号()
    {
        using var temp = new TestTempDirectory("sc-git-meta");
        var repositoryPath = GitFixture.CreateRepository(Path.Combine(temp.Path, "repo"));

        var createResponse = await _client.PostAsJsonAsync("/api/softcopyright/cases", new { name = "Git 来源记录" });
        createResponse.EnsureSuccessStatusCode();
        using var createJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var caseId = createJson.RootElement.GetProperty("id").GetGuid();

        var importResponse = await _client.PostAsJsonAsync(
            $"/api/cases/{caseId}/import-git",
            new GitImportRequest(repositoryPath));
        importResponse.EnsureSuccessStatusCode();

        var detailResponse = await _client.GetAsync($"/api/cases/{caseId}");
        detailResponse.EnsureSuccessStatusCode();
        using var detailJson = JsonDocument.Parse(await detailResponse.Content.ReadAsStringAsync());
        var summary = detailJson.RootElement.GetProperty("summary");

        Assert.False(string.IsNullOrWhiteSpace(summary.GetProperty("sourceRepositoryUrl").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(summary.GetProperty("sourceCommit").GetString()));
    }

    [Fact]
    public async Task 不在白名单的域名会被拒绝()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/softcopyright/cases", new { name = "非法域名" });
        createResponse.EnsureSuccessStatusCode();
        using var createJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var caseId = createJson.RootElement.GetProperty("id").GetGuid();

        var response = await _client.PostAsJsonAsync(
            $"/api/cases/{caseId}/import-git",
            new GitImportRequest("https://git.example.com/owner/repo"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task WaitForScanAsync(Guid caseId)
    {
        for (var attempt = 0; attempt < 80; attempt++)
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
