using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using SoftCopyright.Client.ViewModels;

namespace SoftCopyright.Client.Views;

/// <summary>
/// 应用外壳：顶栏 + 侧栏（或底部导航）+ 内容区 + 状态栏。
///
/// 桌面端由 <see cref="MainWindow" /> 承载它，手机端则整屏显示它——
/// 这样两种形态共用同一套界面与视图模型，只有外壳不同。
/// </summary>
public sealed partial class ShellView : UserControl
{
    private ShellViewModel? _shell;

    public ShellView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
    }

    public ShellView(ShellViewModel shell) : this()
    {
        DataContext = shell;
    }

    private void Attach()
    {
        if (DataContext is not ShellViewModel shell || ReferenceEquals(_shell, shell))
        {
            return;
        }

        _shell = shell;
        shell.PropertyChanged += OnShellPropertyChanged;

        // 文件对话框属于宿主职责，这里把实现注入给用到它的视图模型。
        shell.Import.PickZipFile = PickZipFileAsync;
        shell.Materials.PickSavePath = PickSavePathAsync;

        UpdateStepStates();
        ShowStep(shell.CurrentStep);
        _ = shell.RefreshAiModeAsync();

        Loaded += (_, _) => UpdateCompact();
        SizeChanged += (_, _) => UpdateCompact();
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.CurrentStep))
        {
            UpdateStepStates();
            ShowStep(_shell!.CurrentStep);
        }
    }

    /// <summary>窄屏（手机 / 小窗口）判定：侧栏收起，改为底部流程导航。</summary>
    private void UpdateCompact()
    {
        if (_shell is null)
        {
            return;
        }

        var width = Bounds.Width > 0 ? Bounds.Width : Width;
        var compact = width > 0 && width < 900;

        if (_shell.IsCompact != compact)
        {
            _shell.IsCompact = compact;
        }

        StepContent.Margin = new Thickness(compact ? 12 : 22);
    }

    private void UpdateStepStates()
    {
        if (_shell is null)
        {
            return;
        }

        ApplyStepState(CasesStep, _shell.Steps[0]);
        ApplyStepState(ImportStep, _shell.Steps[1]);
        ApplyStepState(AnalysisStep, _shell.Steps[2]);
        ApplyStepState(ApplicantStep, _shell.Steps[3]);
        ApplyStepState(CodeStep, _shell.Steps[4]);
        ApplyStepState(MaterialsStep, _shell.Steps[5]);
    }

    private static void ApplyStepState(Button button, StepItem step)
    {
        button.Classes.Set("active", step.IsActive);
    }

    private void ShowStep(ShellStep step)
    {
        if (_shell is null)
        {
            return;
        }

        StepContent.Content = step switch
        {
            ShellStep.Cases => new CasesView { DataContext = _shell.Cases },
            ShellStep.Import => new ImportView { DataContext = _shell.Import },
            ShellStep.Analysis => new AnalysisView { DataContext = _shell.Analysis },
            ShellStep.Applicant => new ApplicantView { DataContext = _shell.Applicant },
            ShellStep.Code => new CodeView { DataContext = _shell.Code },
            ShellStep.Materials => new MaterialsView { DataContext = _shell.Materials },
            _ => null
        };
    }

    private async Task<string?> PickZipFileAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return null;
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择代码仓库 ZIP 压缩包",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("ZIP 压缩包") { Patterns = ["*.zip"] }
            ]
        });

        if (files.Count == 0)
        {
            return null;
        }

        return files[0].TryGetLocalPath() ?? files[0].Path.LocalPath;
    }

    private async Task<string?> PickSavePathAsync(string suggestedPath)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return null;
        }

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出材料产物",
            SuggestedFileName = Path.GetFileName(suggestedPath)
        });

        return file?.TryGetLocalPath() ?? file?.Path.LocalPath;
    }
}
