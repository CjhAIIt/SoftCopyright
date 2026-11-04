using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Domain.Options;

namespace SoftCopyright.AI;

/// <summary>
/// OpenAI 兼容 HTTP 模型 Provider。
///
/// 接入设置按优先级取：
/// 1. 当前账号在引导流程中配置的个人设置（BaseUrl / Model / API Key）；
/// 2. 全局环境变量配置（无人值守场景）。
/// 两者都不可用时**不发起任何请求**，由上层回退到规则实现。
/// 密钥不会写入日志。
/// </summary>
public sealed class HttpAiProvider : IAiProvider, IDisposable
{
    private readonly AiOptions _options;
    private readonly IAiSettingsProvider _settingsProvider;
    private readonly ILogger<HttpAiProvider> _logger;
    private readonly HttpClient _httpClient;

    public HttpAiProvider(
        IOptions<AiOptions> options,
        IAiSettingsProvider settingsProvider,
        ILogger<HttpAiProvider> logger)
    {
        _options = options.Value;
        _settingsProvider = settingsProvider;
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(5, _options.TimeoutSeconds))
        };
    }

    public string Id => "http";

    public async Task<AiAccountSettings?> ResolveAsync(CancellationToken ct)
    {
        var account = await _settingsProvider.GetCurrentAsync(ct);
        if (account is not null)
        {
            return account;
        }

        // 回退到全局配置：密钥只从环境变量读取，不写入代码与日志。
        var apiKey = Environment.GetEnvironmentVariable(_options.ApiKeyEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = _options.ApiKey;
        }

        if (string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(_options.BaseUrl) ||
            string.IsNullOrWhiteSpace(_options.Model))
        {
            return null;
        }

        return new AiAccountSettings(_options.BaseUrl, _options.Model, apiKey);
    }

    public async Task<string> CompleteJsonAsync(AiRequest request, CancellationToken ct)
    {
        var settings = await ResolveAsync(ct);
        if (settings is null)
        {
            throw new InvalidOperationException("模型 Provider 未启用（缺少 BaseUrl / Model / API Key）。");
        }

        var endpoint = $"{settings.BaseUrl.TrimEnd('/')}/chat/completions";
        var payload = new
        {
            model = settings.Model,
            temperature = 0,
            max_tokens = request.MaxTokens,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPayload }
            }
        };

        var json = JsonSerializer.Serialize(payload);

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
            message.Content = new StringContent(json, Encoding.UTF8, "application/json");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

            using var response = await _httpClient.SendAsync(message, ct);

            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < 3)
            {
                var delay = TimeSpan.FromSeconds(attempt * 2);
                _logger.LogWarning("模型接口限流，{Delay} 秒后重试（第 {Attempt} 次）", delay, attempt);
                await Task.Delay(delay, ct);
                continue;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(ct);
        }

        throw new InvalidOperationException("模型接口连续限流，已放弃本次调用。");
    }

    public void Dispose() => _httpClient.Dispose();
}
