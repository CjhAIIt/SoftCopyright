using SoftCopyright.Domain;

namespace SoftCopyright.Application.Confirmation;

/// <summary>软件著作权登记的确认门禁：申请信息 → 代码范围 → 文档草稿 → 最终材料。</summary>
public sealed class SoftCopyrightConfirmationGate : IConfirmationGate
{
    public MaterialKind Kind => MaterialKind.SoftwareCopyright;

    public async Task EnsureAsync(ConfirmationStage stage, ConfirmationGateContext context, CancellationToken ct)
    {
        switch (stage)
        {
            case ConfirmationStage.ProjectProfile:
                return;

            case ConfirmationStage.ApplicantInfo:
            {
                var info = context.ApplicantInfo;
                if (info is null || string.IsNullOrWhiteSpace(info.SoftwareFullName))
                {
                    throw new ConflictException("请先填写申请信息，再进行确认。");
                }

                return;
            }

            case ConfirmationStage.CodeScope:
            {
                if (context.CodeSelection is null)
                {
                    throw new ConflictException("请先选择纳入材料包的真实源文件。");
                }

                if (!context.CodeSelectionItems.Any(i => i.Included))
                {
                    throw new ConflictException("至少需要勾选一个源文件才能确认代码范围。");
                }

                return;
            }

            case ConfirmationStage.DocumentDraft:
            {
                // 软著草稿依赖前序三道门禁已经通过且未失效。
                foreach (var required in new[]
                         {
                             ConfirmationStage.ProjectProfile,
                             ConfirmationStage.ApplicantInfo,
                             ConfirmationStage.CodeScope
                         })
                {
                    if (!await context.IsConfirmationValid(required))
                    {
                        throw new ConflictException(
                            $"请先完成「{Mapping.ConfirmationStageText(required)}」，再确认文档草稿。");
                    }
                }

                return;
            }

            case ConfirmationStage.FinalPackage:
            {
                if (!context.Artifacts.Any(a => a.Kind == ArtifactKind.Package && !a.IsStale))
                {
                    throw new ConflictException("请先生成材料包，再进行最终确认。");
                }

                return;
            }

            default:
                return;
        }
    }
}
