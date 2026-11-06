using SoftCopyright.Desktop.ViewModels;

namespace SoftCopyright.Desktop.Services;

/// <summary>
/// 桌面端启动参数。
///
/// 支持 <c>--case &lt;案例 ID&gt;</c>、<c>--step &lt;步骤名&gt;</c> 与 <c>--demo</c>：
/// 启动后直接打开指定案例的指定步骤，或先生成一份演示案例。
/// </summary>
public sealed record DesktopLaunchOptions(Guid? CaseId, ShellStep? Step, bool SeedDemo, string? SnapshotPath)
{
    /// <summary>解析命令行参数；无法识别的参数一律忽略，不阻断启动。</summary>
    public static DesktopLaunchOptions Parse(string[] args)
    {
        Guid? caseId = null;
        ShellStep? step = null;
        var seedDemo = false;
        string? snapshotPath = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--case" when i + 1 < args.Length:
                    if (Guid.TryParse(args[i + 1], out var parsed))
                    {
                        caseId = parsed;
                    }

                    i++;
                    break;

                case "--step" when i + 1 < args.Length:
                    if (Enum.TryParse<ShellStep>(args[i + 1], ignoreCase: true, out var parsedStep))
                    {
                        step = parsedStep;
                    }

                    i++;
                    break;

                case "--snapshot" when i + 1 < args.Length:
                    snapshotPath = args[i + 1];
                    i++;
                    break;

                case "--demo":
                    seedDemo = true;
                    break;
            }
        }

        return new DesktopLaunchOptions(caseId, step, seedDemo, snapshotPath);
    }
}
