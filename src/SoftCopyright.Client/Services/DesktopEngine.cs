using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SoftCopyright.AI;
using SoftCopyright.Application;
using SoftCopyright.Application.Contracts;
using SoftCopyright.Application.Dtos;
using SoftCopyright.Application.Services;
using SoftCopyright.Documents;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Infrastructure;
using SoftCopyright.Infrastructure.Data;
using SoftCopyright.Infrastructure.Storage;
using SoftCopyright.ProjectScan;

namespace SoftCopyright.Client.Services;

/// <summary>
/// 桌面端引擎宿主。
///
/// 端侧离线：把现有业务引擎（Domain / Application / Infrastructure / AI / Documents / ProjectScan）
/// 原样装进本机进程，案例、申请信息与材料全部落在本机，不需要任何服务器。
///
/// 复用方式与 Web 端完全一致——同一批用例服务、同一套校验规则、同一份材料生成器，
/// 差别只在于宿主：这里没有 HTTP，没有 Cookie，也没有前端远程调用。
/// </summary>
public sealed class DesktopEngine : IAsyncDisposable
{
    /// <summary>数据目录环境变量名。</summary>
    public const string DataRootEnvironmentVariable = "SOFTCOPYRIGHT_DESKTOP_DATA_ROOT";

    private readonly IHost _host;
    private readonly IServiceScope _scope;
    private readonly ILogger _logger;

    private DesktopEngine(IHost host, IServiceScope scope, Guid localUserId, ILogger logger, string dataRoot)
    {
        _host = host;
        _scope = scope;
        _logger = logger;
        LocalUserId = localUserId;
        DataRoot = dataRoot;
    }

    /// <summary>本机数据目录（Windows 为 %LOCALAPPDATA%\SoftCopyright）。</summary>
    public string DataRoot { get; }

    /// <summary>本机账号 ID：桌面端是单用户应用，案例全部归属该账号。</summary>
    public Guid LocalUserId { get; }

    /// <summary>应用内使用的服务作用域。</summary>
    public IServiceProvider Services => _scope.ServiceProvider;

    /// <summary>启动引擎：加载配置、初始化本机数据库、准备本机账号。</summary>
    /// <param name="args">命令行参数。</param>
    /// <param name="dataRoot">
    /// 数据目录。默认取环境变量 <c>SOFTCOPYRIGHT_DESKTOP_DATA_ROOT</c>，
    /// 未设置时用系统默认位置（Windows 为 %LOCALAPPDATA%\SoftCopyright）。
    /// </param>
    /// <param name="ct">取消令牌。</param>
    public static async Task<DesktopEngine> StartAsync(
        string[] args,
        string? dataRoot = null,
        CancellationToken ct = default)
    {
        var resolvedRoot = ResolveDataRoot(dataRoot);

        var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
            Args = args
        });

        builder.Configuration.Sources.Clear();
        builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
        builder.Configuration.AddEnvironmentVariables();

        // 数据目录固定下来：数据库、案例文件、产物与密钥文件都在这里。
        builder.Configuration["Storage:DataRoot"] = resolvedRoot;

        // 端侧离线默认使用本机 SQLite；显式配置了 MySQL 连接串时沿用共享数据库。
        var mySql = Environment.GetEnvironmentVariable(InfrastructureServiceExtensions.MySqlConnectionEnvironmentVariable);
        builder.Configuration["Database:Provider"] =
            string.IsNullOrWhiteSpace(mySql) ? "Sqlite" : "MySql";

        builder.Services.AddSoftCopyrightApplication();
        builder.Services.AddSoftCopyrightProjectScan();
        builder.Services.AddSoftCopyrightAi();
        builder.Services.AddSoftCopyrightDocuments();
        builder.Services.AddSoftCopyrightInfrastructure(builder.Configuration);

        var host = builder.Build();

        // 先把本机数据库建好（迁移），再启动后台任务执行器——顺序反了会出现
        // "no such table: BackgroundJobs"。这里只建宿主，不 Start。
        var scope = host.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Desktop");

        var db = scope.ServiceProvider.GetRequiredService<SoftCopyrightDbContext>();
        await DatabaseInitializer.InitializeAsync(db, logger, ct);

        // 桌面端没有登录页：本机账号由内置管理员承担，案例全部归属它。
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountService>();
        await accounts.EnsureAdminAccountAsync(ct);
        var admin = await accounts.GetByUserNameAsync(AccountService.AdminUserName, ct);
        var localUserId = admin?.Id ?? Guid.Empty;

        scope.ServiceProvider.GetRequiredService<ICurrentAccountContext>().Use(localUserId);

        await host.StartAsync(ct);

        logger.LogInformation("桌面端引擎已就绪，数据目录：{DataRoot}", resolvedRoot);

        return new DesktopEngine(host, scope, localUserId, logger, resolvedRoot);
    }

    /// <summary>数据目录解析：显式参数 &gt; 环境变量 &gt; 系统默认位置。</summary>
    public static string ResolveDataRoot(string? dataRoot)
    {
        var configured = dataRoot;
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = Environment.GetEnvironmentVariable(DataRootEnvironmentVariable);
        }

        return StoragePaths.ResolveDataRoot(configured);
    }

    // ── 案例 ──────────────────────────────────────────────────────────────

    /// <summary>按材料类型列出本机案例。</summary>
    public async Task<IReadOnlyList<ApplicationCase>> ListCasesAsync(MaterialKind kind, CancellationToken ct)
    {
        var cases = Services.GetRequiredService<ICaseService>();
        return await cases.ListAsync(kind, ct);
    }

    /// <summary>新建案例。</summary>
    public async Task<ApplicationCase> CreateCaseAsync(string? name, MaterialKind kind, CancellationToken ct)
    {
        var cases = Services.GetRequiredService<ICaseService>();
        return await cases.CreateAsync(name, kind, ct);
    }

    /// <summary>取案例。</summary>
    public async Task<ApplicationCase?> GetCaseAsync(Guid caseId, CancellationToken ct)
    {
        var cases = Services.GetRequiredService<ICaseService>();
        return await cases.GetAsync(caseId, ct);
    }

    /// <summary>删除案例。</summary>
    public async Task<bool> DeleteCaseAsync(Guid caseId, CancellationToken ct)
    {
        var cases = Services.GetRequiredService<ICaseService>();
        return await cases.DeleteAsync(caseId, ct);
    }

    // ── 导入与扫描 ─────────────────────────────────────────────────────────

    /// <summary>导入本机 ZIP 压缩包（只读静态解析，不执行仓库中的任何脚本）。</summary>
    public async Task<UploadSummary> ImportZipAsync(Guid caseId, string filePath, CancellationToken ct)
    {
        var upload = Services.GetRequiredService<ICaseUploadService>();
        await using var stream = File.OpenRead(filePath);
        return await upload.UploadAsync(caseId, Path.GetFileName(filePath), stream, ct);
    }

    /// <summary>从 Git 仓库地址导入（只调用 git 自身，禁用钩子与 LFS 过滤器）。</summary>
    public async Task<GitImportSummary> ImportGitAsync(
        Guid caseId,
        string repositoryUrl,
        string? branch = null,
        string? commit = null,
        CancellationToken ct = default)
    {
        var git = Services.GetRequiredService<IGitImportService>();
        return await git.ImportAsync(caseId, new GitImportRequest(repositoryUrl, branch, commit), ct);
    }

    /// <summary>扫描已导入的仓库。</summary>
    public async Task<ScanSummary> ScanAsync(Guid caseId, CancellationToken ct)
    {
        var scan = Services.GetRequiredService<ICaseScanWorkflow>();
        return await scan.ScanAsync(caseId, null, ct);
    }

    /// <summary>模型增强分析（未配置模型时自动走离线规则实现）。</summary>
    public async Task<ScanSummary> AnalyzeAsync(Guid caseId, CancellationToken ct)
    {
        var analyze = Services.GetRequiredService<ICaseAnalyzeWorkflow>();
        return await analyze.AnalyzeAsync(caseId, null, ct);
    }

    // ── 申请信息 ──────────────────────────────────────────────────────────

    /// <summary>取项目分析结果（画像、覆盖情况、待确认问题与证据）。</summary>
    public async Task<AnalysisDto?> GetAnalysisAsync(Guid caseId, CancellationToken ct)
    {
        var analyses = Services.GetRequiredService<IRepository<ProjectAnalysis>>();
        var evidences = Services.GetRequiredService<IRepository<Evidence>>();

        var analysis = (await analyses.ListAsync(a => a.CaseId == caseId, ct)).FirstOrDefault();
        if (analysis is null)
        {
            return null;
        }

        var caseEvidences = await evidences.ListAsync(e => e.CaseId == caseId, ct);
        return AnalysisMapper.ToDto(analysis, caseEvidences);
    }

    /// <summary>取申请信息。</summary>
    public async Task<ApplicantInfoDto?> GetApplicantAsync(Guid caseId, CancellationToken ct)
    {
        var service = Services.GetRequiredService<IApplicantInfoService>();
        var info = await service.GetAsync(caseId, ct);
        if (info is null)
        {
            return null;
        }

        var confirmed = await IsConfirmedAsync(caseId, ConfirmationStage.ApplicantInfo, ct);
        return ((ApplicantInfoService)service).ToDto(info, confirmed);
    }

    /// <summary>保存申请信息。</summary>
    public async Task<ApplicantInfoDto> SaveApplicantAsync(
        Guid caseId,
        UpdateApplicantInfoRequest request,
        CancellationToken ct)
    {
        var service = Services.GetRequiredService<IApplicantInfoService>();
        var info = await service.SaveAsync(caseId, request, ct);
        return ((ApplicantInfoService)service).ToDto(info, false);
    }

    // ── 代码范围 ──────────────────────────────────────────────────────────

    /// <summary>列出代码候选文件。</summary>
    public async Task<IReadOnlyList<CodeCandidateDto>> ListCodeCandidatesAsync(Guid caseId, CancellationToken ct)
    {
        var selection = Services.GetRequiredService<ICodeSelectionService>();
        var candidates = await selection.ListCandidatesAsync(caseId, ct);
        var selected = (await selection.GetSelectedPathsAsync(caseId, ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return candidates
            .Where(c => c.IsCodeCandidate || c.Category == FileCategory.Code)
            .Select(c => CodeSelectionService.ToDto(c, selected.Contains(c.RelativePath)))
            .OrderBy(c => c.RelativePath, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>保存代码范围。</summary>
    public async Task SaveCodeSelectionAsync(Guid caseId, IReadOnlyList<string> paths, CancellationToken ct)
    {
        var selection = Services.GetRequiredService<ICodeSelectionService>();
        await selection.SaveSelectionAsync(caseId, paths, ct);
    }

    /// <summary>预览源文件内容。</summary>
    public async Task<string?> PreviewAsync(Guid caseId, string relativePath, int maxLines, CancellationToken ct)
    {
        var selection = Services.GetRequiredService<ICodeSelectionService>();
        return await selection.PreviewAsync(caseId, relativePath, maxLines, ct);
    }

    // ── 确认、校验与生成 ───────────────────────────────────────────────────

    /// <summary>确认某个阶段。</summary>
    public async Task<ConfirmationStatus> ConfirmAsync(Guid caseId, ConfirmationStage stage, string note, CancellationToken ct)
    {
        var confirmations = Services.GetRequiredService<IConfirmationService>();
        return await confirmations.ConfirmAsync(caseId, stage, note, ct);
    }

    /// <summary>列出各阶段的确认状态。</summary>
    public async Task<IReadOnlyList<ConfirmationStatus>> ListConfirmationsAsync(Guid caseId, CancellationToken ct)
    {
        var confirmations = Services.GetRequiredService<IConfirmationService>();
        return await confirmations.ListAsync(caseId, ct);
    }

    /// <summary>执行校验。</summary>
    public async Task<IReadOnlyList<ValidationResult>> ValidateAsync(Guid caseId, CancellationToken ct)
    {
        var validation = Services.GetRequiredService<IValidationService>();
        return await validation.ValidateAsync(caseId, ct, includeArtifactCheck: false);
    }

    /// <summary>生成材料。</summary>
    public async Task<GenerationSummary> GenerateAsync(Guid caseId, CancellationToken ct)
    {
        var generation = Services.GetRequiredService<ICaseGenerationWorkflow>();
        return await generation.GenerateAsync(caseId, null, ct);
    }

    /// <summary>列出产物。</summary>
    public async Task<IReadOnlyList<GeneratedArtifact>> ListArtifactsAsync(Guid caseId, CancellationToken ct)
    {
        var artifacts = Services.GetRequiredService<IRepository<GeneratedArtifact>>();
        return await artifacts.ListAsync(a => a.CaseId == caseId, ct);
    }

    /// <summary>读取产物内容。</summary>
    public async Task<byte[]?> ReadArtifactAsync(Guid artifactId, CancellationToken ct)
    {
        var artifacts = Services.GetRequiredService<IRepository<GeneratedArtifact>>();
        var store = Services.GetRequiredService<IFileStore>();

        var artifact = await artifacts.FindAsync(artifactId, ct);
        if (artifact is null || artifact.IsStale)
        {
            return null;
        }

        string path;
        try
        {
            path = store.ResolveSafePath(artifact.RelativePath);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        return File.Exists(path) ? await File.ReadAllBytesAsync(path, ct) : null;
    }

    // ── 账号与模型接入 ─────────────────────────────────────────────────────

    /// <summary>取本机账号信息。</summary>
    public async Task<AccountProfile?> GetAccountAsync(CancellationToken ct)
    {
        var accounts = Services.GetRequiredService<IAccountService>();
        return await accounts.GetAsync(LocalUserId, ct);
    }

    /// <summary>保存模型接入设置（密钥加密后存本机数据库）。</summary>
    public async Task SaveAiSettingsAsync(string? baseUrl, string? model, string? apiKey, CancellationToken ct)
    {
        var accounts = Services.GetRequiredService<IAccountService>();
        await accounts.SaveAiSettingsAsync(LocalUserId, baseUrl, model, apiKey, ct);
    }

    /// <summary>改为完全离线模式。</summary>
    public async Task DisableAiAsync(CancellationToken ct)
    {
        var accounts = Services.GetRequiredService<IAccountService>();
        await accounts.DisableAiAssistantAsync(LocalUserId, ct);
    }

    /// <summary>当前生效的模型接入设置；未启用时返回 null。</summary>
    public async Task<AiAccountSettings?> GetAiSettingsAsync(CancellationToken ct)
    {
        var provider = Services.GetRequiredService<IAiSettingsProvider>();
        return await provider.GetCurrentAsync(ct);
    }

    /// <summary>写日志。</summary>
    public void LogInformation(string message) => _logger.LogInformation("{Message}", message);

    private async Task<bool> IsConfirmedAsync(Guid caseId, ConfirmationStage stage, CancellationToken ct)
    {
        var confirmations = Services.GetRequiredService<IConfirmationService>();
        var list = await confirmations.ListAsync(caseId, ct);
        var status = list.FirstOrDefault(s => s.Stage == stage);
        return status is not null && status.Confirmed && !status.IsStale;
    }

    /// <summary>关闭引擎：停止后台任务并释放数据库连接。</summary>
    public async ValueTask DisposeAsync()
    {
        _scope.Dispose();
        await _host.StopAsync(TimeSpan.FromSeconds(5));
        _host.Dispose();
    }
}
