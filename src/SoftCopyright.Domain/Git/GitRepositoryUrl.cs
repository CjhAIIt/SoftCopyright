using System.Text.RegularExpressions;
using SoftCopyright.Domain.Options;

namespace SoftCopyright.Domain.Git;

/// <summary>
/// Git 仓库地址解析与安全校验。
/// 只接受 http(s) / ssh / git 协议，拒绝本地可执行类协议与以 - 开头的参数注入。
/// </summary>
public sealed partial record GitRepositoryUrl(
    string CloneUrl,
    string Host,
    string Owner,
    string Repository,
    bool IsLocalFile)
{
    /// <summary>把用户粘贴的地址规范化为可克隆地址。</summary>
    public static bool TryParse(
        string? input,
        GitImportOptions options,
        out GitRepositoryUrl? result,
        out string? error)
    {
        result = null;
        error = null;

        var text = input?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "请填写 Git 仓库地址。";
            return false;
        }

        if (text.Length > 500)
        {
            error = "仓库地址过长。";
            return false;
        }

        // 防止把参数当成 URL 传给 git（例如 --upload-pack=...）。
        if (text.StartsWith('-'))
        {
            error = "仓库地址不能以 - 开头。";
            return false;
        }

        if (text.Contains("..", StringComparison.Ordinal) && !text.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            error = "仓库地址不合法。";
            return false;
        }

        if (DangerousFragmentPattern().IsMatch(text))
        {
            error = "仓库地址包含不允许的内容。";
            return false;
        }

        // 本地路径 / file://
        if (IsLocalPath(text))
        {
            if (!options.AllowLocalFileUrls)
            {
                error = "出于安全考虑，当前不允许导入本地目录或 file:// 地址。";
                return false;
            }

            var localPath = text.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                ? new Uri(text).LocalPath
                : Path.GetFullPath(text);

            result = new GitRepositoryUrl(localPath, "localhost", string.Empty, Path.GetFileName(localPath.TrimEnd('/')), true);
            return true;
        }

        // scp 风格：git@host:owner/repo.git（带 "://" 的是普通 URL，不走这里）
        var scp = text.Contains("://", StringComparison.Ordinal)
            ? Match.Empty
            : ScpPattern().Match(text);
        if (scp.Success)
        {
            var host = scp.Groups["host"].Value;
            var path = scp.Groups["path"].Value;
            if (!TryParseOwnerRepository(path, out var scpOwner, out var scpRepository))
            {
                error = "无法从地址中识别仓库名，请使用 owner/repo 形式。";
                return false;
            }

            if (!IsHostAllowed(host, options, out error))
            {
                return false;
            }

            result = new GitRepositoryUrl(text, host, scpOwner, scpRepository, false);
            return true;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            error = "仓库地址格式不正确。";
            return false;
        }

        var scheme = uri.Scheme.ToLowerInvariant();
        if (scheme is not ("http" or "https" or "ssh" or "git"))
        {
            error = $"不支持的协议：{scheme}。仅支持 http/https/ssh/git。";
            return false;
        }

        var uriHost = uri.Host;
        if (!IsHostAllowed(uriHost, options, out error))
        {
            return false;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            error = "无法从地址中识别仓库名，请使用 owner/repo 形式。";
            return false;
        }

        var owner = segments[^2];
        var repository = segments[^1];
        if (repository.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            repository = repository[..^4];
        }

        if (owner.Length == 0 || repository.Length == 0)
        {
            error = "无法从地址中识别仓库名，请使用 owner/repo 形式。";
            return false;
        }

        // 保留完整命名空间路径（如 GitLab 的 group/sub/repo）。
        var fullPath = string.Join('/', segments);
        if (fullPath.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            fullPath = fullPath[..^4];
        }

        // 网页地址（https://github.com/owner/repo）统一转成克隆地址。
        var cloneUrl = scheme switch
        {
            "http" or "https" => $"{scheme}://{uriHost}/{fullPath}.git",
            _ => text
        };

        result = new GitRepositoryUrl(cloneUrl, uriHost, owner, repository, false);
        return true;
    }

    private static bool IsHostAllowed(string host, GitImportOptions options, out string? error)
    {
        error = null;

        if (options.AllowAnyHost)
        {
            return true;
        }

        if (options.AllowedHosts.Any(h => string.Equals(h, host, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        error = $"域名 {host} 不在允许列表中。当前允许：{string.Join("、", options.AllowedHosts)}。"
                + "如需导入其他平台，请在配置中调整 GitImport:AllowedHosts。";
        return false;
    }

    private static bool TryParseOwnerRepository(string path, out string owner, out string repository)
    {
        owner = string.Empty;
        repository = string.Empty;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return false;
        }

        owner = segments[^2];
        repository = segments[^1];
        if (repository.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            repository = repository[..^4];
        }

        return owner.Length > 0 && repository.Length > 0;
    }

    private static bool IsLocalPath(string text)
    {
        if (text.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (text.Contains("://", StringComparison.Ordinal))
        {
            return false;
        }

        // 形如 C:\path 或 /path
        return text.Contains('\\')
               || text.StartsWith("./", StringComparison.Ordinal)
               || text.StartsWith("../", StringComparison.Ordinal)
               || text.StartsWith("/", StringComparison.Ordinal)
               || (text.Length > 1 && text[1] == ':');
    }

    [GeneratedRegex(@"ext::|--upload-pack|--config|-c\s|--receive-pack|--exec", RegexOptions.IgnoreCase)]
    private static partial Regex DangerousFragmentPattern();

    [GeneratedRegex(@"^(?:[\w\.\-]+@)?(?<host>[\w\.\-]+):(?<path>.+)$")]
    private static partial Regex ScpPattern();
}
