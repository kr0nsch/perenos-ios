using System.ComponentModel;
using Perenos.Core;
using Perenos.Services;
using Perenos.ViewModels;
using Perenos.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;
using Windows.Graphics;

namespace Perenos;

public sealed partial class MainWindow : Window
{
    private readonly AppServices _services;
    private readonly UIElement[] _steps;
    private bool _closeConfirmed;

    public MainWindow(AppServices services)
    {
        _services = services;
        ViewModel = new MainViewModel(services, DispatcherQueue);
        InitializeComponent();
        Title = Loc.Get("App_Name");
        AppTitleBar.Title = Title;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(Path.Combine(services.Paths.AppDirectory, "Assets", "Perenos.ico"));
        ConfigureSize();
        ApplyTheme(services.Settings.Current.Theme);

        var deviceStep = new DeviceStepView(ViewModel);
        _steps = [new StartView(ViewModel), new LaterView(ViewModel), deviceStep, new AppsStepView(ViewModel), deviceStep, new InstallStepView(ViewModel)];
        ShowStep(ViewModel.Step);

        ViewModel.PropertyChanged += OnViewModelChanged;
        AppWindow.Closing += OnClosing;

        services.Watcher.Start();
        _ = ViewModel.Account.RefreshAsync();
    }

    public MainViewModel ViewModel { get; }

    public ElementTheme CurrentElementTheme => RootGrid.ActualTheme;

    public void BringToFront()
    {
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
            presenter.Restore();
        Activate();
    }

    public void ApplyTheme(ThemePreference theme)
    {
        RootGrid.RequestedTheme = theme switch
        {
            ThemePreference.Light => ElementTheme.Light,
            ThemePreference.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
        AppWindow.TitleBar.PreferredTheme = theme switch
        {
            ThemePreference.Light => TitleBarTheme.Light,
            ThemePreference.Dark => TitleBarTheme.Dark,
            _ => TitleBarTheme.UseDefaultAppMode,
        };
    }

    public void Restart()
    {
        if (ViewModel.Install.IsRunning)
            return;
        AppInstance.GetCurrent().UnregisterKey();
        AppInstance.Restart("");
    }

    public async void ShowError(Exception ex)
    {
        try
        {
            var dialog = CreateDialog();
            dialog.Title = Loc.Get("Error_Title");
            dialog.Content = new TextBlock { Text = Loc.Format("Error_Body", ex.Message), TextWrapping = TextWrapping.Wrap };
            dialog.CloseButtonText = Loc.Get("Common_OK");
            await dialog.ShowAsync();
        }
        catch (Exception)
        {
            // Another dialog is already open; the error is in the log.
        }
    }

    private ContentDialog CreateDialog() => new()
    {
        XamlRoot = RootGrid.XamlRoot,
        RequestedTheme = RootGrid.ActualTheme,
        Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
    };

    private void ConfigureSize()
    {
        var scale = RootGrid.XamlRoot?.RasterizationScale ?? GetDpiScale();
        var width = (int)(940 * scale);
        var height = (int)(780 * scale);
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        width = Math.Min(width, area.Width);
        height = Math.Min(height, area.Height);
        AppWindow.MoveAndResize(new RectInt32(area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = (int)(680 * scale);
            presenter.PreferredMinimumHeight = (int)(600 * scale);
        }
    }

    private double GetDpiScale()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        return NativeMethods.GetDpiForWindow(hwnd) / 96.0;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Step))
            ShowStep(ViewModel.Step);
        else if (e.PropertyName == nameof(MainViewModel.PrimaryText))
        {
            var key = ViewModel.IsInstallStep && ViewModel.Install.IsRunning ? "DestructivePillButtonStyle" : "PillButtonStyle";
            PrimaryButton.Style = (Style)Application.Current.Resources[key];
        }
    }

    private void ShowStep(WizardStep step)
    {
        var view = _steps[(int)step];
        if (!ReferenceEquals(StepHost.Content, view))
            StepHost.Content = view;
    }

    private async void OnAccountClick(object sender, RoutedEventArgs e)
    {
        var account = ViewModel.Account;
        if (account.IsBusy || account.IsChecking)
            return;

        if (account.IsSignedIn)
        {
            var flyout = new MenuFlyout();
            flyout.Items.Add(new MenuFlyoutItem { Text = account.SignedInText, IsEnabled = false, Icon = new FontIcon { Glyph = "" } });
            flyout.Items.Add(new MenuFlyoutSeparator());
            var signOut = new MenuFlyoutItem { Text = Loc.Get("Account_SignOut"), Icon = new FontIcon { Glyph = "" }, Command = account.SignOutCommand };
            flyout.Items.Add(signOut);
            flyout.ShowAt(AccountButton);
            return;
        }

        var dialog = CreateDialog();
        dialog.Title = Loc.Get("SignIn_DialogTitle");
        dialog.CloseButtonText = Loc.Get("Common_Cancel");
        dialog.Content = new SignInPanel { Account = account, Width = 420 };
        void OnAccountChanged(object? s, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(AccountViewModel.State) && account.IsSignedIn)
                dialog.Hide();
        }

        account.PropertyChanged += OnAccountChanged;
        try
        {
            await dialog.ShowAsync();
        }
        finally
        {
            account.PropertyChanged -= OnAccountChanged;
        }
    }

    private async void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsDialog(_services, this)
        {
            XamlRoot = RootGrid.XamlRoot,
            RequestedTheme = RootGrid.ActualTheme,
        };
        try
        {
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            AppLog.Warn("Could not open settings.", ex);
        }

        await ViewModel.Library.RefreshAsync();
    }

    private async void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_closeConfirmed || !ViewModel.Install.IsRunning)
            return;

        args.Cancel = true;
        var dialog = CreateDialog();
        dialog.Title = Loc.Get("Close_Title");
        dialog.Content = new TextBlock { Text = Loc.Get("Close_Body"), TextWrapping = TextWrapping.Wrap };
        dialog.PrimaryButtonText = Loc.Get("Close_Quit");
        dialog.CloseButtonText = Loc.Get("Close_Continue");
        dialog.DefaultButton = ContentDialogButton.Close;
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        _closeConfirmed = true;
        ViewModel.Install.Cancel();
        for (var i = 0; i < 50 && ViewModel.Install.IsRunning; i++)
            await Task.Delay(100);
        Close();
    }
}

internal static partial class NativeMethods
{
    [System.Runtime.InteropServices.LibraryImport("user32.dll")]
    internal static partial uint GetDpiForWindow(IntPtr hwnd);
}
