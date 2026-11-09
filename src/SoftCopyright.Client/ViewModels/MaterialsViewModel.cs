using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftCopyright.Application;
using SoftCopyright.Client.Services;
using SoftCopyright.Domain;

namespace SoftCopyright.Client.ViewModels;

/// <summary>产物条目。</summary>
public sealed class ArtifactRow
{
    public required Guid Id { get; init; }

    public required string FileName { get; init; }

    public required string KindText { get; init; }

    public required string SizeText { get; init; }

    public required bool IsStale { get; init; }

    public ICommand? ExportCommand { get; init; }

    public string StatusText => IsStale ? "已失效" : "可用";
}

/// <summary>材料与导出步骤：校验、生成材料并导出到本机。</summary>
public sealed partial class MaterialsViewModel : ObservableObject, IRefreshable
{
    private readonly ShellViewModel _shell;
    private readonly DesktopEngine _engine;

    public MaterialsViewModel(ShellViewModel shell)
    {
        _shell = shell;
        _engine = shell.Engine;
    }

    /// <summary>由视图注入的「另存为」对话框。</summary>
    public Func<string, Task<string?>>? PickSavePath { get; set; }

    public ObservableCollection<ValidationRow> Validations { get; } = [];

    public ObservableCollection<ArtifactRow> Artifacts { get; } = [];

    [ObservableProperty] private string _summary = string.Empty;

    [ObservableProperty] private bool _hasGenerated;

    /// <summary>是否有校验结果可展示。</summary>
    public bool HasValidations => Validations.Count > 0;

    /// <summary>是否有产物可展示。</summary>
    public bool HasArtifacts => Artifacts.Count > 0;

    /// <summary>当前案例是否走专利流程。</summary>
    public bool IsPatentFlow => _shell.IsPatentFlow;

    /// <summary>执行校验。</summary>
    [RelayCommand]
    private async Task ValidateAsync()
    {
        await _shell.RunBusyAsync(async () =>
        {
            if (_shell.CurrentCase is null)
            {
                throw new InvalidOperationException("请先选择案例。");
            }

            var results = await _engine.ValidateAsync(_shell.CurrentCase.Id, CancellationToken.None);
            Validations.Clear();

            var actionRequired = 0;
            var warnings = 0;
            foreach (var result in results)
            {
                Validations.Add(new ValidationRow(
                    result.RuleId,
                    result.Title,
                    result.Message,
                    Mapping.ValidationLevelText(result.Level)));
                if (result.Level == ValidationLevel.ActionRequired)
                {
                    actionRequired++;
                }
                else if (result.Level == ValidationLevel.Warning)
                {
                    warnings++;
                }
            }

            Summary = $"共 {results.Count} 条检查结果：需处理 {actionRequired} 条，警告 {warnings} 条。";
            OnPropertyChanged(nameof(HasValidations));
            _shell.StatusMessage = "校验完成。";
        });
    }

    /// <summary>生成材料。</summary>
    [RelayCommand]
    private async Task GenerateAsync()
    {
        await _shell.RunBusyAsync(async () =>
        {
            if (_shell.CurrentCase is null)
            {
                throw new InvalidOperationException("请先选择案例。");
            }

            var caseId = _shell.CurrentCase.Id;

            // 与 Web 端一致：生成材料前必须已通过相应门禁。
            // 软著要求申请信息与代码范围两道，专利只要求确认草稿一道。
            var statuses = await _engine.ListConfirmationsAsync(caseId, CancellationToken.None);

            if (!IsPatentFlow)
            {
                var applicant = statuses.FirstOrDefault(s => s.Stage == ConfirmationStage.ApplicantInfo);
                if (applicant is null || !applicant.Confirmed || applicant.IsStale)
                {
                    throw new InvalidOperationException("请先填写并确认申请信息，再生成材料。");
                }

                var code = statuses.FirstOrDefault(s => s.Stage == ConfirmationStage.CodeScope);
                if (code is null || !code.Confirmed || code.IsStale)
                {
                    throw new InvalidOperationException("请先勾选源文件并确认代码范围，再生成材料。");
                }
            }

            var draft = statuses.FirstOrDefault(s => s.Stage == ConfirmationStage.DocumentDraft);
            if (draft is null || !draft.Confirmed || draft.IsStale)
            {
                throw new InvalidOperationException(IsPatentFlow
                    ? "请先审阅并确认专利草稿，再生成专利申请文件。"
                    : "请先审阅并确认文档草稿，再生成正式材料。");
            }

            _shell.StatusMessage = "正在生成材料……";
            var summary = await _engine.GenerateAsync(caseId, CancellationToken.None);
            Summary = $"已生成 {summary.ArtifactCount} 份产物，材料包 {summary.PackageBytes} 字节。";

            foreach (var warning in summary.Warnings)
            {
                _shell.StatusMessage = warning;
            }

            HasGenerated = true;
            await RefreshAsync();
        });
    }

    /// <summary>确认最终材料（归档）。</summary>
    [RelayCommand]
    private async Task ConfirmFinalAsync()
    {
        await _shell.RunBusyAsync(async () =>
        {
            if (_shell.CurrentCase is null)
            {
                throw new InvalidOperationException("请先选择案例。");
            }

            var caseId = _shell.CurrentCase.Id;

            await _engine.ConfirmAsync(
                caseId,
                ConfirmationStage.FinalPackage,
                "在桌面端确认最终材料",
                CancellationToken.None);

            _shell.StatusMessage = "最终材料已确认，案例已归档。";
            await RefreshAsync();
        });
    }

    /// <summary>
    /// 确认文档草稿 / 专利草稿。这是生成材料前的最后一道门禁：
    /// 软著要求前序三道门禁都已通过且未失效，专利要求交底书与权利要求就绪。
    /// </summary>
    [RelayCommand]
    private async Task ConfirmDraftAsync()
    {
        await _shell.RunBusyAsync(async () =>
        {
            if (_shell.CurrentCase is null)
            {
                throw new InvalidOperationException("请先选择案例。");
            }

            await _engine.ConfirmAsync(
                _shell.CurrentCase.Id,
                ConfirmationStage.DocumentDraft,
                IsPatentFlow ? "在桌面端确认专利草稿" : "在桌面端确认文档草稿",
                CancellationToken.None);

            _shell.StatusMessage = IsPatentFlow
                ? "专利草稿已确认，可以生成专利申请文件。"
                : "文档草稿已确认，可以生成正式材料。";

            await RefreshAsync();
        });
    }

    /// <summary>导出产物到本机目录。</summary>
    [RelayCommand]
    private async Task ExportAsync(ArtifactRow row)
    {
        await _shell.RunBusyAsync(async () =>
        {
            if (PickSavePath is null)
            {
                throw new InvalidOperationException("当前环境不支持另存为对话框。");
            }

            var suggested = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                row.FileName);

            var target = await PickSavePath(suggested);
            if (string.IsNullOrWhiteSpace(target))
            {
                return;
            }

            var content = await _engine.ReadArtifactAsync(row.Id, CancellationToken.None);
            if (content is null)
            {
                throw new InvalidOperationException("产物不存在或已失效，请重新生成材料。");
            }

            await File.WriteAllBytesAsync(target, content);
            _shell.StatusMessage = $"已导出到：{target}";
        });
    }

    public async Task RefreshAsync()
    {
        Artifacts.Clear();

        if (_shell.CurrentCase is null)
        {
            return;
        }

        var artifacts = await _engine.ListArtifactsAsync(_shell.CurrentCase.Id, CancellationToken.None);
        foreach (var artifact in artifacts.OrderByDescending(a => a.CreatedAt))
        {
            Artifacts.Add(new ArtifactRow
            {
                Id = artifact.Id,
                FileName = artifact.FileName,
                KindText = ArtifactKindText(artifact.Kind),
                SizeText = artifact.SizeBytes < 1024
                    ? $"{artifact.SizeBytes} B"
                    : $"{artifact.SizeBytes / 1024.0:F1} KB",
                IsStale = artifact.IsStale,
                ExportCommand = ExportCommand
            });
        }

        HasGenerated = Artifacts.Count > 0;
        OnPropertyChanged(nameof(HasArtifacts));
    }

    private static string ArtifactKindText(ArtifactKind kind) => kind switch
    {
        ArtifactKind.ApplicationFormInfo => "申请表信息",
        ArtifactKind.OperationManual => "操作手册",
        ArtifactKind.CodeMaterial => "代码鉴别材料",
        ArtifactKind.ProjectProfile => "项目画像",
        ArtifactKind.EvidenceManifest => "证据清单",
        ArtifactKind.ApplicantInfoDraft => "申请信息草稿",
        ArtifactKind.ValidationReport => "校验报告",
        ArtifactKind.Package => "材料包",
        ArtifactKind.PatentClaims => "权利要求书",
        ArtifactKind.PatentSpecification => "说明书",
        ArtifactKind.PatentAbstract => "摘要",
        ArtifactKind.PatentDisclosure => "技术交底书",
        ArtifactKind.Manifest => "材料清单",
        _ => kind.ToString()
    };
}

/// <summary>校验结果行。</summary>
public sealed record ValidationRow(string RuleId, string Title, string Message, string LevelText)
{
    public string Level => LevelText switch
    {
        "通过" => "ok",
        "警告" => "warn",
        _ => "danger"
    };
}
