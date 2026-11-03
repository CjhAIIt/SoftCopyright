using Microsoft.Extensions.Options;
using SoftCopyright.Domain.Options;

namespace SoftCopyright.ProjectScan.Files;

/// <summary>排除规则：区分「可配置排除」与「不可取消的安全排除」。</summary>
public static class ExclusionRules
{
    /// <summary>可配置排除（用户可在分析前调整）。</summary>
    public static (bool Excluded, string? Reason) EvaluateUserExclusions(
        string relativePath,
        ScanOptions options,
        IReadOnlyCollection<string> userExcludedDirectories)
    {
        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length <= 1)
        {
            return (false, null);
        }

        var excludedDirectories = new HashSet<string>(options.DefaultExcludedDirectories, StringComparer.OrdinalIgnoreCase);
        foreach (var custom in userExcludedDirectories)
        {
            if (!string.IsNullOrWhiteSpace(custom))
            {
                excludedDirectories.Add(custom.Trim());
            }
        }

        foreach (var segment in segments[..^1])
        {
            if (excludedDirectories.Contains(segment))
            {
                return (true, $"目录 {segment} 在排除规则中（依赖/构建产物）");
            }
        }

        var fileName = segments[^1];
        foreach (var pattern in options.DefaultExcludedFilePatterns)
        {
            if (Matches(fileName, pattern))
            {
                return (true, $"文件匹配排除规则 {pattern}");
            }
        }

        return (false, null);
    }

    private static bool Matches(string fileName, string pattern)
    {
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        return System.Text.RegularExpressions.Regex.IsMatch(
            fileName,
            regex,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }
}
