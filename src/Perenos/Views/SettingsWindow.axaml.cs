using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
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

    private void CloseWindow(object? sender, RoutedEventArgs e) => Close();
}
