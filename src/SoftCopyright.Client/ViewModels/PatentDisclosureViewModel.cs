using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftCopyright.Application.Dtos;
using SoftCopyright.Client.Services;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Client.ViewModels;

/// <summary>权利要求预览行。</summary>
public sealed class ClaimRow
{
    public required int Number { get; init; }

    public required string TypeText { get; init; }

    public required string DependsText { get; init; }

    public required string Text { get; init; }

    public string HeaderText => $"权利要求 {Number}（{TypeText}）";
}

/// <summary>
/// 专利交底书步骤：填写技术交底书、生成专利草稿并预览权利要求。
///
/// 技术交底书是专利申请文件的唯一真源：摘要、权利要求与说明书都由它派生，
/// 工具不代为判断保护范围、新颖性与创造性。
/// </summary>
public sealed partial class PatentDisclosureViewModel : ObservableObject, IRefreshable
{
    private readonly ShellViewModel _shell;
    private readonly DesktopEngine _engine;

    public PatentDisclosureViewModel(ShellViewModel shell)
    {
        _shell = shell;
        _engine = shell.Engine;
        _patentType = PatentTypeOptions[0];
    }

    // ── 表单字段 ──────────────────────────────────────────────────────────

    [ObservableProperty] private string _title = string.Empty;

    [ObservableProperty] private string _technicalField = string.Empty;

    [ObservableProperty] private string _backgroundTechnology = string.Empty;

    [ObservableProperty] private string _problemStatement = string.Empty;

    [ObservableProperty] private string _technicalSolution = string.Empty;

    [ObservableProperty] private string _beneficialEffects = string.Empty;

    [ObservableProperty] private string _drawingDescription = string.Empty;

    [ObservableProperty] private string _priorArt = string.Empty;

    [ObservableProperty] private string _priorArtSources = string.Empty;

    [ObservableProperty] private string _applicantName = string.Empty;

    [ObservableProperty] private string _inventorNames = string.Empty;

    [ObservableProperty] private OptionItem? _patentType;

    [ObservableProperty] private string _note = string.Empty;

    // ── 生成结果 ──────────────────────────────────────────────────────────

    public ObservableCollection<ClaimRow> Claims { get; } = [];

    [ObservableProperty] private string _abstract = string.Empty;

    [ObservableProperty] private string _draftSummary = string.Empty;

    [ObservableProperty] private bool _hasDraft;

    /// <summary>是否有权利要求可展示。</summary>
    public bool HasClaims => Claims.Count > 0;

    /// <summary>是否有摘要可展示。</summary>
    public bool HasAbstract => !string.IsNullOrWhiteSpace(Abstract);

    /// <summary>专利类型候选项。</summary>
    public IReadOnlyList<OptionItem> PatentTypeOptions { get; } =
    [
        new OptionItem(nameof(Domain.PatentType.Invention), "发明专利"),
        new OptionItem(nameof(Domain.PatentType.UtilityModel), "实用新型")
    ];

    // ── 操作 ──────────────────────────────────────────────────────────────

    /// <summary>保存技术交底书。</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        await _shell.RunBusyAsync(async () =>
        {
            if (_shell.CurrentCase is null)
            {
                throw new InvalidOperationException("请先选择案例。");
            }

            await _engine.SavePatentDisclosureAsync(
                _shell.CurrentCase.Id,
                BuildRequest(),
                CancellationToken.None);

            _shell.StatusMessage = "技术交底书已保存。";
            await RefreshAsync();
        });
    }

    /// <summary>保存交底书并生成专利草稿（权利要求、摘要与实施例）。</summary>
    [RelayCommand]
    private async Task SaveAndDraftAsync()
    {
        await _shell.RunBusyAsync(async () =>
        {
            if (_shell.CurrentCase is null)
            {
                throw new InvalidOperationException("请先选择案例。");
            }

            var caseId = _shell.CurrentCase.Id;
            await _engine.SavePatentDisclosureAsync(caseId, BuildRequest(), CancellationToken.None);

            _shell.StatusMessage = "正在生成专利草稿……";
            var summary = await _engine.DraftPatentAsync(caseId, CancellationToken.None);

            foreach (var warning in summary.Warnings)
            {
                _shell.StatusMessage = warning;
            }

            _shell.StatusMessage =
                $"专利草稿已生成：技术特征 {summary.FeatureCount} 项，权利要求 {summary.ClaimCount} 条，实施例 {summary.EmbodimentCount} 个。" +
                "请核对权利要求后确认草稿，再生成专利申请文件。";

            await RefreshAsync();
        });
    }

    /// <summary>确认专利草稿（专利线的一道门禁：交底书与权利要求就绪）。</summary>
    [RelayCommand]
    private async Task ConfirmDraftAsync()
    {
        await _shell.RunBusyAsync(async () =>
        {
            if (_shell.CurrentCase is null)
            {
                throw new InvalidOperationException("请先选择案例。");
            }

            var caseId = _shell.CurrentCase.Id;
            await _engine.SavePatentDisclosureAsync(caseId, BuildRequest(), CancellationToken.None);

            await _engine.ConfirmAsync(
                caseId,
                ConfirmationStage.DocumentDraft,
                string.IsNullOrWhiteSpace(Note) ? "在桌面端确认专利草稿" : Note,
                CancellationToken.None);

            _shell.StatusMessage = "专利草稿已确认，可以生成专利申请文件。";
            await RefreshAsync();
            await _shell.GoToAsync(ShellStep.Materials);
        });
    }

    private PatentDisclosureUpdateRequest BuildRequest() => new(
        Title,
        TechnicalField,
        BackgroundTechnology,
        ProblemStatement,
        TechnicalSolution,
        BeneficialEffects,
        DrawingDescription,
        PriorArt,
        PriorArtSources,
        ApplicantName,
        InventorNames,
        PatentType?.Value == nameof(Domain.PatentType.UtilityModel)
            ? Domain.PatentType.UtilityModel
            : Domain.PatentType.Invention);

    public async Task RefreshAsync()
    {
        Claims.Clear();

        if (_shell.CurrentCase is null)
        {
            return;
        }

        var dto = await _engine.GetPatentDisclosureAsync(_shell.CurrentCase.Id, CancellationToken.None);

        Title = dto.Title;
        TechnicalField = dto.TechnicalField;
        BackgroundTechnology = dto.BackgroundTechnology;
        ProblemStatement = dto.ProblemStatement;
        TechnicalSolution = dto.TechnicalSolution;
        BeneficialEffects = dto.BeneficialEffects;
        DrawingDescription = dto.DrawingDescription;
        PriorArt = dto.PriorArt;
        PriorArtSources = dto.PriorArtSources;
        ApplicantName = dto.ApplicantName;
        InventorNames = dto.InventorNames;
        Abstract = dto.Abstract;
        PatentType = PatentTypeOptions.FirstOrDefault(o =>
            string.Equals(o.Value, dto.PatentType.ToString(), StringComparison.OrdinalIgnoreCase));

        foreach (var claim in dto.Claims)
        {
            Claims.Add(new ClaimRow
            {
                Number = claim.Number,
                TypeText = claim.Type == ClaimType.Independent ? "独立权利要求" : "从属权利要求",
                DependsText = claim.DependsOn is { } dep ? $"引用权利要求 {dep}" : string.Empty,
                Text = claim.Text
            });
        }

        HasDraft = Claims.Count > 0;
        OnPropertyChanged(nameof(HasClaims));
        OnPropertyChanged(nameof(HasAbstract));
    }
}
