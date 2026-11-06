using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftCopyright.Application.Dtos;
using SoftCopyright.Client.Services;
using SoftCopyright.Domain;

namespace SoftCopyright.Client.ViewModels;

/// <summary>代码候选行（可勾选）。</summary>
public sealed partial class CodeRow : ObservableObject
{
    public required string RelativePath { get; init; }

    public required string Sha256 { get; init; }

    public required string Category { get; init; }

    public string? Language { get; init; }

    public int? LineCount { get; init; }

    [ObservableProperty] private bool _isSelected;

    public ICommand? PreviewCommand { get; init; }

    public string LinesText => LineCount is null ? string.Empty : $"{LineCount} 行";

    public string ShortHash => Sha256.Length >= 12 ? Sha256[..12] : Sha256;
}

/// <summary>代码材料步骤：勾选纳入材料包的源文件。</summary>
public sealed partial class CodeViewModel : ObservableObject, IRefreshable
{
    private readonly ShellViewModel _shell;
    private readonly DesktopEngine _engine;

    public CodeViewModel(ShellViewModel shell)
    {
        _shell = shell;
        _engine = shell.Engine;
    }

    public ObservableCollection<CodeRow> Candidates { get; } = [];

    [ObservableProperty] private string _filter = string.Empty;

    [ObservableProperty] private string _preview = string.Empty;

    [ObservableProperty] private string _previewPath = string.Empty;

    [ObservableProperty] private string _note = string.Empty;

    [ObservableProperty] private bool _isConfirmed;

    /// <summary>当前筛选后可见的候选文件。</summary>
    public IEnumerable<CodeRow> VisibleCandidates =>
        string.IsNullOrWhiteSpace(Filter)
            ? Candidates
            : Candidates.Where(c => c.RelativePath.Contains(Filter, StringComparison.OrdinalIgnoreCase));

    public int SelectedCount => Candidates.Count(c => c.IsSelected);

    public string SelectedCountText => $"{SelectedCount} 个文件已勾选";

    /// <summary>保存代码范围并确认。</summary>
    [RelayCommand]
    private async Task SaveAndConfirmAsync()
    {
        await _shell.RunBusyAsync(async () =>
        {
            if (_shell.CurrentCase is null)
            {
                throw new InvalidOperationException("请先选择案例。");
            }

            var caseId = _shell.CurrentCase.Id;
            var paths = Candidates.Where(c => c.IsSelected).Select(c => c.RelativePath).ToList();
            await _engine.SaveCodeSelectionAsync(caseId, paths, CancellationToken.None);

            await _engine.ConfirmAsync(
                caseId,
                ConfirmationStage.CodeScope,
                string.IsNullOrWhiteSpace(Note) ? "在桌面端确认代码范围" : Note,
                CancellationToken.None);

            _shell.StatusMessage = $"已确认 {paths.Count} 个源文件纳入材料。";
            await RefreshAsync();
            await _shell.GoToAsync(ShellStep.Materials);
        });
    }

    /// <summary>预览源文件内容。</summary>
    [RelayCommand]
    private async Task PreviewAsync(CodeRow row)
    {
        if (_shell.CurrentCase is null)
        {
            return;
        }

        var content = await _engine.PreviewAsync(_shell.CurrentCase.Id, row.RelativePath, 200, CancellationToken.None);
        PreviewPath = row.RelativePath;
        Preview = content ?? "（无法读取该文件内容）";
    }

    public async Task RefreshAsync()
    {
        Candidates.Clear();
        Preview = string.Empty;
        PreviewPath = string.Empty;

        if (_shell.CurrentCase is null)
        {
            return;
        }

        var candidates = await _engine.ListCodeCandidatesAsync(_shell.CurrentCase.Id, CancellationToken.None);
        foreach (var candidate in candidates)
        {
            var row = new CodeRow
            {
                RelativePath = candidate.RelativePath,
                Sha256 = candidate.Sha256,
                Category = candidate.Category,
                Language = candidate.Language,
                LineCount = candidate.LineCount,
                IsSelected = candidate.Included,
                PreviewCommand = PreviewCommand
            };

            row.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(CodeRow.IsSelected))
                {
                    OnPropertyChanged(nameof(SelectedCount));
                    OnPropertyChanged(nameof(SelectedCountText));
                }
            };

            Candidates.Add(row);
        }

        var confirmations = await _engine.ListConfirmationsAsync(_shell.CurrentCase.Id, CancellationToken.None);
        var status = confirmations.FirstOrDefault(c => c.Stage == ConfirmationStage.CodeScope);
        IsConfirmed = status is not null && status.Confirmed && !status.IsStale;

        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedCountText));
    }

    partial void OnFilterChanged(string value) => OnPropertyChanged(nameof(VisibleCandidates));
}
