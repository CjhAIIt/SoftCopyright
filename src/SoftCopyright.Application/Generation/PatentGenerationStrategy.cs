using SoftCopyright.Domain;
using SoftCopyright.Domain.Contracts;

namespace SoftCopyright.Application.Generation;

/// <summary>
/// 专利申请文件生成策略。
///
/// 产出权利要求书、说明书、摘要与技术交底书，全部内容来自技术交底书与已生成的权利要求，
/// 不含代码鉴别材料。
/// </summary>
public sealed class PatentGenerationStrategy(IPatentDocumentGenerator documentGenerator) : IMaterialGenerationStrategy
{
    public MaterialKind Kind => MaterialKind.Patent;

    public Task<IReadOnlyList<GeneratedDocument>> GenerateAsync(
        MaterialGenerationContext context,
        CancellationToken ct)
    {
        var disclosure = context.Disclosure
                         ?? throw new InvalidOperationException("尚未填写技术交底书，无法生成专利申请文件。");

        var request = new PatentDocumentRequest(
            string.IsNullOrWhiteSpace(disclosure.Title) ? context.Case.Name : disclosure.Title,
            context.ApplicantInfo?.VersionNumber ?? string.Empty,
            disclosure,
            context.Claims,
            context.ValidationResults,
            context.RulesVersion,
            context.RulesSource,
            context.GeneratedAt);

        return documentGenerator.GenerateAsync(request, ct);
    }

    public ArtifactKind MapKind(string relativePath) => relativePath switch
    {
        var p when p.EndsWith("权利要求书.docx", StringComparison.Ordinal) => ArtifactKind.PatentClaims,
        var p when p.EndsWith("说明书.docx", StringComparison.Ordinal) => ArtifactKind.PatentSpecification,
        var p when p.EndsWith("摘要.docx", StringComparison.Ordinal) => ArtifactKind.PatentAbstract,
        var p when p.EndsWith("技术交底书.docx", StringComparison.Ordinal) => ArtifactKind.PatentDisclosure,
        _ => ArtifactKind.Manifest
    };
}
