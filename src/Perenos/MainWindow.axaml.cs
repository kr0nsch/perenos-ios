using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
        // Windows draws its own caption buttons in the title bar row; macOS keeps the native
        // title bar with traffic lights, so the custom brand and captions stay hidden there.
        if (OperatingSystem.IsMacOS())
        {
            ExtendClientAreaToDecorationsHint = false;
            BrandPanel.IsVisible = false;
            CaptionButtons.IsVisible = false;
        }
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty)
            {
                MaximizeGlyph.IsVisible = WindowState != WindowState.Maximized;
                RestoreGlyph.IsVisible = WindowState == WindowState.Maximized;
            }
        };
        Closing += OnClosing;
    }

    public MainViewModel ViewModel { get; } = null!;

    private void TitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void TitleBarDoubleTapped(object? sender, TappedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void MinimizeWindow(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void ToggleMaximize(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseWindow(object? sender, RoutedEventArgs e) => Close();

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
