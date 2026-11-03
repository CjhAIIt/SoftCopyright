using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Api.Endpoints;

/// <summary>产物下载 API。下载路径完全来自服务端数据库，客户端无法提交磁盘路径。</summary>
public static class ArtifactEndpoints
{
    public static void MapArtifactEndpoints(this WebApplication app)
    {
        app.MapGet("/api/artifacts/{artifactId:guid}/download", async (
            Guid artifactId,
            IRepository<GeneratedArtifact> artifacts,
            IRepository<ApplicationCase> cases,
            IFileStore fileStore,
            CancellationToken ct) =>
        {
            var artifact = await artifacts.FindAsync(artifactId, ct);
            if (artifact is null || artifact.IsStale)
            {
                return Results.NotFound(new { error = new { code = "not_found", message = "产物不存在或已失效。" } });
            }

            var caseEntity = await cases.FindAsync(artifact.CaseId, ct);
            if (caseEntity is null || caseEntity.IsDeleted)
            {
                return Results.NotFound(new { error = new { code = "not_found", message = "所属申请案例不存在。" } });
            }

            string path;
            try
            {
                path = fileStore.ResolveSafePath(artifact.RelativePath);
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(new { error = new { code = "not_found", message = "产物文件路径无效。" } });
            }

            if (!File.Exists(path))
            {
                return Results.NotFound(new { error = new { code = "not_found", message = "产物文件已被清理。" } });
            }

            return Results.File(path, artifact.ContentType, artifact.FileName);
        });
    }
}
