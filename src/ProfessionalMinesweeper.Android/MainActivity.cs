using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;

namespace ProfessionalMinesweeper.Android;

[Activity(Label = "专家扫雷", MainLauncher = true, Icon = "@drawable/app_icon", Theme = "@style/MyTheme.NoActionBar",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity
{
}

[Application(Label = "专家扫雷", Icon = "@drawable/app_icon")]
public sealed class AndroidApplication : AvaloniaAndroidApplication<MobileApp>
{
    public AndroidApplication(nint javaReference, global::Android.Runtime.JniHandleOwnership transfer)
        : base(javaReference, transfer) { }
}
