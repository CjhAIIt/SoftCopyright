using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using SoftCopyright.Desktop.ViewModels;

namespace SoftCopyright.Desktop.Views;

/// <summary>桌面端主窗口：顶栏 + 侧栏步骤 + 内容区 + 状态栏。</summary>
public sealed partial class MainWindow : Window
{
    private readonly ShellViewModel _shell;

    public MainWindow(ShellViewModel shell)
    {
        InitializeComponent();

        _shell = shell;
        DataContext = shell;

        shell.PropertyChanged += OnShellPropertyChanged;

        // 文件对话框属于界面职责，通过委托注入到对应的视图模型。
        shell.Import.PickZipFile = PickZipFileAsync;
        shell.Materials.PickSavePath = PickSavePathAsync;

        UpdateStepStates();
        ShowStep(shell.CurrentStep);
        _ = shell.RefreshAiModeAsync();
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.CurrentStep))
        {
            UpdateStepStates();
            ShowStep(_shell.CurrentStep);
        }
    }

    private void UpdateStepStates()
    {
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
        var top = GetTopLevel(this);
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
        var top = GetTopLevel(this);
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
