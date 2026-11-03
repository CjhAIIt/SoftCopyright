using System.Text.RegularExpressions;

namespace SoftCopyright.ProjectScan.Files;

/// <summary>疑似凭据检测：命中的文件不会进入证据包，也不会写入普通日志。</summary>
public static partial class SecretDetector
{
    private static readonly string[] SensitiveFilePatterns =
    [
        ".env", ".env.*", "*.pem", "*.key", "id_rsa*", "id_dsa*", "id_ecdsa*", "id_ed25519*",
        "*.pfx", "*.p12", "*.keystore", "credentials*.json", "*credentials*.json", "*.npmrc", ".pypirc", "*.kdbx",
        "service-account*.json", "*service-account*.json", "appsettings.*.json", "*.netrc", "master.key"
    ];

    [GeneratedRegex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----", RegexOptions.IgnoreCase)]
    private static partial Regex PrivateKeyPattern();

    [GeneratedRegex(@"\bAKIA[0-9A-Z]{16}\b")]
    private static partial Regex AwsKeyPattern();

    [GeneratedRegex(@"\bgh[pousr]_[A-Za-z0-9]{20,}\b")]
    private static partial Regex GitHubTokenPattern();

    [GeneratedRegex(@"\bsk-[A-Za-z0-9]{20,}\b")]
    private static partial Regex OpenAiKeyPattern();

    [GeneratedRegex(@"(?i)\b(api[_-]?key|secret[_-]?key|access[_-]?token|client[_-]?secret|password|passwd|private[_-]?key)\b\s*[:=]\s*[""']?[A-Za-z0-9_\-\.\/\+]{8,}")]
    private static partial Regex CredentialAssignmentPattern();

    /// <summary>文件名是否疑似凭据文件。</summary>
    public static bool IsSensitiveFileName(string relativePath)
    {
        var fileName = Path.GetFileName(relativePath);
        foreach (var pattern in SensitiveFilePatterns)
        {
            if (Matches(fileName, pattern))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>内容是否疑似包含凭据。</summary>
    public static bool ContainsSecretContent(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        return PrivateKeyPattern().IsMatch(text)
               || AwsKeyPattern().IsMatch(text)
               || GitHubTokenPattern().IsMatch(text)
               || OpenAiKeyPattern().IsMatch(text)
               || CredentialAssignmentPattern().IsMatch(text);
    }

    private static bool Matches(string fileName, string pattern)
    {
        var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(fileName, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
