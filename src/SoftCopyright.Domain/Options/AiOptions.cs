namespace SoftCopyright.Domain.Options;

/// <summary>AI Provider 配置。密钥只允许通过环境变量或本地用户配置注入。</summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Provider 标识：rule-based / mock / http。</summary>
    public string Provider { get; set; } = "rule-based";

    /// <summary>OpenAI 兼容接口地址（http Provider 使用）。</summary>
    public string? BaseUrl { get; set; }

    /// <summary>模型名称。</summary>
    public string? Model { get; set; }

    /// <summary>API Key。仅从环境变量或用户本地配置读取，禁止写入仓库。</summary>
    public string? ApiKey { get; set; }

    /// <summary>环境变量名，默认 SOFTCOPYRIGHT_AI_APIKEY。</summary>
    public string ApiKeyEnvironmentVariable { get; set; } = "SOFTCOPYRIGHT_AI_APIKEY";

    /// <summary>请求超时（秒）。</summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>证据包最大条目数。</summary>
    public int MaxEvidenceItems { get; set; } = 60;

    /// <summary>证据包最大总字节数。</summary>
    public int MaxEvidenceBytes { get; set; } = 200 * 1024;

    /// <summary>单个证据文件最大字节数。</summary>
    public int MaxItemBytes { get; set; } = 8 * 1024;

    /// <summary>代码文件进入证据包时保留的最大行数。</summary>
    public int MaxCodeHeadLines { get; set; } = 40;
}
