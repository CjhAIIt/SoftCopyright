namespace SoftCopyright.Domain.Options;

/// <summary>存储配置。</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>数据根目录。留空时使用 %LOCALAPPDATA%\SoftCopyright。</summary>
    public string? DataRoot { get; set; }

    /// <summary>是否允许在 Development 环境导入服务器本地目录。</summary>
    public bool AllowLocalDirectoryImport { get; set; }

    /// <summary>允许导入的本地目录根（仅 Development）。</summary>
    public string? LocalImportRoot { get; set; }
}
