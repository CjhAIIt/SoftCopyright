using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SoftCopyright.Client.Services;
using SoftCopyright.Client.ViewModels;
using SoftCopyright.Client.Views;

namespace SoftCopyright.Client;

/// <summary>应用入口。桌面端与移动端共用这一份初始化逻辑。</summary>
public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var options = AppBootstrap.Options;

        if (AppBootstrap.Engine is null)
        {
            ShowStartupError(AppBootstrap.StartupError?.Message ?? "业务引擎未能启动。");
            base.OnFrameworkInitializationCompleted();
            return;
        }

        var engine = AppBootstrap.Engine;
        var theme = new ThemeService(engine.DataRoot);
        var shell = new ShellViewModel(engine, theme);
        shell.IsDarkTheme = theme.IsDark;

        // 支持 --case <案例 ID> --step <步骤名>：启动后直接落到指定位置。
        if (options.CaseId is { } caseId)
        {
            var entity = engine.GetCaseAsync(caseId, CancellationToken.None).GetAwaiter().GetResult();
            if (entity is not null)
            {
                shell.SetCase(entity);
            }
        }

        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                desktop.ShutdownRequested += (_, _) => engine.DisposeAsync().AsTask().GetAwaiter().GetResult();
                desktop.MainWindow = new MainWindow { DataContext = shell };

                // --size 390x844：按手机尺寸开窗，用来检查窄屏布局。
                if (options.Width is { } width && options.Height is { } height)
                {
                    desktop.MainWindow.Width = width;
                    desktop.MainWindow.Height = height;
                    desktop.MainWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                break;

            case IActivityApplicationLifetime activity:
                // Android 单视图生命周期：整个应用就是这一个视图。
                activity.MainViewFactory = () => new ShellView { DataContext = shell };
                break;

            case ISingleViewApplicationLifetime singleView:
                singleView.MainView = new ShellView { DataContext = shell };
                break;
        }

        if (options.Step is { } step)
        {
            // 切页要读数据库，不能在 UI 线程上阻塞等待；交给调度器异步执行。
            Dispatcher.UIThread.Post(async () => await shell.GoToAsync(step));
        }

        if (options.SnapshotPath is { } snapshot)
        {
            // 把当前窗口渲染成图片：文档截图与界面回归都用它，
            // 比外部截屏工具更可靠（不受屏幕分辨率与缩放影响）。
            Dispatcher.UIThread.Post(async () => await SaveSnapshotAsync(snapshot));
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ShowStartupError(string message)
    {
        switch (Avalonia.Application.Current?.ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                desktop.MainWindow = new Window
                {
                    Title = "知识产权申请材料工具 · 启动失败",
                    Width = 560,
                    Height = 340,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    CanResize = false,
                    Content = new StartupErrorView(message)
                };
                break;

            case IActivityApplicationLifetime activity:
                activity.MainViewFactory = () => new StartupErrorView(message);
                break;

            case ISingleViewApplicationLifetime singleView:
                singleView.MainView = new StartupErrorView(message);
                break;
        }
    }

    private static async Task SaveSnapshotAsync(string path)
    {
        await Task.Delay(1500);

        var window = Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (window is null)
        {
            return;
        }

        var scale = window.RenderScaling;
        var dpi = scale * 96;
        var size = window.Bounds.Size;
        var pixelSize = new PixelSize(
            (int)Math.Ceiling(size.Width * scale),
            (int)Math.Ceiling(size.Height * scale));

        using var target = new RenderTargetBitmap(pixelSize, new Vector(dpi, dpi));
        target.Render(window);

        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        target.Save(full, new PngBitmapEncoderOptions());
        Console.WriteLine($"[snapshot] saved={full} size={pixelSize.Width}x{pixelSize.Height}");
    }
}
