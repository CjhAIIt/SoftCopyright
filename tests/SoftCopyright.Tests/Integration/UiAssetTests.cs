using System.Net;
using System.Text.Json;
using SoftCopyright.Tests.Support;

namespace SoftCopyright.Tests.Integration;

/// <summary>
/// 界面静态资源冒烟测试。
/// 背景：Blazor 组件放在独立的 Razor 类库时，SDK 不会自动引入框架静态资源包，
/// 导致 /_framework/blazor.web.js 404 —— 页面仍能服务端渲染出样子，但所有按钮无响应。
/// 这里把该前置条件固化为测试，避免问题再次静默复发。
/// </summary>
public sealed class UiAssetTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public void 构建产物必须包含Blazor客户端脚本()
    {
        var manifest = Path.Combine(AppContext.BaseDirectory, "SoftCopyright.Api.staticwebassets.endpoints.json");
        Assert.True(File.Exists(manifest), $"未找到静态资源清单：{manifest}");

        using var document = JsonDocument.Parse(File.ReadAllText(manifest));
        var endpoints = document.RootElement.GetProperty("Endpoints");

        var routes = endpoints.EnumerateArray()
            .Select(e => e.TryGetProperty("Route", out var route) ? route.GetString() : null)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r!.TrimStart('/'))
            .ToList();

        Assert.True(
            routes.Any(r => r.StartsWith("_framework/blazor.web.js", StringComparison.OrdinalIgnoreCase)),
            "静态资源清单中缺少 _framework/blazor.web.js，宿主项目需引用 Microsoft.AspNetCore.App.Internal.Assets。");
    }

    [Fact]
    public async Task Blazor客户端脚本不能返回404()
    {
        var response = await _client.GetAsync("/_framework/blazor.web.js");

        Assert.True(
            response.StatusCode != HttpStatusCode.NotFound,
            "blazor.web.js 返回 404，页面将无法建立交互连接（按钮无响应）。");
    }

    [Fact]
    public async Task 首页可以正常渲染出中文标题()
    {
        var response = await _client.GetAsync("/");
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("软著智能申请工具", html);
        Assert.Contains("blazor.web.js", html);
    }

    [Fact]
    public async Task 静态资源中间件已注册()
    {
        var response = await _client.GetAsync("/favicon.svg");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
