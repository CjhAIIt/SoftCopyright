using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Infrastructure.Jobs;

namespace SoftCopyright.Infrastructure.Jobs;

/// <summary>后台任务执行器：轮询持久化任务表，串行执行，支持进度、失败原因与重试。</summary>
public sealed class BackgroundJobWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<BackgroundJobWorker> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("后台任务执行器已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var queue = scope.ServiceProvider.GetRequiredService<IBackgroundJobQueue>();
                await queue.RequeueExpiredLeasesAsync(stoppingToken);

                var job = await queue.ClaimNextAsync(stoppingToken);
                if (job is null)
                {
                    await Task.Delay(IdleDelay, stoppingToken);
                    continue;
                }

                await ExecuteJobAsync(job, scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "后台任务循环出现异常");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        logger.LogInformation("后台任务执行器已停止");
    }

    private async Task ExecuteJobAsync(BackgroundJob job, IServiceProvider services, CancellationToken ct)
    {
        var queue = services.GetRequiredService<IBackgroundJobQueue>();
        try
        {
            switch (job.Type)
            {
                case JobType.Scan:
                {
                    var scan = services.GetRequiredService<ICaseScanWorkflow>();
                    await scan.ScanAsync(job.CaseId, p => ReportProgress(queue, job.Id, p.Percent, p.Message, ct), ct);
                    break;
                }

                case JobType.Analyze:
                {
                    var analyze = services.GetRequiredService<ICaseAnalyzeWorkflow>();
                    await analyze.AnalyzeAsync(job.CaseId, p => ReportProgress(queue, job.Id, p.Percent, p.Message, ct), ct);
                    break;
                }

                case JobType.Generate:
                {
                    var generate = services.GetRequiredService<ICaseGenerationWorkflow>();
                    await generate.GenerateAsync(job.CaseId, p => ReportProgress(queue, job.Id, p.Percent, p.Message, ct), ct);
                    break;
                }

                default:
                    throw new InvalidOperationException($"不支持的任务类型：{job.Type}");
            }

            await queue.UpdateAsync(job.Id, j =>
            {
                j.Status = JobStatus.Succeeded;
                j.ProgressPercent = 100;
                j.Message = "任务已完成";
                j.FinishedAt = DateTimeOffset.UtcNow;
                j.ErrorMessage = null;
                j.LeaseUntil = null;
            }, ct);
        }
        catch (Exception ex)
        {
            var message = Summarize(ex);
            var attempt = job.AttemptCount;
            var maxAttempts = job.MaxAttempts;

            if (attempt < maxAttempts)
            {
                var delay = BackgroundJobQueue.BackoffSeconds(attempt);
                await queue.UpdateAsync(job.Id, j =>
                {
                    j.Status = JobStatus.Pending;
                    j.Message = $"执行失败，{delay} 秒后重试";
                    j.ErrorMessage = message;
                    j.NextRunAt = DateTimeOffset.UtcNow.AddSeconds(delay);
                    j.LeaseUntil = null;
                }, ct);

                logger.LogWarning(ex, "后台任务 {JobId} 第 {Attempt} 次执行失败，将重试", job.Id, attempt);
            }
            else
            {
                await queue.UpdateAsync(job.Id, j =>
                {
                    j.Status = JobStatus.Failed;
                    j.ProgressPercent = 0;
                    j.Message = "任务失败";
                    j.ErrorMessage = message;
                    j.FinishedAt = DateTimeOffset.UtcNow;
                    j.LeaseUntil = null;
                }, ct);

                logger.LogError(ex, "后台任务 {JobId} 已超过最大重试次数，标记为失败", job.Id);
            }
        }
    }

    private static async Task ReportProgress(IBackgroundJobQueue queue, Guid jobId, int percent, string message, CancellationToken ct)
    {
        var clamped = Math.Clamp(percent, 0, 100);
        await queue.UpdateAsync(jobId, j =>
        {
            j.ProgressPercent = clamped;
            j.Message = message;
        }, ct);
    }

    /// <summary>异常摘要：只保留类型与消息，不输出堆栈，避免把源码正文写进日志。</summary>
    private static string Summarize(Exception ex) =>
        ex is ArchiveRejectedException
            ? ex.Message
            : $"{ex.GetType().Name}: {ex.Message}";
}
