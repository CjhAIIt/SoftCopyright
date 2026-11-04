using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using SoftCopyright.Application;
using SoftCopyright.Application.Contracts;
using SoftCopyright.Application.Dtos;
using SoftCopyright.Application.MaterialFlows;
using SoftCopyright.Application.Services;
using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;
using SoftCopyright.Domain.Options;

namespace SoftCopyright.Api.Endpoints;

/// <summary>申请案例相关 API。</summary>
public static class CaseEndpoints
{
    public static void MapSoftCopyrightApi(this WebApplication app)
    {
        // 两条材料流程各自独立的入口端点：类型由端点决定，不再由调用方传参选择。
        app.MapPost("/api/softcopyright/cases", async (CreateCaseRequest request, ICaseService cases, CancellationToken ct) =>
        {
            var entity = await cases.CreateAsync(request.Name, MaterialKind.SoftwareCopyright, ct);
            return Results.Created($"/api/cases/{entity.Id}", new CreateCaseResponse(entity.Id, entity.Name));
        });

        app.MapPost("/api/patent/cases", async (CreateCaseRequest request, ICaseService cases, CancellationToken ct) =>
        {
            var entity = await cases.CreateAsync(request.Name, MaterialKind.Patent, ct);
            return Results.Created($"/api/cases/{entity.Id}", new CreateCaseResponse(entity.Id, entity.Name));
        });

        // 流程描述：前端据此渲染入口页与工作台，两条流程的差异只在这里定义一次。
        app.MapGet("/api/flows", (MaterialFlowRegistry flows) =>
            Results.Ok(flows.All.Select(Mapping.ToDto).ToList()));

        app.MapGet("/api/flows/{kind:regex(^(SoftwareCopyright|Patent)$)}", (MaterialKind kind, MaterialFlowRegistry flows) =>
            Results.Ok(Mapping.ToDto(flows.Get(kind))));

        var group = app.MapGroup("/api/cases").RequireAuthorization();

        group.MapGet("/", async (ICaseService cases, CancellationToken ct) =>
        {
            var entities = await cases.ListAsync(ct);
            var service = (CaseService)cases;
            var items = new List<CaseSummaryDto>();
            foreach (var entity in entities)
            {
                items.Add(await service.ToSummaryAsync(entity, ct));
            }

            return Results.Ok(items);
        });

        group.MapGet("/by-kind/{kind:regex(^(SoftwareCopyright|Patent)$)}", async (MaterialKind kind, ICaseService cases, CancellationToken ct) =>
        {
            var entities = await cases.ListAsync(kind, ct);
            var service = (CaseService)cases;
            var items = new List<CaseSummaryDto>();
            foreach (var entity in entities)
            {
                items.Add(await service.ToSummaryAsync(entity, ct));
            }

            return Results.Ok(items);
        });

        group.MapGet("/{id:guid}", async (Guid id, ICaseService cases, ICaseWorkflow workflow, IBackgroundJobQueue queue, IConfirmationService confirmationService, CancellationToken ct) =>
        {
            var entity = await cases.GetAsync(id, ct);
            if (entity is null)
            {
                return Results.NotFound(new { error = new { code = "not_found", message = "申请案例不存在。" } });
            }

            var service = (CaseService)cases;
            var summary = await service.ToSummaryAsync(entity, ct);
            var jobs = await queue.ListByCaseAsync(id, ct);
            var confirmationStatuses = await confirmationService.ListAsync(id, ct);

            var scanSummary = ScanSummaryReader.Read(entity.ScanSummaryJson);

            return Results.Ok(new CaseDetailDto(
                summary,
                jobs.Select(Mapping.ToDto).ToList(),
                confirmationStatuses.Select(Mapping.ToDto).ToList(),
                scanSummary,
                entity.RulesVersion,
                entity.AnalyzerVersion,
                workflow.Pipeline.Select(Mapping.StageText).ToList(),
                workflow.Pipeline.Select(s => s.ToString()).ToList()));
        });

        group.MapDelete("/{id:guid}", async (Guid id, ICaseService cases, CancellationToken ct) =>
        {
            var service = (CaseService)cases;
            var entity = await service.GetAsync(id, ct);
            if (entity is null)
            {
                return Results.NotFound(new { error = new { code = "not_found", message = "申请案例不存在。" } });
            }

            await cases.DeleteAsync(id, ct);
            return Results.Ok(new DeleteCaseResponse(id, entity.Name, 0, 0, 0));
        });
        group.MapPost("/{id:guid}/upload", async (Guid id, HttpRequest request, ICaseUploadService upload, CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new { error = new { code = "validation", message = "请以 multipart/form-data 上传 ZIP 压缩包。" } });
            }

            var form = await request.ReadFormAsync(ct);
            var file = form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new { error = new { code = "validation", message = "未收到上传文件。" } });
            }

            await using var stream = file.OpenReadStream();
            var summary = await upload.UploadAsync(id, file.FileName, stream, ct);

            return Results.Ok(new UploadResultDto(
                summary.FileName,
                summary.EntryCount,
                summary.TotalBytes,
                summary.ExtractedFileCount,
                summary.SkippedEntries,
                summary.Warnings,
                summary.SecurityNote));
        });

        group.MapPost("/{id:guid}/import-git", async (Guid id, GitImportRequest request, IGitImportService gitImport, CancellationToken ct) =>
        {
            var summary = await gitImport.ImportAsync(id, request, ct);
            return Results.Ok(new GitImportResultDto(summary.Url, summary.Commit, summary.FileCount, summary.Warnings));
        });

        group.MapPost("/{id:guid}/scan", async (Guid id, ICaseService cases, IBackgroundJobQueue queue, CancellationToken ct) =>
        {
            var entity = await cases.GetAsync(id, ct);
            if (entity is null)
            {
                return Results.NotFound(new { error = new { code = "not_found", message = "申请案例不存在。" } });
            }

            var existing = await queue.ListByCaseAsync(id, ct);
            var running = existing.FirstOrDefault(j =>
                j.Type == JobType.Scan && j.Status is JobStatus.Pending or JobStatus.Running);
            if (running is not null)
            {
                return Results.Conflict(new { error = new { code = "conflict", message = "已有扫描任务在执行中。" }, jobId = running.Id });
            }

            var jobId = await queue.EnqueueAsync(id, JobType.Scan, "{}", ct);
            return Results.Accepted($"/api/cases/{id}/jobs", new { jobId });
        });

        group.MapGet("/{id:guid}/jobs", async (Guid id, IBackgroundJobQueue queue, CancellationToken ct) =>
        {
            var jobs = await queue.ListByCaseAsync(id, ct);
            return Results.Ok(jobs.Select(Mapping.ToDto).ToList());
        });

        group.MapGet("/{id:guid}/analysis", async (Guid id, IRepository<ProjectAnalysis> analyses, IRepository<Evidence> evidences, IRepository<ApplicationCase> cases, CancellationToken ct) =>
        {
            var entity = await cases.FindAsync(id, ct);
            if (entity is null || entity.IsDeleted)
            {
                return Results.NotFound(new { error = new { code = "not_found", message = "申请案例不存在。" } });
            }

            var analysis = (await analyses.ListAsync(a => a.CaseId == id, ct)).OrderByDescending(a => a.CreatedAt).FirstOrDefault();
            if (analysis is null)
            {
                return Results.Content("null", "application/json");
            }

            var caseEvidences = await evidences.ListAsync(e => e.CaseId == id, ct);
            var dto = AnalysisMapper.ToDto(analysis, caseEvidences);
            return Results.Ok(dto);
        });

        group.MapGet("/{id:guid}/applicant-info", async (Guid id, IApplicantInfoService service, IConfirmationService confirmations, CancellationToken ct) =>
        {
            var info = await service.GetAsync(id, ct);
            if (info is null)
            {
                // 必须返回 JSON null 而不是 200 空响应体，否则客户端反序列化会失败。
                return Results.Content("null", "application/json");
            }

            var statuses = await confirmations.ListAsync(id, ct);
            var confirmed = statuses.FirstOrDefault(s => s.Stage == ConfirmationStage.ApplicantInfo)?.Confirmed == true
                           && statuses.FirstOrDefault(s => s.Stage == ConfirmationStage.ApplicantInfo)!.IsStale == false;

            var applicantService = (ApplicantInfoService)service;
            return Results.Ok(applicantService.ToDto(info, confirmed));
        });

        group.MapPut("/{id:guid}/applicant-info", async (Guid id, UpdateApplicantInfoRequest request, IApplicantInfoService service, CancellationToken ct) =>
        {
            var info = await service.SaveAsync(id, request, ct);
            var applicantService = (ApplicantInfoService)service;
            return Results.Ok(applicantService.ToDto(info, false));
        });

        group.MapGet("/{id:guid}/code-candidates", async (Guid id, ICodeSelectionService service, CancellationToken ct) =>
        {
            var candidates = await service.ListCandidatesAsync(id, ct);
            var selected = (await service.GetSelectedPathsAsync(id, ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var items = candidates
                .Where(c => c.IsCodeCandidate || c.Category == FileCategory.Code)
                .Select(c => CodeSelectionService.ToDto(c, selected.Contains(c.RelativePath)))
                .OrderBy(c => c.RelativePath, StringComparer.Ordinal)
                .ToList();

            return Results.Ok(items);
        });

        group.MapGet("/{id:guid}/code-candidates/preview", async (Guid id, string path, int maxLines, ICodeSelectionService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Results.BadRequest(new { error = new { code = "validation", message = "缺少文件路径。" } });
            }

            var preview = await service.PreviewAsync(id, path, maxLines <= 0 ? 200 : maxLines, ct);
            if (preview is null)
            {
                return Results.NotFound(new { error = new { code = "not_found", message = "无法读取该文件内容。" } });
            }

            return Results.Ok(new { path, content = preview });
        });

        group.MapPut("/{id:guid}/code-selection", async (Guid id, UpdateCodeSelectionRequest request, ICodeSelectionService service, CancellationToken ct) =>
        {
            await service.SaveSelectionAsync(id, request.SelectedPaths ?? [], ct);
            return Results.Ok(new { selected = request.SelectedPaths?.Count ?? 0 });
        });

        group.MapGet("/{id:guid}/confirmations", async (Guid id, IConfirmationService service, CancellationToken ct) =>
        {
            var statuses = await service.ListAsync(id, ct);
            return Results.Ok(statuses.Select(Mapping.ToDto).ToList());
        });

        group.MapPost("/{id:guid}/confirm/{stage}", async (Guid id, string stage, ConfirmStageRequest request, IConfirmationService service, CancellationToken ct) =>
        {
            if (!Enum.TryParse<ConfirmationStage>(stage, ignoreCase: true, out var parsed))
            {
                return Results.BadRequest(new { error = new { code = "validation", message = "未知的确认阶段。" } });
            }

            var status = await service.ConfirmAsync(id, parsed, request.Note ?? string.Empty, ct);
            return Results.Ok(Mapping.ToDto(status));
        });

        group.MapPost("/{id:guid}/generate", async (Guid id, ICaseService cases, IBackgroundJobQueue queue, IConfirmationService confirmations, CancellationToken ct) =>
        {
            var entity = await cases.GetAsync(id, ct);
            if (entity is null)
            {
                return Results.NotFound(new { error = new { code = "not_found", message = "申请案例不存在。" } });
            }

            // 生成材料前必须已通过相应门禁：软著走三道，专利只需确认草稿。
            var statuses = await confirmations.ListAsync(id, ct);
            var isPatent = entity.MaterialKind == MaterialKind.Patent;

            if (!isPatent)
            {
                var applicantStatus = statuses.FirstOrDefault(s => s.Stage == ConfirmationStage.ApplicantInfo);
                var codeStatus = statuses.FirstOrDefault(s => s.Stage == ConfirmationStage.CodeScope);

                if (applicantStatus is null || !applicantStatus.Confirmed || applicantStatus.IsStale)
                {
                    return Results.Conflict(new
                    {
                        error = new { code = "conflict", message = "请先填写并确认申请信息，再生成材料。" }
                    });
                }

                if (codeStatus is null || !codeStatus.Confirmed || codeStatus.IsStale)
                {
                    return Results.Conflict(new
                    {
                        error = new { code = "conflict", message = "请先勾选源文件并确认代码范围，再生成材料。" }
                    });
                }
            }

            var draftStatus = statuses.FirstOrDefault(s => s.Stage == ConfirmationStage.DocumentDraft);
            if (draftStatus is null || !draftStatus.Confirmed || draftStatus.IsStale)
            {
                return Results.Conflict(new
                {
                    error = new
                    {
                        code = "conflict",
                        message = isPatent
                            ? "请先审阅并确认专利草稿，再生成专利申请文件。"
                            : "请先审阅并确认文档草稿，再生成正式材料。"
                    }
                });
            }

            var existing = await queue.ListByCaseAsync(id, ct);
            var running = existing.FirstOrDefault(j =>
                j.Type == JobType.Generate && j.Status is JobStatus.Pending or JobStatus.Running);
            if (running is not null)
            {
                return Results.Conflict(new { error = new { code = "conflict", message = "已有生成任务在执行中。" }, jobId = running.Id });
            }

            var jobId = await queue.EnqueueAsync(id, JobType.Generate, "{}", ct);
            return Results.Accepted($"/api/cases/{id}/jobs", new { jobId });
        });

        group.MapGet("/{id:guid}/artifacts", async (Guid id, IRepository<GeneratedArtifact> artifacts, CancellationToken ct) =>
        {
            var items = await artifacts.ListAsync(a => a.CaseId == id, ct);
            return Results.Ok(items
                .OrderBy(a => a.CreatedAt)
                .Select(a => Mapping.ToDto(a))
                .ToList());
        });

        group.MapGet("/{id:guid}/patent/disclosure", async (Guid id, IPatentService patentService, CancellationToken ct) =>
        {
            var disclosure = await patentService.GetDisclosureAsync(id, ct);
            var claims = await patentService.GetClaimsAsync(id, ct);
            return Results.Ok(PatentService.ToDto(disclosure ?? new InventionDisclosure { CaseId = id }, claims));
        });

        group.MapPut("/{id:guid}/patent/disclosure", async (Guid id, PatentDisclosureUpdateRequest request, IPatentService patentService, CancellationToken ct) =>
        {
            var disclosure = await patentService.SaveDisclosureAsync(id, request, ct);
            var claims = await patentService.GetClaimsAsync(id, ct);
            return Results.Ok(PatentService.ToDto(disclosure, claims));
        });

        group.MapPost("/{id:guid}/patent/draft", async (Guid id, ICaseService cases, IBackgroundJobQueue queue, CancellationToken ct) =>
        {
            var entity = await cases.GetAsync(id, ct);
            if (entity is null)
            {
                return Results.NotFound(new { error = new { code = "not_found", message = "申请案例不存在。" } });
            }

            if (entity.MaterialKind != MaterialKind.Patent)
            {
                return Results.Conflict(new { error = new { code = "conflict", message = "当前案例不是专利类型。" } });
            }

            var existing = await queue.ListByCaseAsync(id, ct);
            var running = existing.FirstOrDefault(j =>
                j.Type == JobType.PatentDraft && j.Status is JobStatus.Pending or JobStatus.Running);
            if (running is not null)
            {
                return Results.Conflict(new { error = new { code = "conflict", message = "已有专利草稿任务在执行中。" }, jobId = running.Id });
            }

            var jobId = await queue.EnqueueAsync(id, JobType.PatentDraft, "{}", ct);
            return Results.Accepted($"/api/cases/{id}/jobs", new { jobId });
        });

        group.MapGet("/{id:guid}/validation", async (Guid id, IValidationService service, CancellationToken ct) =>
        {
            var results = await service.ValidateAsync(id, ct);
            return Results.Ok(results.Select(Mapping.ToDto).ToList());
        });

        group.MapGet("/{id:guid}/package/download", async (Guid id, IRepository<GeneratedArtifact> artifacts, IFileStore fileStore, CancellationToken ct) =>
        {
            var package = (await artifacts.ListAsync(a => a.CaseId == id && a.Kind == ArtifactKind.Package, ct))
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefault();

            if (package is null || package.IsStale)
            {
                return Results.NotFound(new { error = new { code = "not_found", message = "尚未生成可下载的材料包。" } });
            }

            var path = fileStore.ResolveSafePath(package.RelativePath);
            if (!File.Exists(path))
            {
                return Results.NotFound(new { error = new { code = "not_found", message = "材料包文件已不存在，请重新生成。" } });
            }

            return Results.File(path, "application/zip", package.FileName);
        });
    }
}

/// <summary>扫描摘要读取。</summary>
internal static class ScanSummaryReader
{
    public static ScanSummaryDto? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var summary = System.Text.Json.JsonSerializer.Deserialize<ScanSummary>(json, Mapping.JsonOptions);
            if (summary is null)
            {
                return null;
            }

            return new ScanSummaryDto(
                summary.TotalFiles,
                summary.IncludedFiles,
                summary.ExcludedFiles,
                summary.CodeCandidates,
                summary.EvidenceCount,
                summary.SuspectedSecretCount,
                summary.Languages,
                summary.ExclusionReasons,
                summary.AdapterHits,
                summary.Warnings);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
