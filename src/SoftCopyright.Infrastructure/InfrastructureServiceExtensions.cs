using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Domain.Options;
using SoftCopyright.Infrastructure.Data;
using SoftCopyright.Infrastructure.Jobs;
using SoftCopyright.Infrastructure.Security;
using SoftCopyright.Infrastructure.Storage;

namespace SoftCopyright.Infrastructure;

/// <summary>基础设施层注册。</summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>连接字符串环境变量名（MySQL 首选，便于不在仓库中保存密码）。</summary>
    public const string MySqlConnectionEnvironmentVariable = "SOFTCOPYRIGHT_MYSQL";

    /// <summary>数据库提供程序配置键。</summary>
    public const string ProviderConfigurationKey = "Database:Provider";

    public static IServiceCollection AddSoftCopyrightInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(o => OptionsBinder.Bind(o, configuration.GetSection(StorageOptions.SectionName)));
        services.Configure<ScanOptions>(o => OptionsBinder.Bind(o, configuration.GetSection(ScanOptions.SectionName)));
        services.Configure<MaterialRules>(o => OptionsBinder.Bind(o, configuration.GetSection(MaterialRules.SectionName)));
        services.Configure<AiOptions>(o => OptionsBinder.Bind(o, configuration.GetSection(AiOptions.SectionName)));
        services.Configure<GitImportOptions>(o => OptionsBinder.Bind(o, configuration.GetSection(GitImportOptions.SectionName)));

        services.AddDbContext<SoftCopyrightDbContext>(options => ConfigureDatabase(options, configuration));
        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddSingleton<IFileStore, LocalFileStore>();
        services.AddScoped<IBackgroundJobQueue, BackgroundJobQueue>();
        services.AddScoped<ISecretProtector, SecretProtector>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddHostedService<BackgroundJobWorker>();

        return services;
    }

    /// <summary>解析数据库提供程序：MySql（默认）/ Sqlite / PostgreSql。</summary>
    public static string ResolveProvider(IConfiguration configuration)
    {
        var provider = configuration[ProviderConfigurationKey];
        if (!string.IsNullOrWhiteSpace(provider))
        {
            return provider.Trim();
        }

        return string.IsNullOrWhiteSpace(ResolveMySqlConnectionString(configuration))
            ? "Sqlite"
            : "MySql";
    }

    /// <summary>解析 MySQL 连接字符串：优先环境变量，其次配置节。</summary>
    public static string? ResolveMySqlConnectionString(IConfiguration configuration)
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(MySqlConnectionEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        var fromConfiguration = configuration.GetConnectionString("MySql");
        return string.IsNullOrWhiteSpace(fromConfiguration) ? null : fromConfiguration;
    }

    private static void ConfigureDatabase(DbContextOptionsBuilder options, IConfiguration configuration)
    {
        var provider = ResolveProvider(configuration);

        switch (provider.ToLowerInvariant())
        {
            case "mysql":
            {
                var connection = ResolveMySqlConnectionString(configuration)
                                 ?? throw new InvalidOperationException(
                                     "已选择 MySQL，但未提供连接字符串。请设置环境变量 " + MySqlConnectionEnvironmentVariable +
                                     " 或配置 ConnectionStrings:MySql。");

                options.UseMySQL(connection);
                return;
            }

            case "postgresql":
            case "postgres":
            {
                var connection = configuration.GetConnectionString("PostgreSql")
                                 ?? throw new InvalidOperationException("已选择 PostgreSQL，但未配置连接字符串 ConnectionStrings:PostgreSql。");
                options.UseNpgsql(connection);
                return;
            }

            default:
            {
                var dataRoot = StoragePaths.ResolveDataRoot(configuration["Storage:DataRoot"]);
                Directory.CreateDirectory(dataRoot);
                var connectionString = configuration.GetConnectionString("Sqlite")
                                       ?? $"Data Source={Path.Combine(dataRoot, "softcopyright.db")}";
                options.UseSqlite(connectionString);
                return;
            }
        }
    }

    /// <summary>确保 MySQL 数据库存在（不存在时创建），供首次启动与自动化测试使用。</summary>
    public static async Task EnsureMySqlDatabaseAsync(string connectionString, CancellationToken ct = default)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString);
        var database = builder.Database;
        if (string.IsNullOrWhiteSpace(database))
        {
            return;
        }

        builder.Database = string.Empty;
        await using var connection = new MySqlConnection(builder.ConnectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE IF NOT EXISTS `{database.Replace("`", "``")}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
        await command.ExecuteNonQueryAsync(ct);
    }
}
