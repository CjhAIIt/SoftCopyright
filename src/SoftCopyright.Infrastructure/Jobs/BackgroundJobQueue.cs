using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Infrastructure.Data;

namespace SoftCopyright.Infrastructure.Jobs;

/// <summary>基于数据库表的后台任务队列（不使用仅在内存中的队列）。</summary>
public sealed class BackgroundJobQueue(
    SoftCopyrightDbContext db,
    ILogger<BackgroundJobQueue> logger) : IBackgroundJobQueue
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(10);

    public async Task<Guid> EnqueueAsync(Guid caseId, JobType type, string payloadJson, CancellationToken ct)
    {
        var job = new BackgroundJob
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            Type = type,
            Status = JobStatus.Pending,
            ProgressPercent = 0,
            Message = "等待执行",
            PayloadJson = payloadJson,
            CreatedAt = DateTimeOffset.UtcNow,
            NextRunAt = DateTimeOffset.UtcNow
        };

        db.Jobs.Add(job);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("已创建后台任务 {JobId}（类型 {JobType}，案例 {CaseId}）", job.Id, type, caseId);
        return job.Id;
    }

    public async Task<IReadOnlyList<BackgroundJob>> ListByCaseAsync(Guid caseId, CancellationToken ct) =>
        await db.Jobs.AsNoTracking().Where(j => j.CaseId == caseId)
            .OrderByDescending(j => j.CreatedAt).ToListAsync(ct);

    public async Task<BackgroundJob?> FindAsync(Guid jobId, CancellationToken ct) =>
        await db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId, ct);

    public async Task<BackgroundJob?> ClaimNextAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var candidate = await db.Jobs.AsNoTracking()
            .Where(j => j.Status == JobStatus.Pending && (j.NextRunAt == null || j.NextRunAt <= now))
            .OrderBy(j => j.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (candidate is null)
        {
            return null;
        }

        // 原子认领：只有状态仍为 Pending 的那一次更新才生效。
        // 使用反引号包裹标识符，兼容 MySQL 与 SQLite；时间参数使用 UTC DateTime，与列的存储类型一致。
        var startedAt = now.UtcDateTime;
        var leaseUntil = now.Add(LeaseDuration).UtcDateTime;

        var updated = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE `BackgroundJobs`
             SET `Status` = 'Running',
                 `StartedAt` = {startedAt},
                 `LeaseUntil` = {leaseUntil},
                 `AttemptCount` = `AttemptCount` + 1,
                 `Message` = '任务已开始'
             WHERE `Id` = {candidate.Id} AND `Status` = 'Pending'
             """, ct);

        if (updated == 0)
        {
            return null;
        }

        candidate.Status = JobStatus.Running;
        candidate.StartedAt = now;
        candidate.LeaseUntil = now.Add(LeaseDuration);
        candidate.AttemptCount += 1;
        candidate.Message = "任务已开始";
        return candidate;
    }

    public async Task<bool> UpdateAsync(Guid jobId, Action<BackgroundJob> mutate, CancellationToken ct)
    {
        var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null)
        {
            return false;
        }

        mutate(job);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task RequeueExpiredLeasesAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var expired = await db.Jobs.Where(j => j.Status == JobStatus.Running && j.LeaseUntil != null && j.LeaseUntil < now).ToListAsync(ct);

        foreach (var job in expired)
        {
            if (job.AttemptCount >= job.MaxAttempts)
            {
                job.Status = JobStatus.Failed;
                job.ErrorMessage = "任务因进程中断超过最大重试次数而失败。";
                job.FinishedAt = now;
                job.ProgressPercent = 0;
            }
            else
            {
                job.Status = JobStatus.Pending;
                job.Message = "上一次执行中断，已重新排队";
                job.NextRunAt = now.AddSeconds(BackoffSeconds(job.AttemptCount));
            }
        }

        if (expired.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogWarning("回收了 {Count} 个租约过期的后台任务", expired.Count);
        }
    }

    internal static int BackoffSeconds(int attempt) => attempt switch
    {
        <= 1 => 5,
        2 => 15,
        _ => 45
    };
}
