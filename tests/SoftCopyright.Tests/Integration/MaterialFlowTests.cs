using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SoftCopyright.Domain;
using SoftCopyright.Tests.Support;

namespace SoftCopyright.Tests.Integration;

/// <summary>两条材料流程相互独立的端到端测试。</summary>
public sealed class MaterialFlowTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task 流程描述提供两条互不相同的流程()
    {
        var flows = await GetJsonAsync("/api/flows");
        var items = flows.EnumerateArray().ToList();

        Assert.Equal(2, items.Count);

        var soft = items.Single(f => f.GetProperty("kind").GetString() == "SoftwareCopyright");
        var patent = items.Single(f => f.GetProperty("kind").GetString() == "Patent");

        // 入口名称与工作区路径不同。
        Assert.NotEqual(soft.GetProperty("displayName").GetString(), patent.GetProperty("displayName").GetString());
        Assert.NotEqual(soft.GetProperty("workspacePath").GetString(), patent.GetProperty("workspacePath").GetString());

        // 阶段文案不同：软著是"申请信息 / 代码范围"，专利是"交底书 / 权利要求"。
        var softStages = soft.GetProperty("stageLabels").EnumerateArray().Select(s => s.GetString()).ToList();
        var patentStages = patent.GetProperty("stageLabels").EnumerateArray().Select(s => s.GetString()).ToList();
        Assert.Contains("代码范围", softStages);
        Assert.DoesNotContain("代码范围", patentStages);
        Assert.Contains("交底书", patentStages);
        Assert.DoesNotContain("交底书", softStages);

        // 页面构成不同：软著有申请信息与代码材料，专利没有。
        var softRoutes = soft.GetProperty("tabs").EnumerateArray().Select(t => t.GetProperty("route").GetString()).ToList();
        var patentRoutes = patent.GetProperty("tabs").EnumerateArray().Select(t => t.GetProperty("route").GetString()).ToList();
        Assert.Contains("code", softRoutes);
        Assert.DoesNotContain("code", patentRoutes);
        Assert.Contains("patent", patentRoutes);
        Assert.DoesNotContain("patent", softRoutes);

        // 只有软著需要勾选代码范围。
        Assert.True(soft.GetProperty("requiresCodeSelection").GetBoolean());
        Assert.False(patent.GetProperty("requiresCodeSelection").GetBoolean());
    }

    [Fact]
    public async Task 两条流程各有独立的创建端点与案例列表()
    {
        var softResponse = await _client.PostAsJsonAsync("/api/softcopyright/cases", new { name = "软著工作区用例" });
        softResponse.EnsureSuccessStatusCode();
        using var softJson = JsonDocument.Parse(await softResponse.Content.ReadAsStringAsync());
        var softId = softJson.RootElement.GetProperty("id").GetGuid();

        var patentResponse = await _client.PostAsJsonAsync("/api/patent/cases", new { name = "专利工作区用例" });
        patentResponse.EnsureSuccessStatusCode();
        using var patentJson = JsonDocument.Parse(await patentResponse.Content.ReadAsStringAsync());
        var patentId = patentJson.RootElement.GetProperty("id").GetGuid();

        var softDetail = await GetJsonAsync($"/api/cases/{softId}");
        var patentDetail = await GetJsonAsync($"/api/cases/{patentId}");
        Assert.Equal("SoftwareCopyright", softDetail.GetProperty("summary").GetProperty("materialKind").GetString());
        Assert.Equal("Patent", patentDetail.GetProperty("summary").GetProperty("materialKind").GetString());

        // 各自的工作区只看到自己这一类案例。
        var softList = await GetJsonAsync("/api/cases/by-kind/SoftwareCopyright");
        var patentList = await GetJsonAsync("/api/cases/by-kind/Patent");

        Assert.Contains(softList.EnumerateArray(), c => c.GetProperty("id").GetGuid() == softId);
        Assert.DoesNotContain(softList.EnumerateArray(), c => c.GetProperty("id").GetGuid() == patentId);

        Assert.Contains(patentList.EnumerateArray(), c => c.GetProperty("id").GetGuid() == patentId);
        Assert.DoesNotContain(patentList.EnumerateArray(), c => c.GetProperty("id").GetGuid() == softId);
    }

    [Fact]
    public async Task 软著案例不提交代码范围时拒绝生成材料()
    {
        var response = await _client.PostAsJsonAsync("/api/softcopyright/cases", new { name = "缺代码范围用例" });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var caseId = json.RootElement.GetProperty("id").GetGuid();

        var generate = await _client.PostAsync($"/api/cases/{caseId}/generate", null);
        Assert.Equal(HttpStatusCode.Conflict, generate.StatusCode);
    }

    private async Task<JsonElement> GetJsonAsync(string url)
    {
        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
