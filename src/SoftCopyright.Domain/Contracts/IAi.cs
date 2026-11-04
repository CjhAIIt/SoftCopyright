namespace SoftCopyright.Domain.Contracts;

/// <summary>项目分析器：把证据包转成带证据的项目画像。</summary>
public interface IProjectAnalyzer
{
    /// <summary>Provider 标识。</summary>
    string Id { get; }

    Task<ProjectAnalysisResult> AnalyzeAsync(EvidencePack pack, ProjectProfile baseline, CancellationToken ct);
}

/// <summary>材料草稿生成器：只产出说明性文字，不得生成代码。</summary>
public interface IMaterialDraftGenerator
{
    string Id { get; }

    Task<MaterialDraftResult> DraftAsync(EvidencePack pack, ApplicantInfoSnapshot applicant, CancellationToken ct);
}

/// <summary>底层模型 Provider。</summary>
public interface IAiProvider
{
    string Id { get; }

    /// <summary>
    /// 取当前可用的模型接入设置；返回 null 表示不可用，此时不得发起任何请求。
    /// 设置可能来自当前账号在引导流程中的配置，也可能来自全局环境变量。
    /// </summary>
    Task<AiAccountSettings?> ResolveAsync(CancellationToken ct);

    /// <summary>请求模型返回 JSON 文本。</summary>
    Task<string> CompleteJsonAsync(AiRequest request, CancellationToken ct);
}

/// <summary>证据包构建器：控制发送范围并剔除疑似凭据内容。</summary>
public interface IEvidencePackBuilder
{
    Task<EvidencePack> BuildAsync(
        IReadOnlyList<ProjectFileEntry> files,
        IReadOnlyList<EvidenceEntry> evidences,
        IProjectFileReader reader,
        CancellationToken ct = default);
}

/// <summary>模型输出校验器：强校验 JSON 并回验证据引用。</summary>
public interface IAiOutputValidator
{
    /// <summary>解析并校验模型输出；校验不通过时返回失败原因。</summary>
    AiOutputValidationResult Validate(string json, EvidencePack pack);

    /// <summary>回退到规则分析结果。</summary>
    ProjectAnalysisResult Fallback(ProjectProfile baseline, string reason);
}

/// <summary>模型输出校验结果。</summary>
public sealed record AiOutputValidationResult(
    bool IsValid,
    IReadOnlyList<Fact<string>> Features,
    IReadOnlyList<string> OperationSections,
    IReadOnlyList<string> Warnings,
    string? Error);
