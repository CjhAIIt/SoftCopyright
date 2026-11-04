using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MySql.Data.MySqlClient;
using SoftCopyright.Infrastructure;

namespace SoftCopyright.Tests.Support;

/// <summary>
/// 集成测试用宿主。
///
/// 默认使用 MySQL（读取环境变量 SOFTCOPYRIGHT_TEST_MYSQL 或 SOFTCOPYRIGHT_MYSQL），
/// 并为**每个宿主实例分配独立的测试库**：多个测试类并行执行时各自迁移自己的库，
/// 避免并发建表互相冲突。
/// 未配置 MySQL 连接信息时回退到 SQLite 临时库，保证离线环境仍可运行测试。
/// </summary>
public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    private string _dataRoot =
        Path.Combine(Path.GetTempPath(), "sc-api-test-" + Guid.NewGuid().ToString("N"));

    /// <summary>数据目录。测试中可覆盖，用于模拟应用重启后复用同一份数据。</summary>
    public string DataRoot
    {
        get => _dataRoot;
        set => _dataRoot = value;
    }

    /// <summary>释放时是否保留数据（模拟重启复用数据时应设为 false）。</summary>
    public bool PreserveDataOnDispose { get; set; }

    /// <summary>实际使用的数据库提供程序。</summary>
    public string Provider { get; private set; } = "Sqlite";

    /// <summary>本次测试使用的 MySQL 连接字符串（未使用 MySQL 时为 null）。可赋值以复用同一数据库。</summary>
    public string? MySqlConnectionString { get; set; }

    private string? _testDatabaseName;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["Storage:DataRoot"] = _dataRoot,
                ["App:ApiBaseAddress"] = "http://localhost",
                ["Ai:Provider"] = "rule-based",
                // 测试用本地 git 仓库验证导入通道。
                ["GitImport:AllowLocalFileUrls"] = "true",
                ["GitImport:TimeoutSeconds"] = "120"
            };

            var mysql = ResolveBaseMySqlConnection();
            if (!string.IsNullOrWhiteSpace(mysql))
            {
                MySqlConnectionString = mysql;
                Provider = "MySql";
                overrides[InfrastructureServiceExtensions.ProviderConfigurationKey] = "MySql";
                overrides["ConnectionStrings:MySql"] = mysql;
            }
            else
            {
                Provider = "Sqlite";
                overrides[InfrastructureServiceExtensions.ProviderConfigurationKey] = "Sqlite";
                overrides["ConnectionStrings:Sqlite"] = $"Data Source={Path.Combine(_dataRoot, "test.db")}";
            }

            configuration.AddInMemoryCollection(overrides);
        });
    }

    /// <summary>在应用启动前确保测试库存在。</summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        EnsureDatabaseAsync().GetAwaiter().GetResult();
        return base.CreateHost(builder);
    }

    /// <summary>确保测试数据库存在。</summary>
    public async Task EnsureDatabaseAsync()
    {
        if (Provider != "MySql" || string.IsNullOrWhiteSpace(MySqlConnectionString))
        {
            return;
        }

        await InfrastructureServiceExtensions.EnsureMySqlDatabaseAsync(MySqlConnectionString);
    }

    /// <summary>生成当前宿主专用的测试库连接串。</summary>
    private string? ResolveBaseMySqlConnection()
    {
        if (!string.IsNullOrWhiteSpace(MySqlConnectionString))
        {
            return MySqlConnectionString;
        }

        var raw = Environment.GetEnvironmentVariable("SOFTCOPYRIGHT_TEST_MYSQL")
                  ?? Environment.GetEnvironmentVariable(InfrastructureServiceExtensions.MySqlConnectionEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var builder = new MySqlConnectionStringBuilder(raw);
        var baseName = string.IsNullOrWhiteSpace(builder.Database) ? "softcopyright_test" : builder.Database;

        // 每个宿主实例一个独立库，避免并行测试互相迁移冲突。
        _testDatabaseName = $"{baseName}_{Guid.NewGuid():N}"[..48];
        builder.Database = _testDatabaseName;
        return builder.ConnectionString;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        if (!PreserveDataOnDispose)
        {
            try
            {
                if (Directory.Exists(_dataRoot))
                {
                    Directory.Delete(_dataRoot, recursive: true);
                }
            }
            catch (IOException)
            {
                // 忽略清理失败。
            }
        }

        DropTestDatabase();
    }

    private void DropTestDatabase()
    {
        if (PreserveDataOnDispose || string.IsNullOrWhiteSpace(_testDatabaseName) || string.IsNullOrWhiteSpace(MySqlConnectionString))
        {
            return;
        }

        try
        {
            var builder = new MySqlConnectionStringBuilder(MySqlConnectionString);
            var database = builder.Database;
            builder.Database = string.Empty;

            using var connection = new MySqlConnection(builder.ConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS `{database.Replace("`", "``")}`;";
            command.ExecuteNonQuery();
        }
        catch (MySqlException)
        {
            // 清理失败不影响测试结论。
        }
    }
}
