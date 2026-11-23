using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Perenos.Core;
using Perenos.Services;
using Perenos.ViewModels;

namespace Perenos.Views;

public sealed partial class SettingsWindow : Window
{
    private readonly MainViewModel _viewModel = null!;
    private bool _loading = true;

    public SettingsWindow() => InitializeComponent();

    public SettingsWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        LanguageBox.ItemsSource = new[] { Loc.Get("Settings_LanguageSystem"), "English", "Русский" };
        LanguageBox.SelectedIndex = viewModel.Services.Settings.Current.Language switch { "en-US" => 1, "ru-RU" => 2, _ => 0 };
        ThemeBox.ItemsSource = new[] { Loc.Get("Settings_ThemeSystem"), Loc.Get("Settings_ThemeLight"), Loc.Get("Settings_ThemeDark") };
        ThemeBox.SelectedIndex = (int)viewModel.Services.Settings.Current.Theme;
        _loading = false;
        if (OperatingSystem.IsMacOS())
        {
            ExtendClientAreaToDecorationsHint = false;
            DialogTitleBar.IsVisible = false;
        }
    }

    private void DragTitleBar(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        if (e.Source is Avalonia.Interactivity.Interactive interactive &&
            interactive.FindAncestorOfType<Button>(includeSelf: true) is not null)
            return;
        BeginMoveDrag(e);
    }

    private void LanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading)
            return;
        var language = LanguageBox.SelectedIndex switch { 1 => "en-US", 2 => "ru-RU", _ => "" };
        _viewModel.Services.Settings.Current.Language = language;
        _viewModel.Services.Settings.Save();
        Loc.ApplyLanguage(language);
        _viewModel.RefreshLocalization();
        Close();
        App.Instance?.BringToFront();
    }

    private void ThemeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading)
            return;
        var theme = (ThemePreference)ThemeBox.SelectedIndex;
        _viewModel.Services.Settings.Current.Theme = theme;
        _viewModel.Services.Settings.Save();
        App.Instance?.ApplyTheme(theme);
    }

    private void OpenFolder(object? sender, RoutedEventArgs e) => Shell.Open(_viewModel.Services.DownloadDirectory);

    private void OpenGitHub(object? sender, RoutedEventArgs e) => Shell.Open(AppServices.RepositoryUrl);

    private void OpenLicenses(object? sender, RoutedEventArgs e)
    {
        var path = Path.Combine(_viewModel.Services.Paths.AppDirectory, "THIRD-PARTY-NOTICES.txt");
        Shell.Open(File.Exists(path) ? path : $"{AppServices.RepositoryUrl}/blob/main/THIRD-PARTY-NOTICES.txt");
    }

    private void OpenLogs(object? sender, RoutedEventArgs e) => Shell.Open(_viewModel.Services.Paths.LogsDirectory);

    private string? _updateUrl;

    private async void CheckUpdates(object? sender, RoutedEventArgs e)
    {
        UpdateStatus.Text = Loc.Get("Settings_Checking");
        UpdateLink.IsVisible = false;
        try
        {
            var checker = new UpdateChecker(new HttpClient());
            var app = await checker.CheckAppAsync(_viewModel.Services.AppVersion);
            var ipatool = await checker.CheckIpatoolAsync(ReadIpatoolCommit());
            if (app is null && ipatool is null)
            {
                UpdateStatus.Text = Loc.Get("Settings_UpToDate");
                _updateUrl = null;
                return;
            }

            var parts = new List<string>();
            if (app is not null)
                parts.Add(Loc.Format("Settings_UpdateAvailable", app.Component, app.Latest));
            if (ipatool is not null)
                parts.Add(Loc.Format("Settings_UpdateAvailable", ipatool.Component, ipatool.Latest));
            UpdateStatus.Text = string.Join("; ", parts);
            _updateUrl = app?.Url ?? ipatool?.Url;
            UpdateLink.IsVisible = _updateUrl is not null;
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Update check failed: {ex.Message}");
            UpdateStatus.Text = Loc.Get("Settings_UpdateFailed");
        }
    }

    private string ReadIpatoolCommit()
    {
        try
        {
            var path = Path.Combine(_viewModel.Services.Paths.AppDirectory, "ipatool.json");
            if (!File.Exists(path))
                return "";
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty("commit", out var commit) ? commit.GetString() ?? "" : "";
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
        {
            return "";
        }
    }

    private void OpenUpdate(object? sender, RoutedEventArgs e)
    {
        if (_updateUrl is not null)
            Shell.Open(_updateUrl);
    }

    private void CloseWindow(object? sender, RoutedEventArgs e) => Close();
}
