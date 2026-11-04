using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using SoftCopyright.Domain;
using SoftCopyright.Tests.Support;

namespace SoftCopyright.Tests.Integration;

/// <summary>注册、登录与引导流程的端到端测试（使用真实 Cookie 认证）。</summary>
public sealed class AccountFlowTests : IClassFixture<AccountFlowTests.AccountFactory>
{
    /// <summary>使用真实认证的测试宿主。</summary>
    public sealed class AccountFactory : ApiTestFactory
    {
        public AccountFactory()
        {
            UseRealAuthentication = true;
        }
    }

    private readonly AccountFactory _factory;

    public AccountFlowTests(AccountFactory factory) => _factory = factory;

    [Fact]
    public async Task 注册后自动登录并进入引导流程()
    {
        var client = CreateClient();

        // 匿名访问案例列表应被拒绝。
        var anonymous = await client.GetAsync("/api/cases");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var register = await PostFormAsync(client, "/api/auth/register", new Dictionary<string, string>
        {
            ["userName"] = "alice-" + Guid.NewGuid().ToString("N")[..8],
            ["displayName"] = "爱丽丝",
            ["password"] = "correct-horse-8"
        });

        Assert.Equal(HttpStatusCode.Found, register.StatusCode);
        Assert.Equal("/onboarding", register.Headers.Location?.OriginalString);

        // 登录态已建立。
        var me = await client.GetAsync("/api/account/me");
        me.EnsureSuccessStatusCode();
        using var meJson = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        Assert.Equal("爱丽丝", meJson.RootElement.GetProperty("displayName").GetString());
        Assert.False(meJson.RootElement.GetProperty("onboardingCompleted").GetBoolean());
        Assert.False(meJson.RootElement.GetProperty("aiAssistantEnabled").GetBoolean());
    }

    [Fact]
    public async Task 密码错误时无法登录()
    {
        var client = CreateClient();
        var userName = "bob-" + Guid.NewGuid().ToString("N")[..8];

        var register = await PostFormAsync(client, "/api/auth/register", new Dictionary<string, string>
        {
            ["userName"] = userName,
            ["displayName"] = "鲍勃",
            ["password"] = "correct-horse-8"
        });
        Assert.Equal(HttpStatusCode.Found, register.StatusCode);

        // 退出后用错误密码登录。
        await PostFormAsync(client, "/api/auth/logout", new Dictionary<string, string>());

        var login = await PostFormAsync(client, "/api/auth/login", new Dictionary<string, string>
        {
            ["userName"] = userName,
            ["password"] = "wrong-password"
        });

        Assert.Equal(HttpStatusCode.Found, login.StatusCode);
        Assert.StartsWith("/login?error=", login.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task 引导流程可以选择完全离线的免费功能()
    {
        var client = CreateClient();
        await RegisterAsync(client, "carol-" + Guid.NewGuid().ToString("N")[..8]);

        var skip = await PostFormAsync(client, "/api/account/onboarding/skip", new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.Found, skip.StatusCode);
        Assert.Equal("/", skip.Headers.Location?.OriginalString);

        var me = await client.GetAsync("/api/account/me");
        using var meJson = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        Assert.True(meJson.RootElement.GetProperty("onboardingCompleted").GetBoolean());
        Assert.False(meJson.RootElement.GetProperty("aiAssistantEnabled").GetBoolean());
        Assert.False(meJson.RootElement.GetProperty("hasAiConfiguration").GetBoolean());
    }

    [Fact]
    public async Task 引导流程配置密钥后启用模型且密钥不以明文落库()
    {
        var client = CreateClient();
        var userName = "dave-" + Guid.NewGuid().ToString("N")[..8];
        await RegisterAsync(client, userName);

        const string apiKey = "sk-test-THIS-MUST-NOT-BE-PLAINTEXT";
        var save = await PostFormAsync(client, "/api/account/onboarding/ai", new Dictionary<string, string>
        {
            ["baseUrl"] = "https://example.invalid/v1",
            ["model"] = "demo-model",
            ["apiKey"] = apiKey
        });

        Assert.Equal(HttpStatusCode.Found, save.StatusCode);
        Assert.Equal("/", save.Headers.Location?.OriginalString);

        var me = await client.GetAsync("/api/account/me");
        using var meJson = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        Assert.True(meJson.RootElement.GetProperty("aiAssistantEnabled").GetBoolean());
        Assert.True(meJson.RootElement.GetProperty("hasAiConfiguration").GetBoolean());

        // 密钥必须以密文落库：数据库里不能出现明文，且密文非空。
        var raw = await ReadRawUserRowAsync(userName);
        Assert.DoesNotContain(apiKey, raw, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(raw));
        Assert.NotEqual(apiKey, raw);
    }

    [Fact]
    public async Task 案例只对所属账号可见()
    {
        var client = CreateClient();
        await RegisterAsync(client, "erin-" + Guid.NewGuid().ToString("N")[..8]);

        var created = await client.PostAsync("/api/softcopyright/cases", new StringContent("{\"name\":\"私有案例\"}", System.Text.Encoding.UTF8, "application/json"));
        created.EnsureSuccessStatusCode();
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var caseId = createdJson.RootElement.GetProperty("id").GetGuid();

        var list = await client.GetAsync("/api/cases");
        list.EnsureSuccessStatusCode();
        using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Contains(listJson.RootElement.EnumerateArray(), c => c.GetProperty("id").GetGuid() == caseId);

        // 换一个账号，就看不到别人的案例。
        var other = CreateClient();
        await RegisterAsync(other, "frank-" + Guid.NewGuid().ToString("N")[..8]);

        var otherList = await other.GetAsync("/api/cases");
        otherList.EnsureSuccessStatusCode();
        using var otherJson = JsonDocument.Parse(await otherList.Content.ReadAsStringAsync());
        Assert.DoesNotContain(otherJson.RootElement.EnumerateArray(), c => c.GetProperty("id").GetGuid() == caseId);

        // 直接访问他人的案例返回 404。
        var direct = await other.GetAsync($"/api/cases/{caseId}");
        Assert.Equal(HttpStatusCode.NotFound, direct.StatusCode);
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });

    private async Task RegisterAsync(HttpClient client, string userName) =>
        await PostFormAsync(client, "/api/auth/register", new Dictionary<string, string>
        {
            ["userName"] = userName,
            ["displayName"] = userName,
            ["password"] = "correct-horse-8"
        });

    /// <summary>带防伪令牌的表单提交：先取页面上的令牌，再连同 Cookie 一起提交。</summary>
    private async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client,
        string url,
        Dictionary<string, string> fields)
    {
        var token = await GetAntiforgeryTokenAsync(client);
        fields["__RequestVerificationToken"] = token;

        return await client.PostAsync(url, new FormUrlEncodedContent(fields));
    }

    /// <summary>从登录页取防伪令牌。</summary>
    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        var page = await client.GetAsync("/login");
        page.EnsureSuccessStatusCode();
        var html = await page.Content.ReadAsStringAsync();
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "登录页应包含防伪令牌。");
        return match.Groups[1].Value;
    }

    /// <summary>直接读数据库原始行，确认密钥没有以明文存储。</summary>
    private async Task<string> ReadRawUserRowAsync(string userName)
    {
        var factory = (AccountFactory)_factory;
        await factory.EnsureDatabaseAsync();

        if (string.Equals(factory.Provider, "MySql", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(factory.MySqlConnectionString))
        {
            var builder = new MySql.Data.MySqlClient.MySqlConnectionStringBuilder(factory.MySqlConnectionString);
            using var connection = new MySql.Data.MySqlClient.MySqlConnection(builder.ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT EncryptedAiApiKey, EncryptedAiModel, EncryptedAiBaseUrl FROM ApplicationUsers WHERE UserName = @name";
            command.Parameters.AddWithValue("@name", userName);
            var value = await command.ExecuteScalarAsync();
            return value?.ToString() ?? string.Empty;
        }

        // SQLite 回退：直接读文件字节。
        var dbPath = Path.Combine(factory.DataRoot, "test.db");
        return await File.ReadAllTextAsync(dbPath);
    }
}
