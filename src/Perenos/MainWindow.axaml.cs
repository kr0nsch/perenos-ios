using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Perenos.Services;
using Perenos.ViewModels;
using Perenos.Views;

namespace Perenos;

public sealed partial class MainWindow : Window
{
    private bool _closeConfirmed;
    private Avalonia.Threading.DispatcherTimer? _stepTimer;
    private Avalonia.Controls.Control? _stepView;
    private readonly System.Diagnostics.Stopwatch _stepClock = new();

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
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Step))
                AnimateCurrentStep();
        };
        Closing += OnClosing;
    }

    public MainViewModel ViewModel { get; } = null!;

    private void AnimateCurrentStep()
    {
        var view = new Avalonia.Controls.Control[] { StartStep, LaterStep, DeviceStep, AppsStep, InstallStep }
            .FirstOrDefault(step => step.IsVisible);
        if (view is null)
            return;
        _stepTimer?.Stop();
        if (_stepView is not null && !ReferenceEquals(_stepView, view))
        {
            _stepView.Opacity = 1;
            _stepView.RenderTransform = null;
        }
        _stepView = view;
        var transform = new Avalonia.Media.TranslateTransform();
        view.RenderTransform = transform;
        view.Opacity = 0;
        _stepClock.Restart();
        _stepTimer ??= new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _stepTimer.Tick -= OnStepTick;
        _stepTimer.Tick += OnStepTick;
        _stepTimer.Start();
    }

    private void OnStepTick(object? sender, EventArgs e)
    {
        if (_stepView is null)
            return;
        var t = Math.Min(1, _stepClock.Elapsed.TotalMilliseconds / 280);
        var eased = 1 - Math.Pow(1 - t, 3);
        _stepView.Opacity = eased;
        if (_stepView.RenderTransform is Avalonia.Media.TranslateTransform transform)
            transform.Y = 14 * (1 - eased);
        if (t < 1)
            return;
        _stepTimer?.Stop();
        _stepView.Opacity = 1;
        _stepView.RenderTransform = null;
        _stepView = null;
    }

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
