using SoftCopyright.Domain;

namespace SoftCopyright.Application.Confirmation;

/// <summary>
/// 专利申请文件的确认门禁：技术交底书与权利要求就绪 → 文档草稿 → 最终材料。
///
/// 专利案例没有申请表与代码范围，因此不设「申请信息」「代码范围」两道门禁。
/// </summary>
public sealed class PatentConfirmationGate : IConfirmationGate
{
    public MaterialKind Kind => MaterialKind.Patent;

    public Task EnsureAsync(ConfirmationStage stage, ConfirmationGateContext context, CancellationToken ct)
    {
        switch (stage)
        {
            case ConfirmationStage.DocumentDraft:
            {
                var disclosure = context.Disclosure;
                if (disclosure is null || string.IsNullOrWhiteSpace(disclosure.TechnicalSolution))
                {
                    throw new ConflictException("请先填写技术交底书，再确认草稿。");
                }

                if (context.Claims.Count == 0)
                {
                    throw new ConflictException("请先生成专利草稿（权利要求），再确认草稿。");
                }

                return Task.CompletedTask;
            }

            case ConfirmationStage.FinalPackage:
            {
                if (!context.Artifacts.Any(a => a.Kind == ArtifactKind.Package && !a.IsStale))
                {
                    throw new ConflictException("请先生成材料包，再进行最终确认。");
                }

                return Task.CompletedTask;
            }

            default:
                return Task.CompletedTask;
        }
    }
}
