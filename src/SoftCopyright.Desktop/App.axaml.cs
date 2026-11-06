using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SoftCopyright.Desktop.Services;
using SoftCopyright.Desktop.ViewModels;
using SoftCopyright.Desktop.Views;

namespace SoftCopyright.Desktop;

/// <summary>桌面端应用入口。</summary>
public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (AppBootstrap.StartupError is { } error || AppBootstrap.Engine is null)
            {
                desktop.MainWindow = new StartupErrorWindow(
                    AppBootstrap.StartupError?.Message ?? "业务引擎未能启动。");
                base.OnFrameworkInitializationCompleted();
                return;
            }

            var engine = AppBootstrap.Engine;
            desktop.ShutdownRequested += (_, _) => engine.DisposeAsync().AsTask().GetAwaiter().GetResult();

            var theme = new ThemeService(engine.DataRoot);
            var shell = new ShellViewModel(engine, theme);
            shell.IsDarkTheme = theme.IsDark;

            // 支持 --case <案例 ID> --step <步骤名>：启动后直接落到指定位置。
            var options = AppBootstrap.Options;
            if (options.CaseId is { } caseId)
            {
                var entity = engine.GetCaseAsync(caseId, CancellationToken.None).GetAwaiter().GetResult();
                if (entity is not null)
                {
                    shell.SetCase(entity);
                }
            }

            desktop.MainWindow = new MainWindow(shell);

            if (options.Step is { } step)
            {
                // 切页要读数据库，不能在 UI 线程上阻塞等待；交给调度器异步执行。
                Dispatcher.UIThread.Post(async () => await shell.GoToAsync(step));
            }

            if (options.SnapshotPath is { } snapshot)
            {
                // 把当前窗口渲染成图片：文档截图与界面回归都用它，
                // 比外部截屏工具更可靠（不受屏幕分辨率与缩放影响）。
                Dispatcher.UIThread.Post(async () =>
                {
                    await Task.Delay(1500);

                    var window = desktop.MainWindow;
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

                    var full = Path.GetFullPath(snapshot);
                    Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                    target.Save(full, new PngBitmapEncoderOptions());
                    Console.WriteLine($"[snapshot] saved={full} size={pixelSize.Width}x{pixelSize.Height}");
                });
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
