using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SoftCopyright.Domain;
using SoftCopyright.Tests.Fixtures;
using SoftCopyright.Tests.Support;

namespace SoftCopyright.Tests.Integration;

/// <summary>专利申请文件生成的端到端测试。</summary>
public sealed class PatentFlowTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task 专利案例可以完成交底书到材料包的完整流程()
    {
        // 1. 创建专利类型案例（专利流程有独立的创建端点）
        var createResponse = await _client.PostAsJsonAsync("/api/patent/cases", new { name = "专利流程用例" });
        createResponse.EnsureSuccessStatusCode();
        using var createJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var caseId = createJson.RootElement.GetProperty("id").GetGuid();

        var detail = await GetJsonAsync($"/api/cases/{caseId}");
        Assert.Equal("Patent", detail.GetProperty("summary").GetProperty("materialKind").GetString());

        // 2. 导入并扫描
        using var temp = new TestTempDirectory("sc-patent");
        var repositoryPath = GitFixture.CreateRepository(Path.Combine(temp.Path, "repo"));
        var import = await _client.PostAsJsonAsync($"/api/cases/{caseId}/import-git", new { url = repositoryPath });
        import.EnsureSuccessStatusCode();
        await WaitForJobAsync(caseId, "Scan");

        // 3. 填写技术交底书
        var disclosureResponse = await _client.PutAsJsonAsync($"/api/cases/{caseId}/patent/disclosure", new
        {
            title = "一种基于证据回溯的代码仓库分析方法",
            technicalField = "软件工程领域",
            backgroundTechnology = "现有代码分析工具多以关键词匹配为主，难以定位真实实现。",
            problemStatement = "如何在没有人工标注的情况下定位技术方案的真实实现",
            technicalSolution = "对每条技术特征在仓库中匹配证据路径；无法匹配的标记为待确认；仅在确认后写入材料",
            beneficialEffects = "提高申请文件与实现的一致性",
            drawingDescription = "图1为整体流程图",
            priorArt = "基于关键词的代码检索方法",
            priorArtSources = "CN123456789A 第 3 页",
            applicantName = "示例申请人",
            inventorNames = "张三",
            patentType = "Invention"
        });
        disclosureResponse.EnsureSuccessStatusCode();

        // 4. 生成专利草稿
        var draftResponse = await _client.PostAsJsonAsync($"/api/cases/{caseId}/patent/draft", new { });
        draftResponse.EnsureSuccessStatusCode();
        await WaitForJobAsync(caseId, "PatentDraft");

        var disclosure = await GetJsonAsync($"/api/cases/{caseId}/patent/disclosure");
        var claims = disclosure.GetProperty("claims");
        Assert.True(claims.GetArrayLength() >= 1, "应当至少生成一条权利要求。");
        Assert.Equal(1, claims[0].GetProperty("number").GetInt32());
        Assert.Equal("Independent", claims[0].GetProperty("type").GetString());

        // 5. 确认草稿（生成正式申请文件的前置门禁）
        var confirmDraft = await _client.PostAsJsonAsync($"/api/cases/{caseId}/confirm/DocumentDraft", new { note = "集成测试" });
        confirmDraft.EnsureSuccessStatusCode();

        // 6. 校验报告应包含专利规则
        var validation = await GetJsonAsync($"/api/cases/{caseId}/validation");
        var ruleIds = validation.EnumerateArray()
            .Select(v => v.GetProperty("ruleId").GetString() ?? string.Empty)
            .ToList();
        Assert.Contains("P-CLAIM-01", ruleIds);
        Assert.Contains("P-SPEC-01", ruleIds);

        // 6. 生成专利申请材料包
        var generate = await _client.PostAsync($"/api/cases/{caseId}/generate", null);
        generate.EnsureSuccessStatusCode();
        await WaitForJobAsync(caseId, "Generate");

        var artifacts = await GetJsonAsync($"/api/cases/{caseId}/artifacts");
        var kinds = artifacts.EnumerateArray().Select(a => a.GetProperty("kind").GetString()).ToList();
        Assert.Contains("PatentClaims", kinds);
        Assert.Contains("PatentSpecification", kinds);
        Assert.Contains("PatentAbstract", kinds);
        Assert.Contains("PatentDisclosure", kinds);

        var package = await _client.GetAsync($"/api/cases/{caseId}/package/download");
        Assert.Equal(HttpStatusCode.OK, package.StatusCode);
        var bytes = await package.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);

        // 7. 阶段应一路推进到「材料包已生成」，否则工作台阶段条会停在旧阶段上。
        var finalDetail = await GetJsonAsync($"/api/cases/{caseId}");
        var stage = finalDetail.GetProperty("summary").GetProperty("stage").GetString();
        Assert.Equal("PackageReady", stage);
    }

    private async Task<JsonElement> GetJsonAsync(string url)
    {
        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private async Task WaitForJobAsync(Guid caseId, string jobType)
    {
        for (var attempt = 0; attempt < 80; attempt++)
        {
            var jobs = await GetJsonAsync($"/api/cases/{caseId}/jobs");
            foreach (var job in jobs.EnumerateArray())
            {
                if (job.GetProperty("type").GetString() != jobType)
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
                    throw new InvalidOperationException($"任务 {jobType} 失败：{job.GetProperty("errorMessage").GetString()}");
                }
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"等待 {jobType} 任务超时。");
    }
}
