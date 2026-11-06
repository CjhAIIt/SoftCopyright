using SoftCopyright.Client.ViewModels;

namespace SoftCopyright.Client.Services;

/// <summary>
/// 启动参数。
///
/// 支持 <c>--case &lt;案例 ID&gt;</c>、<c>--step &lt;步骤名&gt;</c>、<c>--demo</c>、
/// <c>--snapshot &lt;文件&gt;</c> 与 <c>--size &lt;宽x高&gt;</c>：
/// 启动后直接打开指定案例的指定步骤，或先生成一份演示案例；
/// <c>--size</c> 用来按手机尺寸渲染快照，检查窄屏布局。
/// </summary>
public sealed record DesktopLaunchOptions(
    Guid? CaseId,
    ShellStep? Step,
    bool SeedDemo,
    string? SnapshotPath,
    double? Width,
    double? Height)
{
    /// <summary>解析命令行参数；无法识别的参数一律忽略，不阻断启动。</summary>
    public static DesktopLaunchOptions Parse(string[] args)
    {
        Guid? caseId = null;
        ShellStep? step = null;
        var seedDemo = false;
        string? snapshotPath = null;
        double? width = null;
        double? height = null;

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

                case "--size" when i + 1 < args.Length:
                    // 形如 390x844：按手机尺寸渲染快照
                    var parts = args[i + 1].Split('x', 'X');
                    if (parts.Length == 2 &&
                        double.TryParse(parts[0], out var w) &&
                        double.TryParse(parts[1], out var h))
                    {
                        width = w;
                        height = h;
                    }

                    i++;
                    break;

                case "--demo":
                    seedDemo = true;
                    break;
            }
        }

        return new DesktopLaunchOptions(caseId, step, seedDemo, snapshotPath, width, height);
    }
}
