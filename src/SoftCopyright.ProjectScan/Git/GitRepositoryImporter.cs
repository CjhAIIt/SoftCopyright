using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Domain.Git;
using SoftCopyright.Domain.Options;

namespace SoftCopyright.ProjectScan.Git;

/// <summary>
/// Git 仓库克隆器。
///
/// 安全约束：
/// - 只调用 git 自身，**不执行仓库中的任何脚本**（克隆后立即删除 .git，禁用钩子与符号链接）；
/// - 关闭 LFS 过滤器（.gitattributes 可触发本地已安装的过滤器进程）；
/// - 禁止 ext:: 等可执行类传输协议；
/// - 参数全部使用 ArgumentList 传入，不经过 shell 解析，避免参数注入；
/// - 克隆结果落在调用方给定的隔离目录内，失败即清理。
/// </summary>
public sealed class GitRepositoryImporter(
    IOptions<GitImportOptions> options,
    ILogger<GitRepositoryImporter> logger) : IGitRepositoryImporter
{
    private readonly GitImportOptions _options = options.Value;

    public async Task<GitImportResult> CloneAsync(
        GitRepositoryUrl repository,
        string destinationRoot,
        GitImportRequest request,
        CancellationToken ct)
    {
        var warnings = new List<string>();

        if (!Directory.Exists(destinationRoot))
        {
            Directory.CreateDirectory(destinationRoot);
        }

        // 空钩子目录放在目标目录之外：git clone 要求目标目录为空。
        var emptyHooksDirectory = Path.Combine(
            Path.GetTempPath(),
            "sc-empty-hooks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyHooksDirectory);

        var arguments = new List<string> { "clone" };

        if (_options.Shallow)
        {
            arguments.Add("--depth");
            arguments.Add("1");
            arguments.Add("--single-branch");
        }

        arguments.Add("--no-tags");
        arguments.Add("--config");
        arguments.Add($"core.hooksPath={emptyHooksDirectory}");
        arguments.Add("--config");
        arguments.Add("core.symlinks=false");
        arguments.Add("--config");
        arguments.Add("filter.lfs.smudge=");
        arguments.Add("--config");
        arguments.Add("filter.lfs.process=");
        arguments.Add("--config");
        arguments.Add("filter.lfs.required=false");
        arguments.Add("--config");
        arguments.Add("protocol.ext.allow=never");
        arguments.Add("--config");
        arguments.Add("protocol.file.allow=user");

        if (!string.IsNullOrWhiteSpace(request.Branch))
        {
            arguments.Add("--branch");
            arguments.Add(request.Branch.Trim());
        }

        arguments.Add(repository.CloneUrl);
        arguments.Add(destinationRoot);

        var startInfo = new ProcessStartInfo
        {
            FileName = ResolveGitExecutable(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var accessToken = ResolveAccessToken(repository);
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            startInfo.Environment["GIT_ASKPASS"] = "echo";
            startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
            startInfo.Environment["GIT_USERNAME"] = "x-access-token";
            startInfo.Environment["GIT_PASSWORD"] = accessToken;
        }

        // Git 2.45 起，克隆时显式指定 core.hooksPath 会被克隆保护拒绝。
        // 我们明确把钩子目录指向空目录（确保不执行任何钩子），因此关闭该保护。
        startInfo.Environment["GIT_CLONE_PROTECTION_ACTIVE"] = "false";

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return new GitImportResult(false, $"无法启动 git：{ex.Message}", null, 0, 0, warnings);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(30, _options.TimeoutSeconds)));

        var stdoutTask = ReadLimitedAsync(process.StandardOutput, 64 * 1024);
        var stderrTask = ReadLimitedAsync(process.StandardError, 64 * 1024);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            var reason = ct.IsCancellationRequested ? "已取消" : $"克隆超时（超过 {_options.TimeoutSeconds} 秒）";
            return new GitImportResult(false, reason, null, 0, 0, warnings);
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            var detail = FirstMeaningfulLine(stderr) ?? FirstMeaningfulLine(stdout) ?? $"git 返回退出码 {process.ExitCode}";
            return new GitImportResult(false, $"克隆失败：{detail}", null, 0, 0, warnings);
        }

        // 指定提交号时再切到该提交（浅克隆默认只有 HEAD）。
        if (!string.IsNullOrWhiteSpace(request.Commit))
        {
            var checkout = await RunGitAsync(
                destinationRoot,
                ["checkout", "--detach", request.Commit.Trim()],
                timeoutCts.Token);

            if (!checkout.Success)
            {
                return new GitImportResult(false, $"切换到提交 {request.Commit} 失败：{checkout.Message}", null, 0, 0, warnings);
            }
        }

        var resolvedCommit = await ReadHeadCommitAsync(destinationRoot, timeoutCts.Token);

        // 删除 .git：材料只需要工作区快照，也不应把仓库内部对象带进证据。
        var gitDirectory = Path.Combine(destinationRoot, ".git");
        if (Directory.Exists(gitDirectory))
        {
            SafeFileSystem.DeleteDirectory(gitDirectory);
        }

        if (Directory.Exists(emptyHooksDirectory))
        {
            SafeFileSystem.DeleteDirectory(emptyHooksDirectory);
        }

        var (fileCount, totalBytes) = MeasureDirectory(destinationRoot);
        var limitBytes = (long)_options.MaxWorkingTreeSizeMb * 1024 * 1024;
        if (totalBytes > limitBytes)
        {
            SafeFileSystem.DeleteDirectory(destinationRoot);
            return new GitImportResult(
                false,
                $"仓库内容超过 {_options.MaxWorkingTreeSizeMb} MB 上限，已放弃导入。",
                resolvedCommit,
                0,
                totalBytes,
                warnings);
        }

        warnings.Add("已按只读方式导入仓库快照：克隆完成后删除了 .git 目录，未执行任何仓库脚本。");

        return new GitImportResult(true, null, resolvedCommit, fileCount, totalBytes, warnings);
    }

    private string ResolveGitExecutable()
    {
        if (!string.IsNullOrWhiteSpace(_options.GitExecutablePath) && File.Exists(_options.GitExecutablePath))
        {
            return _options.GitExecutablePath;
        }

        return OperatingSystem.IsWindows() ? "git.exe" : "git";
    }

    private string? ResolveAccessToken(GitRepositoryUrl repository)
    {
        if (repository.IsLocalFile || string.IsNullOrWhiteSpace(_options.AccessTokenEnvironmentVariable))
        {
            return null;
        }

        return Environment.GetEnvironmentVariable(_options.AccessTokenEnvironmentVariable);
    }

    private async Task<(bool Success, string? Message)> RunGitAsync(string workingDirectory, IReadOnlyList<string> args, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ResolveGitExecutable(),
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in args)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }

        await process.WaitForExitAsync(ct);
        var error = await process.StandardError.ReadToEndAsync(ct);
        return process.ExitCode == 0
            ? (true, null)
            : (false, FirstMeaningfulLine(error) ?? $"git 返回退出码 {process.ExitCode}");
    }

    private async Task<string?> ReadHeadCommitAsync(string workingDirectory, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ResolveGitExecutable(),
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("rev-parse");
        startInfo.ArgumentList.Add("HEAD");

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            var commit = output.Trim();
            return process.ExitCode == 0 && commit.Length > 0 ? commit : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "读取 HEAD 提交号失败");
            return null;
        }
    }

    private static async Task<string> ReadLimitedAsync(StreamReader reader, int maxChars)
    {
        var builder = new StringBuilder();
        var buffer = new char[4096];
        while (builder.Length < maxChars)
        {
            var read = await reader.ReadAsync(buffer, 0, Math.Min(buffer.Length, maxChars - builder.Length));
            if (read <= 0)
            {
                break;
            }

            builder.Append(buffer, 0, read);
        }

        return builder.ToString();
    }

    private static string? FirstMeaningfulLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var lines = text.Split('\n');
        var fallback = (string?)null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            // git 的进度信息（Cloning into ...）不是错误原因。
            if (trimmed.StartsWith("Cloning into", StringComparison.OrdinalIgnoreCase))
            {
                fallback ??= trimmed;
                continue;
            }

            if (trimmed.StartsWith("fatal:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("error:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("remote:", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Length > 300 ? trimmed[..300] : trimmed;
            }

            fallback ??= trimmed.Length > 300 ? trimmed[..300] : trimmed;
        }

        return fallback;
    }

    private static (int Files, long Bytes) MeasureDirectory(string root)
    {
        var count = 0;
        long bytes = 0;

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            try
            {
                bytes += new FileInfo(file).Length;
                count++;
            }
            catch (IOException)
            {
                // 忽略无法读取的文件。
            }
        }

        return (count, bytes);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
            // 忽略结束进程时的异常。
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // 忽略清理失败。
        }
        catch (UnauthorizedAccessException)
        {
            // 忽略清理失败。
        }
    }
}
