using SoftCopyright.Domain;

namespace SoftCopyright.Application.Validation;

/// <summary>
/// 某一类材料的专属校验规则集。
///
/// 软著与专利的检查内容互不相同，这里各有一份实现，
/// 校验中心只负责按材料类型挑选规则集并汇总结果。
/// </summary>
public interface IMaterialValidationRuleSet
{
    /// <summary>本规则集适用的材料类型。</summary>
    MaterialKind Kind { get; }

    /// <summary>执行本材料类型的全部专属检查。</summary>
    IReadOnlyList<ValidationResult> Evaluate(MaterialValidationContext context);
}
