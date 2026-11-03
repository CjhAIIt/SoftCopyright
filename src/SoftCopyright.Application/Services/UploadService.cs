using Microsoft.Extensions.Logging;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Application.Services;

/// <summary>上传用例：接收压缩包并安全解包到案例隔离目录。</summary>
public sealed class UploadService(
    IRepository<ApplicationCase> cases,
    IFileStore fileStore,
    ISafeArchiveExtractor extractor,
    ICaseWorkflow workflow,
    ILogger<UploadService> logger) : ICaseUploadService
{
    private const string SecurityNote =
        "导入过程仅做只读静态解析：不会执行仓库中的任何脚本、构建或安装命令；" +
        "疑似凭据文件会被排除且不会发送给模型；解压路径已做穿越与压缩炸弹防护。";

    public async Task<UploadSummary> UploadAsync(Guid caseId, string fileName, Stream content, CancellationToken ct)
    {
        var caseEntity = await cases.FindAsync(caseId, ct);
        if (caseEntity is null || caseEntity.IsDeleted)
        {
            throw new NotFoundException("申请案例不存在。");
        }

        if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new InputValidationException("目前只支持上传 .zip 格式的代码仓库压缩包。");
        }

        await fileStore.SaveUploadAsync(caseId, fileName, content, ct);

        var uploadDirectory = fileStore.GetUploadDirectory(caseId);
        var archivePath = Path.Combine(uploadDirectory, Path.GetFileName(fileName));

        var extractedRoot = fileStore.GetExtractedRoot(caseId);
        if (Directory.Exists(extractedRoot))
        {
            Directory.Delete(extractedRoot, recursive: true);
        }

        Directory.CreateDirectory(extractedRoot);

        ArchiveExtractionResult result;
        try
        {
            result = await extractor.ExtractAsync(archivePath, extractedRoot, ct);
        }
        catch (ArchiveRejectedException ex)
        {
            caseEntity.FailureReason = ex.Message;
            caseEntity.StatusMessage = $"上传被拒绝：{ex.Message}";
            await cases.SaveChangesAsync(ct);
            throw new InputValidationException(ex.Message);
        }

        caseEntity.UploadedFileName = fileName;
        caseEntity.FailureReason = null;
        caseEntity.UpdatedAt = DateTimeOffset.UtcNow;

        if (workflow.CanTransition(caseEntity.Stage, CaseStage.Uploaded))
        {
            caseEntity.Stage = CaseStage.Uploaded;
            caseEntity.StageUpdatedAt = caseEntity.UpdatedAt;
        }

        caseEntity.StatusMessage = $"已导入 {result.EntryCount} 个压缩包条目，解压后 {result.TotalBytes} 字节，等待执行扫描。";
        await cases.SaveChangesAsync(ct);

        var extractedCount = Directory.Exists(extractedRoot)
            ? Directory.EnumerateFiles(extractedRoot, "*", SearchOption.AllDirectories).Count()
            : 0;

        logger.LogInformation("案例 {CaseId} 完成导入：{Entries} 个条目", caseId, result.EntryCount);

        return new UploadSummary(
            fileName,
            result.EntryCount,
            result.TotalBytes,
            extractedCount,
            result.SkippedEntries,
            result.Warnings,
            SecurityNote);
    }
}
