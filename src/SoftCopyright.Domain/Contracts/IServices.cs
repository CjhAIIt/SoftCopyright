using System.Linq.Expressions;

namespace SoftCopyright.Domain.Contracts;

/// <summary>通用仓储（由 Infrastructure 用 EF Core 实现）。</summary>
public interface IRepository<T> where T : class
{
    Task<T?> FindAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate, CancellationToken ct);

    Task AddAsync(T entity, CancellationToken ct);

    void Remove(T entity);

    Task<int> SaveChangesAsync(CancellationToken ct);
}

/// <summary>案例阶段状态机。</summary>
public interface ICaseWorkflow
{
    bool CanTransition(CaseStage from, CaseStage to);

    /// <summary>按材料类型判断迁移是否合法：软著与专利走不同的前置门禁，允许的迁移也不同。</summary>
    bool CanTransition(CaseStage from, CaseStage to, MaterialKind materialKind);

    IReadOnlyList<CaseStage> AllowedTransitions(CaseStage from);

    /// <summary>按顺序排列的主流程阶段，用于进度展示。</summary>
    IReadOnlyList<CaseStage> Pipeline { get; }
}

/// <summary>文件存储。</summary>
public interface IFileStore
{
    string DataRoot { get; }

    string GetCaseDirectory(Guid caseId);

    string GetUploadDirectory(Guid caseId);

    string GetExtractedRoot(Guid caseId);

    string GetArtifactDirectory(Guid caseId, Guid artifactId);

    /// <summary>保存上传的原始包，返回相对于数据根目录的路径。</summary>
    Task<string> SaveUploadAsync(Guid caseId, string fileName, Stream content, CancellationToken ct);

    /// <summary>保存产物，返回相对于数据根目录的路径。</summary>
    Task<string> SaveArtifactAsync(Guid caseId, Guid artifactId, string relativePath, byte[] content, CancellationToken ct);

    /// <summary>把相对路径解析为绝对路径，并确认仍在数据根目录内。</summary>
    string ResolveSafePath(string relativePath);

    /// <summary>删除案例目录。</summary>
    void DeleteCaseDirectory(Guid caseId);
}

/// <summary>后台任务队列。</summary>
public interface IBackgroundJobQueue
{
    Task<Guid> EnqueueAsync(Guid caseId, JobType type, string payloadJson, CancellationToken ct);

    Task<IReadOnlyList<BackgroundJob>> ListByCaseAsync(Guid caseId, CancellationToken ct);

    Task<BackgroundJob?> FindAsync(Guid jobId, CancellationToken ct);

    /// <summary>认领一个待执行任务；返回 null 表示当前没有可执行任务。</summary>
    Task<BackgroundJob?> ClaimNextAsync(CancellationToken ct);

    Task<bool> UpdateAsync(Guid jobId, Action<BackgroundJob> mutate, CancellationToken ct);

    Task RequeueExpiredLeasesAsync(CancellationToken ct);
}

/// <summary>申请案例的用例服务。</summary>
public interface ICaseService
{
    Task<IReadOnlyList<ApplicationCase>> ListAsync(CancellationToken ct);

    /// <summary>按材料类型列出案例：软著与专利各自独立的工作区。</summary>
    Task<IReadOnlyList<ApplicationCase>> ListAsync(MaterialKind materialKind, CancellationToken ct);

    Task<ApplicationCase?> GetAsync(Guid id, CancellationToken ct);

    Task<ApplicationCase> CreateAsync(string? name, CancellationToken ct);

    Task<ApplicationCase> CreateAsync(string? name, MaterialKind materialKind, CancellationToken ct);

    Task<bool> DeleteAsync(Guid id, CancellationToken ct);
}

/// <summary>代码选择用例服务。</summary>
public interface ICodeSelectionService
{
    Task<IReadOnlyList<ProjectFile>> ListCandidatesAsync(Guid caseId, CancellationToken ct);

    Task<IReadOnlyList<string>> GetSelectedPathsAsync(Guid caseId, CancellationToken ct);

    Task SaveSelectionAsync(Guid caseId, IReadOnlyList<string> selectedPaths, CancellationToken ct);

    Task<string?> PreviewAsync(Guid caseId, string relativePath, int maxLines, CancellationToken ct);
}

/// <summary>确认门禁服务。</summary>
public interface IConfirmationService
{
    Task<IReadOnlyList<ConfirmationStatus>> ListAsync(Guid caseId, CancellationToken ct);

    Task<ConfirmationStatus> ConfirmAsync(Guid caseId, ConfirmationStage stage, string note, CancellationToken ct);

    /// <summary>计算某阶段当前数据的规范化哈希。</summary>
    Task<string> ComputeDataHashAsync(Guid caseId, ConfirmationStage stage, CancellationToken ct);
}

/// <summary>确认状态。</summary>
public sealed record ConfirmationStatus(
    ConfirmationStage Stage,
    bool Confirmed,
    bool IsStale,
    DateTimeOffset? ConfirmedAt,
    string? ConfirmedBy,
    string? DataHash,
    string? Note,
    string? InvalidatedReason);

/// <summary>校验服务。</summary>
public interface IValidationService
{
    /// <summary>执行校验。生成材料时把 includeArtifactCheck 设为 false，避免自指循环。</summary>
    Task<IReadOnlyList<ValidationResult>> ValidateAsync(Guid caseId, CancellationToken ct, bool includeArtifactCheck = true);
}
