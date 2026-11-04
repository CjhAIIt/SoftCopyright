using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Application.Generation;

/// <summary>
/// 某一类材料的产物生成策略。
///
/// 软著与专利产出的文件完全不同，这里各有一份实现：
/// 软著出申请表信息、操作手册与代码鉴别材料；专利出权利要求书、说明书、摘要与技术交底书。
/// </summary>
public interface IMaterialGenerationStrategy
{
    /// <summary>本策略适用的材料类型。</summary>
    MaterialKind Kind { get; }

    /// <summary>生成全部材料产物。</summary>
    Task<IReadOnlyList<GeneratedDocument>> GenerateAsync(MaterialGenerationContext context, CancellationToken ct);

    /// <summary>按产物的相对路径判定产物类型。</summary>
    ArtifactKind MapKind(string relativePath);
}
