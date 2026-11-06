using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftCopyright.Client.Services;
using SoftCopyright.Domain;

namespace SoftCopyright.Client.ViewModels;

/// <summary>桌面端的流程步骤。</summary>
public enum ShellStep
{
    Cases,
    Import,
    Analysis,
    Applicant,
    Code,
    Materials
}

/// <summary>侧栏步骤条目。</summary>
public sealed partial class StepItem : ObservableObject
{
    public required ShellStep Step { get; init; }

    public required string Title { get; init; }

    public required string Hint { get; init; }

    [ObservableProperty] private bool _isActive;

    [ObservableProperty] private bool _isDone;

    public string StatusText => IsDone ? "已完成" : IsActive ? "进行中" : "未开始";
}

/// <summary>可刷新的步骤视图模型。</summary>
public interface IRefreshable
{
    Task RefreshAsync();
}

/// <summary>
/// 桌面端外壳：持有引擎、当前案例与当前步骤，负责在步骤之间流转。
/// 各步骤的具体逻辑放在各自的视图模型里，共用同一个 <see cref="DesktopEngine" />。
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly DesktopEngine _engine;
    private readonly ThemeService _theme;

    public ShellViewModel(DesktopEngine engine, ThemeService theme)
    {
        _engine = engine;
        _theme = theme;

        Steps =
        [
            new StepItem { Step = ShellStep.Cases, Title = "案例", Hint = "新建与选择申请案例" },
            new StepItem { Step = ShellStep.Import, Title = "导入仓库", Hint = "ZIP 压缩包或 Git 仓库" },
            new StepItem { Step = ShellStep.Analysis, Title = "分析与证据", Hint = "项目画像与待确认问题" },
            new StepItem { Step = ShellStep.Applicant, Title = "申请信息", Hint = "按官方规定填写申请表" },
            new StepItem { Step = ShellStep.Code, Title = "代码材料", Hint = "勾选纳入材料的源文件" },
            new StepItem { Step = ShellStep.Materials, Title = "材料与导出", Hint = "校验、生成与导出材料包" }
        ];

        Cases = new CasesViewModel(this);
        Import = new ImportViewModel(this);
        Analysis = new AnalysisViewModel(this);
        Applicant = new ApplicantViewModel(this);
        Code = new CodeViewModel(this);
        Materials = new MaterialsViewModel(this);

        _currentStep = ShellStep.Cases;
        SyncSteps();
    }

    public ObservableCollection<StepItem> Steps { get; }

    /// <summary>业务引擎：各步骤共用同一份。</summary>
    public DesktopEngine Engine => _engine;

    public CasesViewModel Cases { get; }

    public ImportViewModel Import { get; }

    public AnalysisViewModel Analysis { get; }

    public ApplicantViewModel Applicant { get; }

    public CodeViewModel Code { get; }

    public MaterialsViewModel Materials { get; }

    [ObservableProperty] private ShellStep _currentStep = ShellStep.Cases;

    [ObservableProperty] private ApplicationCase? _currentCase;

    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private string _statusMessage = "就绪";

    [ObservableProperty] private string? _errorMessage;

    [ObservableProperty] private string _instanceName = "知识产权申请材料工具";

    /// <summary>窄屏下的短标题（手机顶栏放不下全称）。</summary>
    public string CompactInstanceName => "申请材料工具";

    [ObservableProperty] private string _aiMode = "离线（免费）";

    [ObservableProperty] private bool _isDarkTheme;

    [ObservableProperty] private string _aiBaseUrl = string.Empty;

    [ObservableProperty] private string _aiModel = string.Empty;

    [ObservableProperty] private string _aiApiKey = string.Empty;

    [ObservableProperty] private bool _isAiPanelOpen;

    /// <summary>窄屏（手机 / 小窗口）模式：侧栏改为底部导航，表单改为单列。</summary>
    [ObservableProperty] private bool _isCompact;

    /// <summary>当前步骤在流程中的序号（从 0 开始）。</summary>
    public int StepIndex => (int)CurrentStep;

    /// <summary>当前步骤名称。</summary>
    public string CurrentStepTitle => Steps[StepIndex].Title;

    /// <summary>是否还有上一步。</summary>
    public bool CanGoBack => StepIndex > 0;

    /// <summary>是否还有下一步。</summary>
    public bool CanGoNext => StepIndex < Steps.Count - 1;

    public bool HasCase => CurrentCase is not null;

    /// <summary>是否有错误提示。</summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public string CaseDisplayName =>
        CurrentCase?.SoftwareFullName is { Length: > 0 } full ? full
        : CurrentCase?.Name is { Length: > 0 } name ? name
        : "未选择案例";

    public string DataRoot => _engine.DataRoot;

    /// <summary>当前步骤是否可进入（没有案例时只能停留在案例列表）。</summary>
    public bool CanEnterStep(ShellStep step) =>
        step == ShellStep.Cases || HasCase;

    /// <summary>切换步骤。</summary>
    [RelayCommand]
    public async Task GoToAsync(ShellStep step)
    {
        if (!CanEnterStep(step))
        {
            ErrorMessage = "请先选择一个案例。";
            return;
        }

        ErrorMessage = null;
        CurrentStep = step;
        SyncSteps();

        object? target = step switch
        {
            ShellStep.Cases => Cases,
            ShellStep.Import => Import,
            ShellStep.Analysis => Analysis,
            ShellStep.Applicant => Applicant,
            ShellStep.Code => Code,
            ShellStep.Materials => Materials,
            _ => null
        };

        if (target is IRefreshable refreshable)
        {
            await RunBusyAsync(async () => await refreshable.RefreshAsync());
        }
    }

    /// <summary>切换主题。</summary>
    [RelayCommand]
    private void ToggleTheme()
    {
        _theme.Toggle();
        IsDarkTheme = _theme.IsDark;
    }

    /// <summary>上一步（窄屏底部的流程导航）。</summary>
    [RelayCommand]
    private async Task PreviousAsync()
    {
        if (!CanGoBack)
        {
            return;
        }

        await GoToAsync(Steps[StepIndex - 1].Step);
    }

    /// <summary>下一步（窄屏底部的流程导航）。</summary>
    [RelayCommand]
    private async Task NextAsync()
    {
        if (!CanGoNext)
        {
            return;
        }

        await GoToAsync(Steps[StepIndex + 1].Step);
    }

    /// <summary>打开本机数据目录。</summary>
    [RelayCommand]
    private void OpenDataFolder()
    {
        try
        {
            Directory.CreateDirectory(DataRoot);
            OsLauncher.OpenFolder(DataRoot);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"无法打开数据目录：{ex.Message}";
        }
    }

    /// <summary>展开或收起模型接入设置。</summary>
    [RelayCommand]
    private void ToggleAiPanel() => IsAiPanelOpen = !IsAiPanelOpen;

    /// <summary>保存模型接入设置（密钥加密后存本机数据库）。</summary>
    [RelayCommand]
    private async Task SaveAiSettingsAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(AiBaseUrl) || string.IsNullOrWhiteSpace(AiModel) || string.IsNullOrWhiteSpace(AiApiKey))
            {
                throw new InvalidOperationException("Base URL、模型名称与 API 密钥三项都必须填写。");
            }

            await _engine.SaveAiSettingsAsync(AiBaseUrl, AiModel, AiApiKey, CancellationToken.None);
            await RefreshAiModeAsync();
            StatusMessage = "模型接入设置已保存到本机（密钥已加密存储）。";
        });
    }

    /// <summary>改为完全离线模式。</summary>
    [RelayCommand]
    private async Task DisableAiAsync()
    {
        await RunBusyAsync(async () =>
        {
            await _engine.DisableAiAsync(CancellationToken.None);
            AiBaseUrl = string.Empty;
            AiModel = string.Empty;
            AiApiKey = string.Empty;
            await RefreshAiModeAsync();
            StatusMessage = "已切换为完全离线模式，不会产生任何外部请求。";
        });
    }

    /// <summary>载入当前案例并刷新界面状态。</summary>
    public void SetCase(ApplicationCase? caseEntity)
    {
        CurrentCase = caseEntity;
        OnPropertyChanged(nameof(HasCase));
        OnPropertyChanged(nameof(CaseDisplayName));
        SyncSteps();
    }

    /// <summary>刷新顶栏的模型接入状态。</summary>
    public async Task RefreshAiModeAsync()
    {
        var account = await _engine.GetAccountAsync(CancellationToken.None);
        AiMode = account is { HasAiConfiguration: true } ? "已配置模型密钥" : "离线（免费）";
    }

    /// <summary>统一的忙碌包装：捕获异常并展示中文提示。</summary>
    public async Task RunBusyAsync(Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SyncSteps()
    {
        foreach (var step in Steps)
        {
            var active = step.Step == CurrentStep;
            if (step.IsActive != active)
            {
                step.IsActive = active;
            }
        }
    }

    partial void OnCurrentStepChanged(ShellStep value)
    {
        SyncSteps();
        OnPropertyChanged(nameof(StepIndex));
        OnPropertyChanged(nameof(CurrentStepTitle));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoNext));
    }

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    partial void OnCurrentCaseChanged(ApplicationCase? value)
    {
        OnPropertyChanged(nameof(HasCase));
        OnPropertyChanged(nameof(CaseDisplayName));
        SyncSteps();
    }
}
