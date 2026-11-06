using Avalonia.Controls;

namespace SoftCopyright.Client.Views;

/// <summary>桌面端窗口外壳：只是给 <see cref="ShellView" /> 套一层窗口。</summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
