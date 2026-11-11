using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Perenos.Core;
using Perenos.Services;

namespace Perenos;

public sealed class App : Application
{
    public static App? Instance { get; private set; }

    public AppServices? Services { get; private set; }

    public MainWindow? MainWindow { get; private set; }

    public override void Initialize()
    {
        Instance = this;
        AvaloniaXamlLoader.Load(this);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => AppLog.Error("Unhandled exception.", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLog.Error("Unobserved task exception.", e.Exception);
            e.SetObserved();
        };
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;
        Services = AppServices.Create();
        Loc.ApplyLanguage(Services.Settings.Current.Language);
        MainWindow = new MainWindow(Services);
        desktop.MainWindow = MainWindow;
        desktop.Exit += (_, _) => Services.Dispose();
        ApplyTheme(Services.Settings.Current.Theme);
        Services.Watcher.Start();
        _ = MainWindow.ViewModel.Account.RefreshAsync();
        AppLog.Info($"Perenos {AppServices.Version} started on {Environment.OSVersion.Platform} ({Services.Paths.AppDirectory}).");
        base.OnFrameworkInitializationCompleted();
    }

    public void ApplyTheme(ThemePreference theme) =>
        RequestedThemeVariant = theme switch
        {
            ThemePreference.Light => Avalonia.Styling.ThemeVariant.Light,
            ThemePreference.Dark => Avalonia.Styling.ThemeVariant.Dark,
            _ => Avalonia.Styling.ThemeVariant.Default,
        };

    public void BringToFront()
    {
        if (MainWindow is null)
            return;
        MainWindow.Show();
        MainWindow.WindowState = Avalonia.Controls.WindowState.Normal;
        MainWindow.Activate();
    }
}
