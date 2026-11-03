namespace SoftCopyright.Infrastructure.Storage;

/// <summary>数据目录解析规则（LocalFileStore 与数据库连接串共用同一套逻辑）。</summary>
public static class StoragePaths
{
    public static string ResolveDataRoot(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = AppContext.BaseDirectory;
        }

        return Path.Combine(localAppData, "SoftCopyright");
    }
}
