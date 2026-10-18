using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using System;

namespace ProfessionalMinesweeper.Desktop;

public partial class App : Application
{
    public bool IsDarkTheme { get; private set; }

    public void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        RequestedThemeVariant = IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
    }
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        // Optional startup override, handy for scripts and for users who always
        // want dark mode: set PMS_THEME to "dark" or "light".
        if (string.Equals(Environment.GetEnvironmentVariable("PMS_THEME"), "dark", StringComparison.OrdinalIgnoreCase))
        {
            IsDarkTheme = true;
            RequestedThemeVariant = ThemeVariant.Dark;
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
