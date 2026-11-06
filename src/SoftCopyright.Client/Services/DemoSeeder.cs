using System.IO.Compression;
using SoftCopyright.Application.Dtos;
using SoftCopyright.Domain;

namespace SoftCopyright.Client.Services;

/// <summary>
/// 演示数据生成器。
///
/// 用仓库里的样例仓库建一个可查看的演示案例，并填好一份示例申请信息，
/// 方便第一次打开桌面端时就能看到完整的流程与界面（尤其��条件联动字段）。
/// </summary>
public sealed class DemoSeeder(DesktopEngine engine)
{
    /// <summary>生成演示案例，返回案例 ID；找不到样例仓库时只建空案例。</summary>
    public async Task<Guid> SeedAsync(CancellationToken ct = default)
    {
        var caseEntity = await engine.CreateCaseAsync("演示案例", MaterialKind.SoftwareCopyright, ct);

        var samples = FindSamplesRoot();
        if (samples is not null)
        {
            var zipPath = Path.Combine(engine.DataRoot, "demo-sample.zip");
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            ZipFile.CreateFromDirectory(samples, zipPath);
            await engine.ImportZipAsync(caseEntity.Id, zipPath, ct);
            await engine.ScanAsync(caseEntity.Id, ct);
            await engine.AnalyzeAsync(caseEntity.Id, ct);
            await engine.ConfirmAsync(caseEntity.Id, ConfirmationStage.ProjectProfile, "演示数据", ct);
        }

        await engine.SaveApplicantAsync(caseEntity.Id, BuildSampleApplicantInfo(), ct);
        await engine.ConfirmAsync(caseEntity.Id, ConfirmationStage.ApplicantInfo, "演示数据", ct);

        return caseEntity.Id;
    }

    /// <summary>
    /// 样例申请信息：刻意选了「修改软件」「部分权利」「已发表」，
    /// 这样打开就能看到条件必填字段是怎么联动出来的。
    /// </summary>
    private static UpdateApplicantInfoRequest BuildSampleApplicantInfo() => new(
        "演示应用系统",
        "演示系统",
        "V1.0",
        "示例著作权人",
        "Solo",
        "OriginalAcquisition",
        "2026-11-01",
        "Published",
        "2026-11-02",
        "Windows 11 + .NET 10",
        "Windows 10 及以上",
        "C#",
        "220 行",
        "演示用样例：展示申请信息各栏位与官方规定选项的填写方式。",
        "LegalPerson",
        "Modified",
        "在 V0.9 的基础上增加材料导出与多主题支持。",
        null,
        "Partial",
        "复制权、发行权",
        "广东省深圳市",
        "General",
        "ByOwner",
        "12345-1234",
        0);

    /// <summary>寻找仓库里的样例目录（发布后的目录结构可能不同，找不到就跳过导入）。</summary>
    private static string? FindSamplesRoot()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "samples"),
            Path.Combine(AppContext.BaseDirectory, "samples"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "samples")
        };

        foreach (var candidate in candidates)
        {
            var full = Path.GetFullPath(candidate);
            if (Directory.Exists(full) && Directory.EnumerateFileSystemEntries(full).Any())
            {
                return full;
            }
        }

        return null;
    }
}
