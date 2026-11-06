using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftCopyright.Desktop.Services;
using SoftCopyright.Domain;

namespace SoftCopyright.Desktop.ViewModels;

/// <summary>导入仓库步骤：从本机 ZIP 压缩包或 Git 仓库地址导入并扫描。</summary>
public sealed partial class ImportViewModel : ObservableObject, IRefreshable
{
    private readonly ShellViewModel _shell;
    private readonly DesktopEngine _engine;

    public ImportViewModel(ShellViewModel shell)
    {
        _shell = shell;
        _engine = shell.Engine;
    }

    /// <summary>由视图注入的 ZIP 文件选择器（文件对话框属于界面职责）。</summary>
    public Func<Task<string?>>? PickZipFile { get; set; }

    [ObservableProperty] private string _selectedZipPath = string.Empty;

    [ObservableProperty] private string _gitUrl = string.Empty;

    [ObservableProperty] private string _gitBranch = string.Empty;

    [ObservableProperty] private string _message = string.Empty;

    [ObservableProperty] private string _summary = string.Empty;

    /// <summary>是否已有导入结果可展示。</summary>
    public bool HasMessage => !string.IsNullOrWhiteSpace(Message) || !string.IsNullOrWhiteSpace(Summary);

    /// <summary>选择本机 ZIP 压缩包。</summary>
    [RelayCommand]
    private async Task ChooseZipAsync()
    {
        if (PickZipFile is null)
        {
            return;
        }

        var path = await PickZipFile();
        if (!string.IsNullOrWhiteSpace(path))
        {
            SelectedZipPath = path;
            Message = string.Empty;
        }
    }

    /// <summary>导入 ZIP 并立即扫描。</summary>
    [RelayCommand]
    private async Task ImportZipAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedZipPath) || !File.Exists(SelectedZipPath))
        {
            _shell.ErrorMessage = "请先选择一个存在的 ZIP 压缩包。";
            return;
        }

        await _shell.RunBusyAsync(async () =>
        {
            var caseId = await RequireCaseAsync();
            var upload = await _engine.ImportZipAsync(caseId, SelectedZipPath, CancellationToken.None);
            Message = $"已导入 {upload.FileName}：{upload.EntryCount} 个条目，解压后 {upload.ExtractedFileCount} 个文件。";
            await RunScanAsync(caseId);
        });
    }

    /// <summary>从 Git 仓库导入并立即扫描。</summary>
    [RelayCommand]
    private async Task ImportGitAsync()
    {
        if (string.IsNullOrWhiteSpace(GitUrl))
        {
            _shell.ErrorMessage = "请填写 Git 仓库地址。";
            return;
        }

        await _shell.RunBusyAsync(async () =>
        {
            var caseId = await RequireCaseAsync();
            var summary = await _engine.ImportGitAsync(
                caseId,
                GitUrl.Trim(),
                string.IsNullOrWhiteSpace(GitBranch) ? null : GitBranch.Trim(),
                null,
                CancellationToken.None);

            Message = $"已从 {summary.Url} 导入 {summary.FileCount} 个文件" +
                      (string.IsNullOrWhiteSpace(summary.Commit) ? string.Empty : $"，提交 {summary.Commit[..Math.Min(8, summary.Commit.Length)]}");

            foreach (var warning in summary.Warnings)
            {
                _shell.StatusMessage = warning;
            }

            await RunScanAsync(caseId);
        });
    }

    private async Task<Guid> RequireCaseAsync()
    {
        if (_shell.CurrentCase is null)
        {
            throw new InvalidOperationException("请先在「案例」步骤中选择或新建一个案例。");
        }

        return _shell.CurrentCase.Id;
    }

    private async Task RunScanAsync(Guid caseId)
    {
        _shell.StatusMessage = "正在扫描仓库……";
        var scan = await _engine.ScanAsync(caseId, CancellationToken.None);

        _shell.StatusMessage = "正在整理项目画像……";
        await _engine.AnalyzeAsync(caseId, CancellationToken.None);

        Summary = $"共 {scan.TotalFiles} 个文件（纳入 {scan.IncludedFiles} 个，排除 {scan.ExcludedFiles} 个），" +
                  $"代码候选 {scan.CodeCandidates} 个，证据 {scan.EvidenceCount} 条" +
                  (scan.SuspectedSecretCount > 0 ? $"，疑似凭据文件 {scan.SuspectedSecretCount} 个已排除" : string.Empty) +
                  "。";

        var entity = await _engine.GetCaseAsync(caseId, CancellationToken.None);
        if (entity is not null)
        {
            _shell.SetCase(entity);
        }

        _shell.StatusMessage = "扫描完成。";
        await _shell.GoToAsync(ShellStep.Analysis);
    }

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    partial void OnSummaryChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    public Task RefreshAsync() => Task.CompletedTask;
}
