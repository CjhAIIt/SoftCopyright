namespace SoftCopyright.Domain;

/// <summary>证据引用：指向仓库中的真实文件位置与内容哈希。</summary>
public sealed record EvidenceRef(
    string RelativePath,
    int? StartLine,
    int? EndLine,
    string EvidenceType,
    string Sha256);

/// <summary>带状态与证据的事实。</summary>
public sealed record Fact<T>(
    T? Value,
    FactStatus Status,
    IReadOnlyList<EvidenceRef> Evidence,
    string? Explanation)
{
    public static Fact<T> Unknown(string? explanation = null) => new(default, FactStatus.Unknown, [], explanation);

    public static Fact<T> Observed(T value, EvidenceRef evidence) =>
        new(value, FactStatus.Observed, [evidence], null);

    public static Fact<T> Inferred(T? value, string explanation, IReadOnlyList<EvidenceRef>? evidence = null) =>
        new(value, FactStatus.Inferred, evidence ?? [], explanation);
}

/// <summary>项目画像。</summary>
public sealed record ProjectProfile(
    Fact<string> Name,
    Fact<string> Version,
    Fact<string> Purpose,
    IReadOnlyList<Fact<string>> Features,
    IReadOnlyList<string> DetectedTechnologies,
    IReadOnlyList<string> DetectedEntrypoints,
    IReadOnlyList<string> OpenQuestions,
    string AnalyzerVersion);

/// <summary>单项覆盖情况。</summary>
public sealed record CoverageItem(string Dimension, bool Covered, string Detail);

/// <summary>分析覆盖摘要：完整度是导航指标，不是登记通过率预测。</summary>
public sealed record CoverageSummary(IReadOnlyList<CoverageItem> Items)
{
    public int CoveredCount => Items.Count(i => i.Covered);

    public int TotalCount => Items.Count;

    public double Ratio => TotalCount == 0 ? 0d : (double)CoveredCount / TotalCount;
}

/// <summary>扫描过程中发现的一个文件。</summary>
public sealed record ProjectFileEntry(
    string RelativePath,
    long SizeBytes,
    string Sha256,
    bool IsText,
    string DetectedEncoding,
    string? DetectedLanguage,
    FileCategory Category,
    bool IsExcluded,
    string? ExclusionReason,
    int? LineCount,
    SecretRisk SecretRisk,
    bool IsCodeCandidate);

/// <summary>扫描过程中提取的一条证据。</summary>
public sealed record EvidenceEntry(
    string RelativePath,
    int? StartLine,
    int? EndLine,
    string EvidenceType,
    string Sha256,
    string Excerpt,
    EvidenceSource Source);

/// <summary>适配器抽取出的事实。</summary>
public sealed record AdapterFact(string Key, string? Value, string? EvidencePath, int? Line);

/// <summary>单个适配器的分析结果。</summary>
public sealed record AdapterAnalysis(
    string AdapterId,
    bool Matched,
    string Summary,
    IReadOnlyList<EvidenceEntry> Evidences,
    IReadOnlyList<string> Technologies,
    IReadOnlyList<string> Entrypoints,
    IReadOnlyList<AdapterFact> Facts);

/// <summary>校验结论。</summary>
public sealed record ValidationResult(
    string RuleId,
    ValidationLevel Level,
    string Title,
    string Message,
    string? Target = null,
    string? FixHint = null);

/// <summary>代码块：一段来自真实源码的行。</summary>
public sealed record CodeLineBlock(
    string RelativePath,
    string Sha256,
    int StartLine,
    IReadOnlyList<string> Lines);

/// <summary>申请人信息快照（供 AI 起草使用，只读）。</summary>
public sealed record ApplicantInfoSnapshot(
    string SoftwareFullName,
    string SoftwareVersion,
    string CopyrightOwner,
    string FunctionSummary,
    string ProgrammingLanguages,
    string DevelopmentEnvironment,
    string RuntimeEnvironment);

/// <summary>证据包条目：发送给模型前已脱敏、截断并标注哈希。</summary>
public sealed record EvidencePackItem(
    string RelativePath,
    string EvidenceType,
    string Sha256,
    string Content,
    int? StartLine,
    int? EndLine);

/// <summary>证据包。</summary>
public sealed record EvidencePack(
    IReadOnlyList<EvidencePackItem> Items,
    IReadOnlyList<string> OmittedPaths,
    int TotalBytes);

/// <summary>模型请求。</summary>
public sealed record AiRequest(string SystemPrompt, string UserPayload, int MaxTokens = 2000);

/// <summary>项目分析结果。</summary>
public sealed record ProjectAnalysisResult(ProjectProfile Profile, IReadOnlyList<string> Warnings);

/// <summary>材料草稿结果。</summary>
public sealed record MaterialDraftResult(
    IReadOnlyList<Fact<string>> Features,
    IReadOnlyList<string> OperationSections,
    IReadOnlyList<string> Warnings);
