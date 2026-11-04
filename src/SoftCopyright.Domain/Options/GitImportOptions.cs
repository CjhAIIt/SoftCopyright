namespace SoftCopyright.Domain.Options;

/// <summary>Git 仓库导入配置。</summary>
public sealed class GitImportOptions
{
    public const string SectionName = "GitImport";

    /// <summary>允许克隆的仓库域名白名单（不含协议与端口）。</summary>
    public string[] AllowedHosts { get; set; } =
    [
        "github.com",
        "gitee.com",
        "gitlab.com"
    ];

    /// <summary>是否允许任意域名（自建 GitLab 等场景）。</summary>
    public bool AllowAnyHost { get; set; }

    /// <summary>是否允许 file:// 与本地路径（仅本机仓库与自动化测试使用）。</summary>
    public bool AllowLocalFileUrls { get; set; }

    /// <summary>克隆超时（秒）。</summary>
    public int TimeoutSeconds { get; set; } = 600;

    /// <summary>是否浅克隆（只取最新一次提交，大幅降低流量与耗时）。</summary>
    public bool Shallow { get; set; } = true;

    /// <summary>克隆完成后目录大小上限（MB），超过即判定失败并清理。</summary>
    public int MaxWorkingTreeSizeMb { get; set; } = 512;

    /// <summary>访问令牌环境变量名（私有仓库用，仅在配置了令牌时才注入）。</summary>
    public string? AccessTokenEnvironmentVariable { get; set; } = "SOFTCOPYRIGHT_GIT_TOKEN";

    /// <summary>git 可执行文件路径，留空则按 PATH 查找。</summary>
    public string? GitExecutablePath { get; set; }
}
