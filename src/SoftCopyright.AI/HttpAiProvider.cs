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

/// <summary>OpenAI 兼容 HTTP 模型 Provider。未配置密钥时不发起任何请求。</summary>
public sealed class HttpAiProvider : IAiProvider, IDisposable
{
    private readonly AiOptions _options;
    private readonly ILogger<HttpAiProvider> _logger;
    private readonly HttpClient _httpClient;

    public HttpAiProvider(IOptions<AiOptions> options, ILogger<HttpAiProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(5, _options.TimeoutSeconds))
        };
    }

    public string Id => "http";

    public bool IsEnabled =>
        !string.IsNullOrWhiteSpace(ResolveApiKey())
        && !string.IsNullOrWhiteSpace(_options.BaseUrl)
        && !string.IsNullOrWhiteSpace(_options.Model);

    /// <summary>密钥只从环境变量或本地用户配置读取，不写入代码与日志。</summary>
    private string? ResolveApiKey()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(_options.ApiKeyEnvironmentVariable);
        return !string.IsNullOrWhiteSpace(fromEnvironment) ? fromEnvironment : _options.ApiKey;
    }

    public async Task<string> CompleteJsonAsync(AiRequest request, CancellationToken ct)
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("模型 Provider 未启用（缺少 BaseUrl / Model / API Key）。");
        }

        var endpoint = $"{_options.BaseUrl!.TrimEnd('/')}/chat/completions";
        var payload = new
        {
            model = _options.Model,
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
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ResolveApiKey());

            using var response = await _httpClient.SendAsync(message, ct);

            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < 3)
            {
                var delay = TimeSpan.FromSeconds(attempt * 2);
                _logger.LogWarning("模型接口限流，{Delay} 秒后重试（第 {Attempt} 次）", delay, attempt);
                await Task.Delay(delay, ct);
                continue;
            }

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync(ct);

            try
            {
                using var document = JsonDocument.Parse(body);
                var content = document.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new InvalidOperationException("模型返回内容为空。");
                }

                return content;
            }
            catch (KeyNotFoundException)
            {
                throw new InvalidOperationException("模型返回结构不符合 OpenAI 兼容格式。");
            }
            catch (JsonException)
            {
                throw new InvalidOperationException("模型返回内容不是合法 JSON。");
            }
        }

        throw new InvalidOperationException("模型接口多次限流，已放弃本次请求。");
    }

    public void Dispose() => _httpClient.Dispose();
}
