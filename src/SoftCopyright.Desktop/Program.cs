using System;
using Avalonia;
using SoftCopyright.Client.Services;

namespace SoftCopyright.Desktop;

/// <summary>桌面端程序入口。</summary>
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // 引擎在这里准备好：此时还没有 Avalonia 的同步上下文，
        // 阻塞等待异步任务不会死锁；放到 UI 线程里做会死锁。
        AppBootstrap.PrepareEngine(args);

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia 配置，不要删除；可视化设计器也会用到。
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<SoftCopyright.Client.App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
