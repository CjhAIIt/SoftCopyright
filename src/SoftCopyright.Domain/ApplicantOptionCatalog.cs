namespace SoftCopyright.Domain;

/// <summary>申请表中一个受约束的候选项。</summary>
public sealed record ApplicantOption(string Value, string Label, string? Note = null)
{
    /// <summary>
    /// 历史自由文本写法的别名。用于把旧数据里填的「独立开发」这类中文写法
    /// 归一化到规范选项码，避免升级后旧案例直接失效。
    /// </summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];
}

/// <summary>申请表中一个选项型字段的定义。</summary>
public sealed record ApplicantOptionField(
    string Field,
    string Label,
    string Description,
    bool Required,
    IReadOnlyList<ApplicantOption> Options);

/// <summary>条件必填规则：某字段取特定选项时，另一个字段必须填写。</summary>
public sealed record ApplicantCondition(
    string Field,
    string Value,
    string RequiredField,
    string Reason);

/// <summary>跨字段取值约束：某字段取特定选项时，相关字段只能取指定值。</summary>
public sealed record ApplicantConstraint(
    string Field,
    string Value,
    string RelatedField,
    IReadOnlyList<string> AllowedValues,
    string Reason);

/// <summary>
/// 软著登记申请表的选项目录。
///
/// 依据中国版权保护中心《计算机软件著作权登记申请表填写说明》整理，
/// 把「申请信息」里本应是选项而非自由文本的字段收敛为受约束的候选项，
/// 并把说明里写明的联动要求（条件必填、取值互斥）表达为数据。
///
/// 说明：本目录是本项目的工程实现，不是官方权威解释，材料中一律提示申请人按官方最新要求核对。
/// </summary>
public static class ApplicantOptionCatalog
{
    /// <summary>规则来源说明，写入材料与校验报告。</summary>
    public const string Source =
        "依据中国版权保护中心《计算机软件著作权登记申请表填写说明》整理；本目录为工程实现，最终以官方最新要求为准。";

    // ── 软件作品说明 ────────────────────────────────────────────────────────

    public static readonly ApplicantOptionField SoftwareSourceField = new(
        nameof(ApplicantInfo.SoftwareSource),
        "软件来源",
        "选择申请软件是原创软件还是修改软件。",
        true,
        [
            new("Original", "原创软件", "由著作权人独立开发完成的软件。")
                { Aliases = ["原创", "原创软件"] },
            new("Modified", "修改软件", "前期版本已登记的，应填写原登记号并提交原登记证书。")
                { Aliases = ["修改", "修改软件"] },
            new("ModifiedAuthorized", "修改软件须经原权利人授权", "修改他人软件，须提交原著作权人的许可证明。")
                { Aliases = ["修改软件（须经原权利人授权）", "修改他人软件"] }
        ]);

    public static readonly ApplicantOptionField DevelopmentModeField = new(
        nameof(ApplicantInfo.DevelopmentMode),
        "开发方式",
        "根据实际情况选择开发方式之一。",
        true,
        [
            new("Solo", "单独开发", "依靠自身条件自行开发完成。")
                { Aliases = ["单独开发", "独立开发", "自研", "自主开发"] },
            new("Cooperative", "合作开发", "需提交合作开发协议原件的照片或扫描件。")
                { Aliases = ["合作开发", "联合开发"] },
            new("Commissioned", "委托开发", "需提交委托协议原件的照片或扫描件。")
                { Aliases = ["委托开发", "受托开发"] },
            new("AssignedTask", "下达任务开发", "需提交项目任务书原件的照片或扫描件。")
                { Aliases = ["下达任务开发", "任务开发"] }
        ]);

    public static readonly ApplicantOptionField FirstPublicationStatusField = new(
        nameof(ApplicantInfo.FirstPublicationStatus),
        "发表状态",
        "选择软件首次公之于众的状态。",
        true,
        [
            new("Published", "已发表", "须填写首次发表日期与首次发表地点。")
                { Aliases = ["已发表", "发表"] },
            new("Unpublished", "未发表", "首次发表日期与地点留空。")
                { Aliases = ["未发表", "未公开发表"] }
        ]);

    // ── 权利说明 ────────────────────────────────────────────────────────────

    public static readonly ApplicantOptionField RightsAcquisitionMethodField = new(
        nameof(ApplicantInfo.RightsAcquisitionMethod),
        "权利取得方式",
        "选择著作权人取得权利的方式。",
        true,
        [
            new("OriginalAcquisition", "原始取得", "申请登记的软件须为独立开发。")
                { Aliases = ["原始取得", "原始"] },
            new("Assignment", "继受取得—受让", "需提交软件著作权转让合同原件的照片或扫描件。")
                { Aliases = ["受让", "受让取得", "继受取得—受让", "继受取得(受让)"] },
            new("Inheritance", "继受取得—继承", "需提交死亡证明、有效遗嘱、关系证明等材料。")
                { Aliases = ["继承", "继承取得", "继受取得—继承", "继受取得(继承)"] },
            new("Assumption", "继受取得—承受", "需提交企业变更、终止、清算等相关证明材料。")
                { Aliases = ["承受", "承受取得", "继受取得—承受", "继受取得(承受)"] }
        ]);

    public static readonly ApplicantOptionField RightsScopeField = new(
        nameof(ApplicantInfo.RightsScope),
        "权利范围",
        "选择著作权人享有的权利范围。",
        true,
        [
            new("All", "全部权利", "指《计算机软件保护条例》第八条规定的所有权利。")
                { Aliases = ["全部权利", "全部"] },
            new("Partial", "部分权利", "须注明具体的权项。")
                { Aliases = ["部分权利", "部分"] }
        ]);

    // ── 著作权人与办理方式 ──────────────────────────────────────────────────

    public static readonly ApplicantOptionField OwnerTypeField = new(
        nameof(ApplicantInfo.OwnerType),
        "著作权人类别",
        "选择著作权人的类别，证件类型应与所提交身份证明文件一致。",
        true,
        [
            new("NaturalPerson", "自然人", "申请人为自然人的应本人签名或加盖人名章。")
                { Aliases = ["自然人", "个人"] },
            new("LegalPerson", "法人", "申请人为机构的应加盖公章。")
                { Aliases = ["法人", "企业", "公司"] },
            new("UnincorporatedOrganization", "非法人组织", null)
                { Aliases = ["非法人组织", "非法人"] },
            new("Other", "其他", null)
                { Aliases = ["其他"] }
        ]);

    public static readonly ApplicantOptionField ApplicationHandlingMethodField = new(
        nameof(ApplicantInfo.ApplicationHandlingMethod),
        "申请办理方式",
        "选择由著作权人还是代理人办理申请。",
        true,
        [
            new("ByOwner", "著作权人申请办理", "著作权人有多个的，申请人为著作权人之一。")
                { Aliases = ["著作权人", "著作权人申请办理", "自行办理", "本人办理"] },
            new("ByAgent", "代理人申请办理", "须注明委托代理范围和权限，以及代理授权期限。")
                { Aliases = ["代理人", "代理人申请办理", "委托代理"] }
        ]);

    // ── 软件鉴别材料 ────────────────────────────────────────────────────────

    public static readonly ApplicantOptionField DepositModeField = new(
        nameof(ApplicantInfo.DepositMode),
        "鉴别材料交存方式",
        "选择鉴别材料的交存方式，例外交存需选择其中一种。",
        true,
        [
            new("General", "一般交存",
                "源程序连续的前 30 页和后 30 页；任何一种文档连续的前 30 页和后 30 页；不足 60 页的全部提交。")
                { Aliases = ["一般交存", "普通交存"] },
            new("ExceptionMasked", "例外交存 a：源程序前后各 30 页，机密部分遮盖",
                "机密部分用黑色宽斜线覆盖，覆盖部分不得超过交存源程序的 50%。")
                { Aliases = ["例外交存a", "例外交存 a", "例外交存"] },
            new("ExceptionPages", "例外交存 b：源程序连续前 10 页 + 任意连续 50 页", null)
                { Aliases = ["例外交存b", "例外交存 b"] },
            new("ExceptionTarget", "例外交存 c：目标程序前后各 30 页 + 源程序任意连续 20 页", null)
                { Aliases = ["例外交存c", "例外交存 c"] }
        ]);

    /// <summary>全部选项型字段。</summary>
    public static IReadOnlyList<ApplicantOptionField> All { get; } =
    [
        SoftwareSourceField,
        DevelopmentModeField,
        FirstPublicationStatusField,
        RightsAcquisitionMethodField,
        RightsScopeField,
        OwnerTypeField,
        ApplicationHandlingMethodField,
        DepositModeField
    ];

    /// <summary>条件必填规则。</summary>
    public static IReadOnlyList<ApplicantCondition> Conditions { get; } =
    [
        new(SoftwareSourceField.Field, "Modified", nameof(ApplicantInfo.ModificationDescription),
            "选择「修改软件」时须填写修改说明。"),
        new(SoftwareSourceField.Field, "ModifiedAuthorized", nameof(ApplicantInfo.ModificationDescription),
            "选择「修改软件须经原权利人授权」时须填写修改说明。"),
        new(RightsAcquisitionMethodField.Field, "Assignment", nameof(ApplicantInfo.OriginalRegistrationNumber),
            "继受取得的软件若已做过著作权登记，须填写原登记号。"),
        new(RightsAcquisitionMethodField.Field, "Inheritance", nameof(ApplicantInfo.OriginalRegistrationNumber),
            "继受取得的软件若已做过著作权登记，须填写原登记号。"),
        new(RightsAcquisitionMethodField.Field, "Assumption", nameof(ApplicantInfo.OriginalRegistrationNumber),
            "继受取得的软件若已做过著作权登记，须填写原登记号。"),
        new(RightsScopeField.Field, "Partial", nameof(ApplicantInfo.PartialRightsDescription),
            "选择「部分权利」时须注明具体的权项。"),
        new(FirstPublicationStatusField.Field, "Published", nameof(ApplicantInfo.FirstPublicationDate),
            "选择「已发表」时须填写首次发表日期。"),
        new(FirstPublicationStatusField.Field, "Published", nameof(ApplicantInfo.FirstPublicationPlace),
            "选择「已发表」时须填写首次发表地点。")
    ];

    /// <summary>跨字段取值约束。</summary>
    public static IReadOnlyList<ApplicantConstraint> Constraints { get; } =
    [
        new(RightsAcquisitionMethodField.Field, "OriginalAcquisition", DevelopmentModeField.Field, ["Solo"],
            "软件著作权为原始取得的，申请登记的软件须为独立开发。")
    ];

    private static readonly Dictionary<string, ApplicantOptionField> FieldMap =
        All.ToDictionary(f => f.Field, StringComparer.Ordinal);

    /// <summary>申请信息全部字段的中文标签（选项型与非选项型都在这里）。</summary>
    public static IReadOnlyDictionary<string, string> FieldLabels { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [nameof(ApplicantInfo.SoftwareFullName)] = "软件全称",
        [nameof(ApplicantInfo.SoftwareAbbreviation)] = "软件简称",
        [nameof(ApplicantInfo.VersionNumber)] = "版本号",
        [nameof(ApplicantInfo.CopyrightOwner)] = "著作权人",
        [nameof(ApplicantInfo.OwnerType)] = "著作权人类别",
        [nameof(ApplicantInfo.SoftwareSource)] = "软件来源",
        [nameof(ApplicantInfo.ModificationDescription)] = "修改说明",
        [nameof(ApplicantInfo.OriginalRegistrationNumber)] = "原登记号",
        [nameof(ApplicantInfo.DevelopmentMode)] = "开发方式",
        [nameof(ApplicantInfo.RightsAcquisitionMethod)] = "权利取得方式",
        [nameof(ApplicantInfo.RightsScope)] = "权利范围",
        [nameof(ApplicantInfo.PartialRightsDescription)] = "部分权利权项说明",
        [nameof(ApplicantInfo.CompletionDate)] = "开发完成日期",
        [nameof(ApplicantInfo.FirstPublicationStatus)] = "发表状态",
        [nameof(ApplicantInfo.FirstPublicationDate)] = "首次发表日期",
        [nameof(ApplicantInfo.FirstPublicationPlace)] = "首次发表地点",
        [nameof(ApplicantInfo.DevelopmentEnvironment)] = "硬件环境",
        [nameof(ApplicantInfo.RuntimeEnvironment)] = "软件环境",
        [nameof(ApplicantInfo.ProgrammingLanguages)] = "编程语言",
        [nameof(ApplicantInfo.SourceProgramAmount)] = "源程序量",
        [nameof(ApplicantInfo.DepositMode)] = "鉴别材料交存方式",
        [nameof(ApplicantInfo.ApplicationHandlingMethod)] = "申请办理方式",
        [nameof(ApplicantInfo.ClassificationCode)] = "分类号",
        [nameof(ApplicantInfo.FunctionSummary)] = "功能简介"
    };

    /// <summary>取字段的中文标签。</summary>
    public static string LabelOfField(string field) =>
        FieldLabels.TryGetValue(field, out var label) ? label : field;

    /// <summary>按字段名取选项型字段定义。</summary>
    public static ApplicantOptionField? Find(string field) =>
        FieldMap.TryGetValue(field, out var found) ? found : null;

    /// <summary>取某字段的候选选项。</summary>
    public static IReadOnlyList<ApplicantOption> OptionsOf(string field) =>
        Find(field)?.Options ?? [];

    /// <summary>把任意写法归一化为规范选项码；无法识别时返回 null。</summary>
    public static string? Normalize(string field, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim();
        var options = OptionsOf(field);
        if (options.Count == 0)
        {
            return null;
        }

        foreach (var option in options)
        {
            if (string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase))
            {
                return option.Value;
            }
        }

        foreach (var option in options)
        {
            if (string.Equals(option.Label, value, StringComparison.OrdinalIgnoreCase))
            {
                return option.Value;
            }

            foreach (var alias in option.Aliases)
            {
                if (string.Equals(alias, value, StringComparison.OrdinalIgnoreCase))
                {
                    return option.Value;
                }
            }
        }

        return null;
    }

    /// <summary>取值是否在该字段的合法候选之内。</summary>
    public static bool IsValid(string field, string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        OptionsOf(field).Any(o => string.Equals(o.Value, value.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>取选项的中文标签；不是合法选项时返回 null。</summary>
    public static string? LabelOf(string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return OptionsOf(field)
            .FirstOrDefault(o => string.Equals(o.Value, value.Trim(), StringComparison.OrdinalIgnoreCase))
            ?.Label;
    }

    /// <summary>取选项的补充说明；不是合法选项时返回 null。</summary>
    public static string? NoteOf(string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return OptionsOf(field)
            .FirstOrDefault(o => string.Equals(o.Value, value.Trim(), StringComparison.OrdinalIgnoreCase))
            ?.Note;
    }
}
