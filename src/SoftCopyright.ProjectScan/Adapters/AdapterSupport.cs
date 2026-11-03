using System.Security.Cryptography;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.ProjectScan.Adapters;

/// <summary>适配器公用工具。</summary>
public static class AdapterSupport
{
    /// <summary>构造一条证据（内容截断，不保存全文）。</summary>
    public static EvidenceEntry Evidence(
        ProjectFileIndex index,
        string relativePath,
        string evidenceType,
        string excerpt,
        EvidenceSource source = EvidenceSource.Adapter,
        int? startLine = null,
        int? endLine = null)
    {
        var sha = index.Files.FirstOrDefault(f => string.Equals(f.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase))?.Sha256
                  ?? string.Empty;
        return new EvidenceEntry(relativePath, startLine, endLine, evidenceType, sha, Truncate(excerpt), source);
    }

    /// <summary>截断摘要，避免把大段源码写入证据表。</summary>
    public static string Truncate(string text, int maxLength = 500)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "…";
    }

    /// <summary>计算字符串的 SHA-256（用于没有文件条目的内联内容）。</summary>
    public static string HashText(string text) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    /// <summary>从文本中提取第一行非空内容。</summary>
    public static string FirstNonEmptyLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0)
            {
                return trimmed;
            }
        }

        return string.Empty;
    }
}
