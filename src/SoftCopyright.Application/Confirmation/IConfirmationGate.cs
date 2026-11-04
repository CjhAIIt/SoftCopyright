using SoftCopyright.Domain;

namespace SoftCopyright.Application.Confirmation;

/// <summary>
/// 某一类材料的确认门禁。
///
/// 软著要逐级确认「申请信息 → 代码范围 → 文档草稿」；
/// 专利没有申请表与代码范围，门禁只看「交底书与权利要求是否就绪」。
/// </summary>
public interface IConfirmationGate
{
    /// <summary>本门禁适用的材料类型。</summary>
    MaterialKind Kind { get; }

    /// <summary>校验指定确认阶段的前置条件；不满足时抛出 <see cref="ConflictException"/>。</summary>
    Task EnsureAsync(ConfirmationStage stage, ConfirmationGateContext context, CancellationToken ct);
}
