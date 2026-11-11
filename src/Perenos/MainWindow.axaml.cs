using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Perenos.Services;
using Perenos.ViewModels;
using Perenos.Views;

namespace Perenos;

public sealed partial class MainWindow : Window
{
    private bool _closeConfirmed;

    public MainWindow() => InitializeComponent();

    public MainWindow(AppServices services)
    {
        ViewModel = new MainViewModel(services, new AvaloniaUiDispatcher())
        {
            FolderPicker = PickFolderAsync,
        };
        DataContext = ViewModel;
        InitializeComponent();
        UpdateNavigation();
        SizeChanged += (_, _) => UpdateNavigation();
        Closing += OnClosing;
    }

    public MainViewModel ViewModel { get; } = null!;

    private void UpdateNavigation()
    {
        var compact = Bounds.Width < 1000;
        Sidebar.IsVisible = !compact;
        MobileNavigation.IsVisible = compact;
        ((Grid)Content!).ColumnDefinitions[0].Width = new GridLength(compact ? 0 : 232);
    }

    private async void ShowSignIn(object? sender, RoutedEventArgs e) =>
        await new SignInWindow(ViewModel.Account).ShowDialog(this);

    private async void ShowSettings(object? sender, RoutedEventArgs e)
    {
        await new SettingsWindow(ViewModel).ShowDialog(this);
        await ViewModel.Library.RefreshAsync();
    }

    private async Task<string?> PickFolderAsync()
    {
        if (!StorageProvider.CanOpen)
            return null;
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = Loc.Get("Settings_Folder"),
        });
        return folders.Count == 1 ? folders[0].Path.LocalPath : null;
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed || !ViewModel.Install.IsRunning)
            return;
        e.Cancel = true;
        if (!await MessageWindow.ConfirmAsync(this))
            return;
        _closeConfirmed = true;
        ViewModel.Install.Cancel();
        for (var i = 0; i < 50 && ViewModel.Install.IsRunning; i++)
            await Task.Delay(100);
        Close();
    }
}
