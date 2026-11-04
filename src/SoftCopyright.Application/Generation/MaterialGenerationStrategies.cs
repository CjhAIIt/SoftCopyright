using SoftCopyright.Domain;

namespace SoftCopyright.Application.Generation;

/// <summary>生成策略注册表：按材料类型挑选产物生成策略。</summary>
public sealed class MaterialGenerationStrategies
{
    private readonly Dictionary<MaterialKind, IMaterialGenerationStrategy> _strategies;

    public MaterialGenerationStrategies(IEnumerable<IMaterialGenerationStrategy> strategies)
    {
        _strategies = strategies.ToDictionary(s => s.Kind);
    }

    /// <summary>取指定材料类型的生成策略。</summary>
    public IMaterialGenerationStrategy Get(MaterialKind kind) =>
        _strategies.TryGetValue(kind, out var strategy)
            ? strategy
            : throw new InvalidOperationException($"未登记的材料生成策略：{kind}");
}
