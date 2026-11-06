using System;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Avalonia.Android;
using SoftCopyright.Client.Services;

namespace SoftCopyright.Mobile;

/// <summary>
/// Android 宿主 Activity。
///
/// 引擎必须在 Avalonia 初始化**之前**就绪：在 UI 线程上阻塞等待异步任务
/// 会与 Avalonia 的同步上下文死锁，所以这里在 base.OnCreate 之前完成准备。
/// </summary>
[Activity(
    Label = "知识产权申请材料工具",
    Theme = "@style/MyTheme.NoActionBar",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode
                           | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize)]
public sealed class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Android 上 LocalApplicationData 不可用，改用应用专属的可写目录（内部存储，无需任何权限）。
        var files = FilesDir;
        var dataRoot = files is null ? null : Path.Combine(files.AbsolutePath, "SoftCopyright");

        AppBootstrap.PrepareEngine(Array.Empty<string>(), dataRoot);
        base.OnCreate(savedInstanceState);
    }
}
