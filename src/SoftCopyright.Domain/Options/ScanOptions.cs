namespace SoftCopyright.Domain.Options;

/// <summary>仓库扫描与安全限制配置。</summary>
public sealed class ScanOptions
{
    public const string SectionName = "Scan";

    /// <summary>压缩包最大字节数。</summary>
    public long MaxArchiveBytes { get; set; } = 50L * 1024 * 1024;

    /// <summary>压缩包条目总数上限。</summary>
    public int MaxEntryCount { get; set; } = 20_000;

    /// <summary>解压后总大小上限（字节）。</summary>
    public long MaxTotalExtractedBytes { get; set; } = 500L * 1024 * 1024;

    /// <summary>单文件大小上限（字节），超过则跳过该文件。</summary>
    public long MaxFileBytes { get; set; } = 20L * 1024 * 1024;

    /// <summary>相对路径最大层级。</summary>
    public int MaxPathDepth { get; set; } = 32;

    /// <summary>压缩比阈值（压缩后大于该字节数且超过该倍率即判定为炸弹）。</summary>
    public int MaxCompressionRatio { get; set; } = 100;

    /// <summary>触发压缩比检查的最小压缩字节数。</summary>
    public long CompressionRatioMinBytes { get; set; } = 1024 * 1024;

    /// <summary>文本嗅探时读取的最大字节数。</summary>
    public int TextSniffBytes { get; set; } = 8192;

    /// <summary>读取全文的最大字节数（超过视为大文件）。</summary>
    public long MaxReadableFileBytes { get; set; } = 2L * 1024 * 1024;

    /// <summary>可配置排除的目录名（用户可调整，但安全类排除不可取消）。</summary>
    public string[] DefaultExcludedDirectories { get; set; } =
    [
        ".git", ".hg", ".svn", "node_modules", "bin", "obj", "vendor", "dist", "build",
        "target", "__pycache__", ".venv", "venv", ".tox", ".mypy_cache", ".pytest_cache",
        "coverage", ".idea", ".vscode", ".cache", ".next", ".nuxt", ".gradle", "packages"
    ];

    /// <summary>可配置排除的文件名或后缀。</summary>
    public string[] DefaultExcludedFilePatterns { get; set; } =
    [
        "*.min.js", "*.min.css", "*.map", "*.lock", "*.log", "*.dll", "*.so", "*.dylib",
        "*.exe", "*.pdb", "*.class", "*.jar", "*.zip", "*.7z", "*.rar", "*.tar", "*.gz"
    ];
}
