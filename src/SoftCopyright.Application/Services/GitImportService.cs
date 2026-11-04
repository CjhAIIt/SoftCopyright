using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoftCopyright.Application.Dtos;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Domain.Git;
using SoftCopyright.Domain.Options;

namespace SoftCopyright.Application.Services;

/// <summary>
/// Git 仓库导入用例：校验地址 → 只读克隆到案例隔离目录 → 记录来源 → 触发扫描。
/// </summary>
public sealed class GitImportService(
    IRepository<ApplicationCase> cases,
    IRepository<ProjectFile> projectFiles,
    IGitRepositoryImporter importer,
    IBackgroundJobQueue jobQueue,
    IFileStore fileStore,
    ICaseWorkflow workflow,
    IOptions<GitImportOptions> options,
    ILogger<GitImportService> logger) : IGitImportService
{
    private readonly GitImportOptions _options = options.Value;

    public async Task<GitImportSummary> ImportAsync(Guid caseId, GitImportRequest request, CancellationToken ct)
    {
        var caseEntity = await cases.FindAsync(caseId, ct);
        if (caseEntity is null || caseEntity.IsDeleted)
        {
            throw new NotFoundException("申请案例不存在。");
        }

        if (!GitRepositoryUrl.TryParse(request.Url, _options, out var repository, out var error) || repository is null)
        {
            throw new InputValidationException(error ?? "仓库地址不合法。");
        }

        // 清空上一份解包内容，避免两次导入混在一起。
        var extractedRoot = fileStore.GetExtractedRoot(caseId);
        SafeFileSystem.DeleteDirectory(extractedRoot);

        Directory.CreateDirectory(extractedRoot);

        logger.LogInformation("开始克隆仓库 {Url}（案例 {CaseId}）", repository.CloneUrl, caseId);

        var result = await importer.CloneAsync(repository, extractedRoot, request, ct);

        if (!result.Success)
        {
            TryClearDirectory(extractedRoot);
            caseEntity.FailureReason = result.Error;
            caseEntity.StatusMessage = $"Git 导入失败：{result.Error}";
            await cases.SaveChangesAsync(ct);
            throw new InputValidationException(result.Error ?? "仓库克隆失败。");
        }

        // 旧的扫描结果作废，重新扫描。
        var staleFiles = await projectFiles.ListAsync(f => f.CaseId == caseId, ct);
        foreach (var file in staleFiles)
        {
            projectFiles.Remove(file);
        }

        await projectFiles.SaveChangesAsync(ct);

        caseEntity.SourceRepositoryUrl = repository.CloneUrl;
        caseEntity.SourceCommit = result.ResolvedCommit;
        caseEntity.UploadedFileName = null;
        caseEntity.FailureReason = null;
        caseEntity.UpdatedAt = DateTimeOffset.UtcNow;

        if (workflow.CanTransition(caseEntity.Stage, CaseStage.Uploaded))
        {
            caseEntity.Stage = CaseStage.Uploaded;
            caseEntity.StageUpdatedAt = caseEntity.UpdatedAt;
        }

        caseEntity.StatusMessage =
            $"已从 {repository.CloneUrl} 导入 {result.FileCount} 个文件"
            + (result.ResolvedCommit is null ? string.Empty : $"（提交 {Short(result.ResolvedCommit)}）")
            + "，正在扫描。";

        await cases.SaveChangesAsync(ct);

        // Git 导入后直接排队扫描，省去一次手动操作。
        await jobQueue.EnqueueAsync(caseId, JobType.Scan, "{}", ct);

        return new GitImportSummary(repository.CloneUrl, result.ResolvedCommit, result.FileCount, result.Warnings);
    }

    private static string Short(string? commit) =>
        commit is null ? string.Empty : commit.Length <= 8 ? commit : commit[..8];

    private static void TryClearDirectory(string path)
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
