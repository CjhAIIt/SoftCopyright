using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoftCopyright.Application.Dtos;
using SoftCopyright.Desktop.Services;
using SoftCopyright.Domain;

namespace SoftCopyright.Desktop.ViewModels;

/// <summary>
/// 申请信息步骤：按官方申请表填写申请信息。
///
/// 选项型字段直接复用 <see cref="ApplicantOptionCatalog" />——候选项、条件必填与跨字段约束
/// 只有一份定义，桌面端不做二次硬编码。
/// </summary>
public sealed partial class ApplicantViewModel : ObservableObject, IRefreshable
{
    private readonly ShellViewModel _shell;
    private readonly DesktopEngine _engine;

    public ApplicantViewModel(ShellViewModel shell)
    {
        _shell = shell;
        _engine = shell.Engine;
    }

    // ── 选项目录 ──────────────────────────────────────────────────────────

    public IReadOnlyList<OptionItem> OwnerTypeOptions { get; } = OptionsOf(nameof(ApplicantInfo.OwnerType));

    public IReadOnlyList<OptionItem> SoftwareSourceOptions { get; } = OptionsOf(nameof(ApplicantInfo.SoftwareSource));

    public IReadOnlyList<OptionItem> DevelopmentModeOptions { get; } = OptionsOf(nameof(ApplicantInfo.DevelopmentMode));

    public IReadOnlyList<OptionItem> PublicationStatusOptions { get; } = OptionsOf(nameof(ApplicantInfo.FirstPublicationStatus));

    public IReadOnlyList<OptionItem> RightsAcquisitionOptions { get; } = OptionsOf(nameof(ApplicantInfo.RightsAcquisitionMethod));

    public IReadOnlyList<OptionItem> RightsScopeOptions { get; } = OptionsOf(nameof(ApplicantInfo.RightsScope));

    public IReadOnlyList<OptionItem> ApplicationHandlingOptions { get; } = OptionsOf(nameof(ApplicantInfo.ApplicationHandlingMethod));

    public IReadOnlyList<OptionItem> DepositModeOptions { get; } = OptionsOf(nameof(ApplicantInfo.DepositMode));

    /// <summary>选项目录的来源说明。</summary>
    public string OptionSource => ApplicantOptionCatalog.Source;

    // ── 选项型字段 ────────────────────────────────────────────────────────

    [ObservableProperty] private OptionItem? _ownerType;

    [ObservableProperty] private OptionItem? _softwareSource;

    [ObservableProperty] private OptionItem? _developmentMode;

    [ObservableProperty] private OptionItem? _publicationStatus;

    [ObservableProperty] private OptionItem? _rightsAcquisitionMethod;

    [ObservableProperty] private OptionItem? _rightsScope;

    [ObservableProperty] private OptionItem? _applicationHandlingMethod;

    [ObservableProperty] private OptionItem? _depositMode;

    // ── 自由文本字段 ──────────────────────────────────────────────────────

    [ObservableProperty] private string _softwareFullName = string.Empty;

    [ObservableProperty] private string _softwareAbbreviation = string.Empty;

    [ObservableProperty] private string _versionNumber = string.Empty;

    [ObservableProperty] private string _classificationCode = string.Empty;

    [ObservableProperty] private string _copyrightOwner = string.Empty;

    [ObservableProperty] private string _modificationDescription = string.Empty;

    [ObservableProperty] private string _originalRegistrationNumber = string.Empty;

    [ObservableProperty] private string _partialRightsDescription = string.Empty;

    [ObservableProperty] private string _completionDate = string.Empty;

    [ObservableProperty] private string _firstPublicationDate = string.Empty;

    [ObservableProperty] private string _firstPublicationPlace = string.Empty;

    [ObservableProperty] private string _developmentEnvironment = string.Empty;

    [ObservableProperty] private string _runtimeEnvironment = string.Empty;

    [ObservableProperty] private string _programmingLanguages = string.Empty;

    [ObservableProperty] private string _sourceProgramAmount = string.Empty;

    [ObservableProperty] private string _functionSummary = string.Empty;

    [ObservableProperty] private string _note = string.Empty;

    [ObservableProperty] private bool _isConfirmed;

    /// <summary>乐观并发版本号：保存时带回，避免覆盖其他窗口的改动。</summary>
    private int _expectedRowVersion;

    // ── 条件联动 ──────────────────────────────────────────────────────────

    /// <summary>软件来源为「修改软件」类时，必须填写修改说明。</summary>
    public bool IsModificationVisible =>
        SoftwareSource?.Value is "Modified" or "ModifiedAuthorized";

    /// <summary>权利范围为「部分权利」时，必须注明具体权项。</summary>
    public bool IsPartialRightsVisible => RightsScope?.Value == "Partial";

    /// <summary>发表状态为「已发表」时，必须填写首次发表日期与地点。</summary>
    public bool IsPublicationVisible => PublicationStatus?.Value == "Published";

    /// <summary>继受取得或修改软件时，需要填写原登记号。</summary>
    public bool IsOriginalRegistrationVisible =>
        RightsAcquisitionMethod?.Value is "Assignment" or "Inheritance" or "Assumption" ||
        SoftwareSource?.Value is "Modified" or "ModifiedAuthorized";

    /// <summary>是否存在跨字段取值冲突。</summary>
    public bool HasConstraint => ConstraintHint is not null;

    /// <summary>原始取得要求开发方式为「单独开发」，冲突时在这里直接提示。</summary>
    public string? ConstraintHint
    {
        get
        {
            if (RightsAcquisitionMethod?.Value != "OriginalAcquisition")
            {
                return null;
            }

            if (DevelopmentMode is null || DevelopmentMode.Value == "Solo")
            {
                return null;
            }

            return "软件著作权为原始取得的，申请登记的软件须为独立开发（开发方式应选择「单独开发」）。";
        }
    }

    // ── 操作 ──────────────────────────────────────────────────────────────

    /// <summary>保存申请信息。</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        await _shell.RunBusyAsync(async () =>
        {
            if (_shell.CurrentCase is null)
            {
                throw new InvalidOperationException("请先选择案例。");
            }

            var request = BuildRequest(_shell.CurrentCase.Id);
            await _engine.SaveApplicantAsync(_shell.CurrentCase.Id, request, CancellationToken.None);
            _shell.StatusMessage = "申请信息已保存。修改后下游确认与已生成材料需要重新确认。";
            await RefreshAsync();
        });
    }

    /// <summary>保存并确认申请信息。</summary>
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
            var request = BuildRequest(caseId);
            await _engine.SaveApplicantAsync(caseId, request, CancellationToken.None);

            await _engine.ConfirmAsync(
                caseId,
                ConfirmationStage.ApplicantInfo,
                string.IsNullOrWhiteSpace(Note) ? "在桌面端确认申请信息" : Note,
                CancellationToken.None);

            _shell.StatusMessage = "申请信息已保存并确认，可以继续选择代码范围。";
            await RefreshAsync();
            await _shell.GoToAsync(ShellStep.Code);
        });
    }

    private UpdateApplicantInfoRequest BuildRequest(Guid caseId)
    {
        return new UpdateApplicantInfoRequest(
            SoftwareFullName,
            string.IsNullOrWhiteSpace(SoftwareAbbreviation) ? null : SoftwareAbbreviation,
            VersionNumber,
            CopyrightOwner,
            DevelopmentMode?.Value ?? string.Empty,
            RightsAcquisitionMethod?.Value ?? string.Empty,
            string.IsNullOrWhiteSpace(CompletionDate) ? null : CompletionDate,
            PublicationStatus?.Value ?? string.Empty,
            string.IsNullOrWhiteSpace(FirstPublicationDate) ? null : FirstPublicationDate,
            DevelopmentEnvironment,
            RuntimeEnvironment,
            ProgrammingLanguages,
            SourceProgramAmount,
            FunctionSummary,
            OwnerType?.Value ?? string.Empty,
            SoftwareSource?.Value ?? string.Empty,
            string.IsNullOrWhiteSpace(ModificationDescription) ? null : ModificationDescription,
            string.IsNullOrWhiteSpace(OriginalRegistrationNumber) ? null : OriginalRegistrationNumber,
            RightsScope?.Value ?? string.Empty,
            string.IsNullOrWhiteSpace(PartialRightsDescription) ? null : PartialRightsDescription,
            string.IsNullOrWhiteSpace(FirstPublicationPlace) ? null : FirstPublicationPlace,
            DepositMode?.Value ?? string.Empty,
            ApplicationHandlingMethod?.Value ?? string.Empty,
            string.IsNullOrWhiteSpace(ClassificationCode) ? null : ClassificationCode,
            _expectedRowVersion);
    }

    public async Task RefreshAsync()
    {
        if (_shell.CurrentCase is null)
        {
            return;
        }

        var info = await _engine.GetApplicantAsync(_shell.CurrentCase.Id, CancellationToken.None);
        if (info is null)
        {
            return;
        }

        SoftwareFullName = info.SoftwareFullName;
        SoftwareAbbreviation = info.SoftwareAbbreviation ?? string.Empty;
        VersionNumber = info.VersionNumber;
        ClassificationCode = info.ClassificationCode ?? string.Empty;
        CopyrightOwner = info.CopyrightOwner;
        OwnerType = FindOption(OwnerTypeOptions, info.OwnerType);
        SoftwareSource = FindOption(SoftwareSourceOptions, info.SoftwareSource);
        ModificationDescription = info.ModificationDescription ?? string.Empty;
        OriginalRegistrationNumber = info.OriginalRegistrationNumber ?? string.Empty;
        DevelopmentMode = FindOption(DevelopmentModeOptions, info.DevelopmentMode);
        RightsAcquisitionMethod = FindOption(RightsAcquisitionOptions, info.RightsAcquisitionMethod);
        RightsScope = FindOption(RightsScopeOptions, info.RightsScope);
        PartialRightsDescription = info.PartialRightsDescription ?? string.Empty;
        CompletionDate = info.CompletionDate ?? string.Empty;
        PublicationStatus = FindOption(PublicationStatusOptions, info.FirstPublicationStatus);
        FirstPublicationDate = info.FirstPublicationDate ?? string.Empty;
        FirstPublicationPlace = info.FirstPublicationPlace ?? string.Empty;
        DevelopmentEnvironment = info.DevelopmentEnvironment;
        RuntimeEnvironment = info.RuntimeEnvironment;
        ProgrammingLanguages = info.ProgrammingLanguages;
        SourceProgramAmount = info.SourceProgramAmount;
        DepositMode = FindOption(DepositModeOptions, info.DepositMode);
        ApplicationHandlingMethod = FindOption(ApplicationHandlingOptions, info.ApplicationHandlingMethod);
        FunctionSummary = info.FunctionSummary;

        _expectedRowVersion = info.RowVersion;
        IsConfirmed = info.IsConfirmed;
    }

    // ── 联动通知 ──────────────────────────────────────────────────────────

    partial void OnSoftwareSourceChanged(OptionItem? value) => NotifyConditionChanged();

    partial void OnRightsScopeChanged(OptionItem? value) => NotifyConditionChanged();

    partial void OnPublicationStatusChanged(OptionItem? value) => NotifyConditionChanged();

    partial void OnRightsAcquisitionMethodChanged(OptionItem? value) => NotifyConditionChanged();

    partial void OnDevelopmentModeChanged(OptionItem? value) => NotifyConditionChanged();

    private void NotifyConditionChanged()
    {
        OnPropertyChanged(nameof(IsModificationVisible));
        OnPropertyChanged(nameof(IsPartialRightsVisible));
        OnPropertyChanged(nameof(IsPublicationVisible));
        OnPropertyChanged(nameof(IsOriginalRegistrationVisible));
        OnPropertyChanged(nameof(ConstraintHint));
        OnPropertyChanged(nameof(HasConstraint));
    }

    private static IReadOnlyList<OptionItem> OptionsOf(string field) =>
        ApplicantOptionCatalog.OptionsOf(field)
            .Select(o => new OptionItem(o.Value, o.Label, o.Note))
            .ToList();

    private static OptionItem? FindOption(IReadOnlyList<OptionItem> options, string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : options.FirstOrDefault(o => string.Equals(o.Value, value, StringComparison.OrdinalIgnoreCase));
}
