namespace SoftCopyright.Desktop.Services;

/// <summary>
/// 启动期共享状态。
///
/// 引擎与演示数据必须在 Avalonia 初始化**之前**就绪：在 UI 线程上阻塞等待异步任务
/// 会与 Avalonia 的同步上下文死锁，因此这部分工作放到 <c>Program.Main</c> 里完成。
/// </summary>
public static class AppBootstrap
{
    /// <summary>业务引擎；启动失败时为 null。</summary>
    public static DesktopEngine? Engine { get; set; }

    /// <summary>启动失败原因。</summary>
    public static Exception? StartupError { get; set; }

    /// <summary>启动参数。</summary>
    public static DesktopLaunchOptions Options { get; set; } = new(null, null, false, null);
}
