using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SoftCopyright.Desktop.Views;

/// <summary>启动失败时展示的窗口（例如本机数据目录不可写）。</summary>
public sealed class StartupErrorWindow : Window
{
    public StartupErrorWindow(string message)
    {
        Title = "知识产权申请材料工具 · 启动失败";
        Width = 560;
        Height = 320;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        CanResize = false;

        var hint = new TextBlock
        {
            Text = "常见原因：数据目录不可写、数据库文件被占用、或磁盘空间不足。",
            Classes = { "muted" },
            TextWrapping = TextWrapping.Wrap
        };

        Content = new StackPanel
        {
            Margin = new Thickness(28),
            Spacing = 14,
            Children =
            {
                new TextBlock
                {
                    Text = "无法启动本机引擎",
                    FontSize = 18,
                    FontWeight = FontWeight.SemiBold
                },
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.Parse("#F87171"))
                },
                hint
            }
        };
    }
}
