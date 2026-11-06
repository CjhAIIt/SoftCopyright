using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SoftCopyright.Client.Views;

/// <summary>
/// 启动失败提示（桌面端放进窗口，手机端整屏显示）。
/// </summary>
public sealed class StartupErrorView : UserControl
{
    public StartupErrorView(string message)
    {
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
