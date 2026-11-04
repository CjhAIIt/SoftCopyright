using SoftCopyright.Domain.Git;

namespace SoftCopyright.Domain.Contracts;

/// <summary>Git 导入请求。</summary>
public sealed record GitImportRequest(string Url, string? Branch = null, string? Commit = null);

/// <summary>Git 导入结果。</summary>
public sealed record GitImportResult(
    bool Success,
    string? Error,
    string? ResolvedCommit,
    int FileCount,
    long TotalBytes,
    IReadOnlyList<string> Warnings);

/// <summary>Git 仓库克隆器：只执行 git 自身，不执行仓库中的任何内容。</summary>
public interface IGitRepositoryImporter
{
    Task<GitImportResult> CloneAsync(
        GitRepositoryUrl repository,
        string destinationRoot,
        GitImportRequest request,
        CancellationToken ct);
}

/// <summary>Git 导入用例服务。</summary>
public interface IGitImportService
{
    Task<GitImportSummary> ImportAsync(Guid caseId, GitImportRequest request, CancellationToken ct);
}

/// <summary>Git 导入摘要。</summary>
public sealed record GitImportSummary(
    string Url,
    string? Commit,
    int FileCount,
    IReadOnlyList<string> Warnings);
