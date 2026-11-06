using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftCopyright.Application.Dtos;
using SoftCopyright.Client.Services;
using SoftCopyright.Domain;

namespace SoftCopyright.Client.ViewModels;

/// <summary>分析与证据步骤：查看项目画像、覆盖情况与待确认问题，并确认画像。</summary>
public sealed partial class AnalysisViewModel : ObservableObject, IRefreshable
{
    private readonly ShellViewModel _shell;
    private readonly DesktopEngine _engine;

    public AnalysisViewModel(ShellViewModel shell)
    {
        _shell = shell;
        _engine = shell.Engine;
    }

    public ObservableCollection<string> Questions { get; } = [];

    public ObservableCollection<CoverageDto> Coverage { get; } = [];

    public ObservableCollection<FeatureRow> Features { get; } = [];

    public ObservableCollection<EvidenceRow> Evidences { get; } = [];

    [ObservableProperty] private string _projectName = "尚未扫描";

    [ObservableProperty] private string _projectVersion = string.Empty;

    [ObservableProperty] private string _purpose = string.Empty;

    [ObservableProperty] private string _technologies = string.Empty;

    [ObservableProperty] private string _entrypoints = string.Empty;

    [ObservableProperty] private string _adapters = string.Empty;

    [ObservableProperty] private bool _isConfirmed;

    [ObservableProperty] private string _note = string.Empty;

    /// <summary>是否有待确认事项。</summary>
    public bool HasQuestions => Questions.Count > 0;

    /// <summary>是否有覆盖情况。</summary>
    public bool HasCoverage => Coverage.Count > 0;

    /// <summary>是否有功能清单。</summary>
    public bool HasFeatures => Features.Count > 0;

    /// <summary>是否有证据。</summary>
    public bool HasEvidences => Evidences.Count > 0;

    public string ProjectVersionText => $"版本：{ProjectVersion}";

    public string TechnologiesText => $"技术栈：{Technologies}";

    public string EntrypointsText => $"入口点：{Entrypoints}";

    public string AdaptersText => $"适配器：{Adapters}";

    /// <summary>确认项目画像。</summary>
    [RelayCommand]
    private async Task ConfirmAsync()
    {
        await _shell.RunBusyAsync(async () =>
        {
            if (_shell.CurrentCase is null)
            {
                throw new InvalidOperationException("请先选择案例。");
            }

            await _engine.ConfirmAsync(
                _shell.CurrentCase.Id,
                ConfirmationStage.ProjectProfile,
                string.IsNullOrWhiteSpace(Note) ? "在桌面端确认项目画像" : Note,
                CancellationToken.None);

            _shell.StatusMessage = "项目画像已确认，可以继续填写申请信息。";
            await RefreshAsync();
            await _shell.GoToAsync(ShellStep.Applicant);
        });
    }

    public async Task RefreshAsync()
    {
        Questions.Clear();
        Coverage.Clear();
        Features.Clear();
        Evidences.Clear();

        if (_shell.CurrentCase is null)
        {
            return;
        }

        var analysis = await _engine.GetAnalysisAsync(_shell.CurrentCase.Id, CancellationToken.None);
        if (analysis is null)
        {
            ProjectName = "尚未扫描";
            ProjectVersion = string.Empty;
            Purpose = string.Empty;
            Technologies = string.Empty;
            Entrypoints = string.Empty;
            Adapters = string.Empty;
            IsConfirmed = false;
            NotifyContentChanged();
            return;
        }

        ProjectName = analysis.Name ?? "尚未扫描";
        ProjectVersion = analysis.Version ?? string.Empty;
        Purpose = analysis.Purpose ?? string.Empty;
        Technologies = analysis.Technologies.Count == 0 ? "未识别" : string.Join("、", analysis.Technologies);
        Entrypoints = analysis.Entrypoints.Count == 0 ? "未识别" : string.Join("、", analysis.Entrypoints);
        Adapters = analysis.AdapterHits.Count == 0 ? "未命中" : string.Join("；", analysis.AdapterHits);

        foreach (var question in analysis.OpenQuestions)
        {
            Questions.Add(question);
        }

        foreach (var item in analysis.Coverage)
        {
            Coverage.Add(item);
        }

        foreach (var feature in analysis.Features)
        {
            Features.Add(new FeatureRow(feature.Value ?? "（无名称）", feature.StatusText));
        }

        foreach (var evidence in analysis.Evidences)
        {
            Evidences.Add(new EvidenceRow(
                evidence.RelativePath,
                evidence.StartLine,
                evidence.EndLine,
                evidence.EvidenceType));
        }

        NotifyContentChanged();

        var confirmations = await _engine.ListConfirmationsAsync(_shell.CurrentCase.Id, CancellationToken.None);
        var status = confirmations.FirstOrDefault(c => c.Stage == ConfirmationStage.ProjectProfile);
        IsConfirmed = status is not null && status.Confirmed && !status.IsStale;
    }

    private void NotifyContentChanged()
    {
        OnPropertyChanged(nameof(HasQuestions));
        OnPropertyChanged(nameof(HasCoverage));
        OnPropertyChanged(nameof(HasFeatures));
        OnPropertyChanged(nameof(HasEvidences));
        OnPropertyChanged(nameof(ProjectVersionText));
        OnPropertyChanged(nameof(TechnologiesText));
        OnPropertyChanged(nameof(EntrypointsText));
        OnPropertyChanged(nameof(AdaptersText));
    }

    partial void OnProjectVersionChanged(string value) => OnPropertyChanged(nameof(ProjectVersionText));

    partial void OnTechnologiesChanged(string value) => OnPropertyChanged(nameof(TechnologiesText));

    partial void OnEntrypointsChanged(string value) => OnPropertyChanged(nameof(EntrypointsText));

    partial void OnAdaptersChanged(string value) => OnPropertyChanged(nameof(AdaptersText));
}

/// <summary>画像中的功能条目。</summary>
public sealed record FeatureRow(string Name, string StatusText);

/// <summary>证据条目。</summary>
public sealed record EvidenceRow(string Path, int? StartLine, int? EndLine, string Type)
{
    public string LocationText => StartLine is null ? Path : $"{Path}:{StartLine}";
}
