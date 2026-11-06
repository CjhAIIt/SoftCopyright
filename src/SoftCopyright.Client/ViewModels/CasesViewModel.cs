using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftCopyright.Application;
using SoftCopyright.Client.Services;
using SoftCopyright.Domain;

namespace SoftCopyright.Client.ViewModels;

/// <summary>案例列表中的一行。</summary>
public sealed class CaseRow
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string StageText { get; init; }

    public required string StatusMessage { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public required MaterialKind MaterialKind { get; init; }

    public ICommand? OpenCommand { get; init; }

    public ICommand? DeleteCommand { get; init; }

    public string KindText => MaterialKind == MaterialKind.Patent ? "专利申请" : "软著登记";

    public string UpdatedText => UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}

/// <summary>案例步骤：新建、选择与删除本机案例。</summary>
public sealed partial class CasesViewModel : ObservableObject, IRefreshable
{
    private readonly ShellViewModel _shell;
    private readonly DesktopEngine _engine;

    public CasesViewModel(ShellViewModel shell)
    {
        _shell = shell;
        _engine = shell.Engine;
        _newCaseKind = KindOptions[0];
    }

    public ObservableCollection<CaseRow> Cases { get; } = [];

    /// <summary>是否已有案例。</summary>
    public bool HasCases => Cases.Count > 0;

    [ObservableProperty] private string _newCaseName = string.Empty;

    [ObservableProperty] private OptionItem? _newCaseKind;

    [ObservableProperty] private bool _showPatentCases;

    /// <summary>新建案例时可选的材料类型。</summary>
    public IReadOnlyList<OptionItem> KindOptions { get; } =
    [
        new OptionItem(nameof(MaterialKind.SoftwareCopyright), "软著登记"),
        new OptionItem(nameof(MaterialKind.Patent), "专利申请")
    ];

    /// <summary>新建案例。</summary>
    [RelayCommand]
    private async Task CreateAsync()
    {
        await _shell.RunBusyAsync(async () =>
        {
            var kind = NewCaseKind?.Value == nameof(MaterialKind.Patent)
                ? MaterialKind.Patent
                : MaterialKind.SoftwareCopyright;

            var entity = await _engine.CreateCaseAsync(NewCaseName, kind, CancellationToken.None);
            NewCaseName = string.Empty;
            _shell.SetCase(entity);
            _shell.StatusMessage = "案例已创建，接下来导入代码仓库。";
            await RefreshAsync();
            await _shell.GoToAsync(ShellStep.Import);
        });
    }

    /// <summary>打开案例。</summary>
    [RelayCommand]
    private async Task OpenAsync(CaseRow row)
    {
        var entity = await _engine.GetCaseAsync(row.Id, CancellationToken.None);
        if (entity is null)
        {
            _shell.ErrorMessage = "案例不存在或已被删除。";
            return;
        }

        _shell.SetCase(entity);
        _shell.StatusMessage = $"已选择案例：{row.Name}";
        await _shell.GoToAsync(ShellStep.Import);
    }

    /// <summary>删除案例。</summary>
    [RelayCommand]
    private async Task DeleteAsync(CaseRow row)
    {
        await _shell.RunBusyAsync(async () =>
        {
            var deleted = await _engine.DeleteCaseAsync(row.Id, CancellationToken.None);
            if (!deleted)
            {
                throw new InvalidOperationException("案例删除失败，可能已处于归档状态。");
            }

            if (_shell.CurrentCase?.Id == row.Id)
            {
                _shell.SetCase(null);
            }

            _shell.StatusMessage = "案例已删除。";
            await RefreshAsync();
        });
    }

    public async Task RefreshAsync()
    {
        var kind = ShowPatentCases ? MaterialKind.Patent : MaterialKind.SoftwareCopyright;
        var list = await _engine.ListCasesAsync(kind, CancellationToken.None);

        Cases.Clear();
        foreach (var entity in list.OrderByDescending(c => c.UpdatedAt))
        {
            Cases.Add(new CaseRow
            {
                Id = entity.Id,
                Name = string.IsNullOrWhiteSpace(entity.SoftwareFullName) ? entity.Name : entity.SoftwareFullName,
                StageText = Mapping.StageText(entity.Stage),
                StatusMessage = entity.StatusMessage,
                UpdatedAt = entity.UpdatedAt,
                MaterialKind = entity.MaterialKind,
                OpenCommand = OpenCommand,
                DeleteCommand = DeleteCommand
            });
        }

        OnPropertyChanged(nameof(HasCases));
    }

    partial void OnShowPatentCasesChanged(bool value) => _ = RefreshAsync();
}
