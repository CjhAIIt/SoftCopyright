using SoftCopyright.Domain;

namespace SoftCopyright.Application.Validation;

/// <summary>校验规则集注册表：按材料类型挑选专属规则集。</summary>
public sealed class MaterialValidationRuleSets
{
    private readonly Dictionary<MaterialKind, IMaterialValidationRuleSet> _sets;

    public MaterialValidationRuleSets(IEnumerable<IMaterialValidationRuleSet> ruleSets)
    {
        _sets = ruleSets.ToDictionary(r => r.Kind);
    }

    /// <summary>取指定材料类型的专属规则集。</summary>
    public IMaterialValidationRuleSet Get(MaterialKind kind) =>
        _sets.TryGetValue(kind, out var set)
            ? set
            : throw new InvalidOperationException($"未登记的材料校验规则集：{kind}");
}
