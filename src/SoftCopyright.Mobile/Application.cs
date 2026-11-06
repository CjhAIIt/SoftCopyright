using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using SoftCopyright.Client;

namespace SoftCopyright.Mobile;

/// <summary>Android 应用入口：把 Avalonia 的 App 接到 Android 的 Application 上。</summary>
[Application]
public class MobileApplication : AvaloniaAndroidApplication<App>
{
    protected MobileApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).WithInterFont();
}
