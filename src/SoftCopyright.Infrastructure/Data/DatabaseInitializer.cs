using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SoftCopyright.Infrastructure.Data;

/// <summary>
/// 数据库初始化：使用 EF Core 迁移演进表结构。
///
/// 早期版本使用 EnsureCreated，它**不会演进已有库的表结构**——新增表或列会直接导致
/// "Unknown column / table doesn't exist" 运行时错误。改用迁移后，结构变更由迁移脚本负责，
/// 升级不需要删库。
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        SoftCopyrightDbContext db,
        ILogger logger,
        CancellationToken ct = default)
    {
        try
        {
            await db.Database.MigrateAsync(ct);
            logger.LogInformation("数据库结构已升级到最新版本（提供程序 {Provider}）", db.Database.ProviderName);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "数据库迁移失败：{Message}。请确认数据库为全新创建，或删除旧库后重启（数据目录：{DataRoot}）",
                ex.Message,
                db.Database.GetConnectionString());
        }
    }
}
