using System.Text.RegularExpressions;
using SoftCopyright.Domain;

namespace SoftCopyright.Application.Validation;

/// <summary>申请信息中一个字段的检查问题。</summary>
public sealed record ApplicantOptionIssue(string Field, string Label, string Message, bool Blocking);

/// <summary>
/// 依据选项目录检查申请信息的取值合法性。
///
/// 服务层用它拒绝非法输入（值域与格式），校验规则集用它出具正式校验报告（缺失、条件必填、跨字段约束）。
/// 两者共用同一份判定，避免界面提示与校验报告口径不一致。
/// </summary>
public static partial class ApplicantOptionChecker
{
    /// <summary>检查全部选项型字段、条件必填、跨字段约束与分类号格式。</summary>
    /// <param name="read">按字段名读取当前值；字段不存在时返回 null。</param>
    public static IReadOnlyList<ApplicantOptionIssue> Check(Func<string, string?> read)
    {
        ArgumentNullException.ThrowIfNull(read);

        var issues = new List<ApplicantOptionIssue>();

        foreach (var field in ApplicantOptionCatalog.All)
        {
            var raw = read(field.Field);

            if (string.IsNullOrWhiteSpace(raw))
            {
                if (field.Required)
                {
                    issues.Add(new ApplicantOptionIssue(
                        field.Field,
                        field.Label,
                        $"「{field.Label}」是必填选项，尚未选择。",
                        Blocking: false));
                }

                continue;
            }

            if (ApplicantOptionCatalog.Normalize(field.Field, raw) is null)
            {
                issues.Add(new ApplicantOptionIssue(
                    field.Field,
                    field.Label,
                    $"「{field.Label}」的取值「{raw.Trim()}」不在官方允许的候选项之内。",
                    Blocking: true));
            }
        }

        foreach (var condition in ApplicantOptionCatalog.Conditions)
        {
            var trigger = read(condition.Field);
            if (!string.Equals(trigger?.Trim(), condition.Value, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(read(condition.RequiredField)))
            {
                issues.Add(new ApplicantOptionIssue(
                    condition.RequiredField,
                    ApplicantOptionCatalog.LabelOfField(condition.RequiredField),
                    condition.Reason,
                    Blocking: false));
            }
        }

        foreach (var constraint in ApplicantOptionCatalog.Constraints)
        {
            var trigger = read(constraint.Field);
            if (!string.Equals(trigger?.Trim(), constraint.Value, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var related = read(constraint.RelatedField);
            if (string.IsNullOrWhiteSpace(related))
            {
                // 缺失由必填规则单独报，此处不重复。
                continue;
            }

            var normalized = ApplicantOptionCatalog.Normalize(constraint.RelatedField, related);
            if (normalized is null)
            {
                // 取值本身已越界，由值域规则单独报，此处不重复。
                continue;
            }

            if (!constraint.AllowedValues.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                issues.Add(new ApplicantOptionIssue(
                    constraint.RelatedField,
                    ApplicantOptionCatalog.LabelOfField(constraint.RelatedField),
                    constraint.Reason,
                    Blocking: false));
            }
        }

        var classification = read(nameof(ApplicantInfo.ClassificationCode));
        if (!string.IsNullOrWhiteSpace(classification) && !ClassificationCodePattern().IsMatch(classification.Trim()))
        {
            issues.Add(new ApplicantOptionIssue(
                nameof(ApplicantInfo.ClassificationCode),
                ApplicantOptionCatalog.LabelOfField(nameof(ApplicantInfo.ClassificationCode)),
                "分类号应为 GB/T 13702 软件分类代码（5 位）与 GB/T 4754 国民经济行业分类代码（4 位）组成的「×××××-××××」形式。",
                Blocking: true));
        }

        return issues;
    }

    /// <summary>只检查会破坏数据完整性的问题（值域非法、格式非法）。</summary>
    public static IReadOnlyList<ApplicantOptionIssue> CheckBlocking(Func<string, string?> read) =>
        Check(read).Where(i => i.Blocking).ToList();

    [GeneratedRegex(@"^\d{5}-\d{4}$")]
    private static partial Regex ClassificationCodePattern();
}
