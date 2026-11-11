using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Perenos.Core;
using Perenos.Services;
using Perenos.ViewModels;
using Perenos.Views;

[assembly: AvaloniaTestApplication(typeof(Perenos.Ui.Tests.TestAppBuilder))]

namespace Perenos.Ui.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        Environment.SetEnvironmentVariable("PERENOS_DEMO", "1");
        return AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}

public sealed class AppBuilderTests : IDisposable
{
    private readonly List<MainWindow> _windows = [];
    [AvaloniaFact]
    public async Task AppRowShowsSelectionWithoutCoveringTheIcon()
    {
        var window = Track(CreateWindow("en-US"));
        var vm = window.ViewModel;
        vm.TransferNowCommand.Execute(null);
        await Until(() => vm.Source?.IsReady == true);
        await vm.PrimaryCommand.ExecuteAsync(null);
        await Until(() => vm.Apps.LoadedFromUdid is not null);
        vm.Apps.SearchText = "Cloudy";
        var app = Assert.Single(vm.AppList.Visible);
        ToggleButton? toggle = null;
        await Until(() =>
        {
            window.UpdateLayout();
            toggle = window.GetVisualDescendants().OfType<ToggleButton>().FirstOrDefault(button => ReferenceEquals(button.DataContext, app));
            return toggle is not null;
        });

        toggle!.IsChecked = true;
        await Until(() => vm.IsPrimaryEnabled);

        Assert.True(app.IsSelected);
        Assert.True(toggle.IsChecked);
        Assert.Equal(1, vm.AppList.SelectedCount);
        var indicator = toggle.GetVisualDescendants().OfType<Border>().First(border => border.Width == 24 && border.Height == 24);
        var icon = toggle.GetVisualDescendants().OfType<Border>().First(border => border.Width == 44 && border.Height == 44);
        Assert.True(indicator.Bounds.Right <= icon.Bounds.Left);
        Assert.True(Assert.Single(indicator.GetVisualChildren().OfType<Border>()).IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public async Task PrimaryActionTextUsesOnAccentForeground()
    {
        var window = Track(CreateWindow("en-US"));
        var vm = window.ViewModel;
        vm.TransferNowCommand.Execute(null);
        await Until(() => vm.Source?.IsReady == true);
        await vm.PrimaryCommand.ExecuteAsync(null);
        await Until(() => vm.Apps.LoadedFromUdid is not null);
        var app = vm.AppList.Visible.First();
        app.IsSelected = true;
        await Until(() => vm.IsPrimaryEnabled);
        window.UpdateLayout();

        var primary = window.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("primary"));
        var label = primary.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == vm.PrimaryText);
        var icon = primary.GetVisualDescendants().OfType<PathIcon>().Single(path => path.Classes.Contains("on-accent"));
        var expected = Assert.IsType<Avalonia.Media.SolidColorBrush>(icon.Foreground);
        var actual = Assert.IsType<Avalonia.Media.SolidColorBrush>(label.Foreground);
        Assert.Equal(expected.Color, actual.Color);
        window.Close();
    }

    [AvaloniaFact]
    public void CompactNavigationHasAccessibleName()
    {
        var window = Track(CreateWindow("en-US", 820));
        window.UpdateLayout();

        Assert.False(window.FindControl<Border>("Sidebar")!.IsVisible);
        var navigation = window.FindControl<Button>("MobileNavigation")!;
        Assert.True(navigation.IsVisible);
        Assert.Equal(Loc.Get("Nav_Workspace"), AutomationProperties.GetName(navigation));
        window.Close();
    }

    [AvaloniaFact]
    public void RussianBrandTaglineStaysInsideSidebar()
    {
        var window = Track(CreateWindow("ru-RU"));
        window.UpdateLayout();
        var sidebar = window.FindControl<Border>("Sidebar")!;
        var tagline = window.GetVisualDescendants().OfType<TextBlock>()
            .Single(text => text.Text == Loc.Get("Shell_Tagline"));

        Assert.True(sidebar.IsVisible);
        var bottomRight = tagline.TranslatePoint(new Point(tagline.Bounds.Width, tagline.Bounds.Height), sidebar);
        Assert.NotNull(bottomRight);
        Assert.True(bottomRight.Value.X <= sidebar.Bounds.Width - sidebar.Padding.Right,
            $"Tagline ends at {bottomRight.Value.X}, sidebar content ends at {sidebar.Bounds.Width - sidebar.Padding.Right}.");
        Assert.True(bottomRight.Value.Y <= sidebar.Bounds.Height - sidebar.Padding.Bottom);
        window.Close();
    }

    [AvaloniaFact]
    public void CompactStartContentCanBeScrolledToRequirements()
    {
        var window = Track(CreateWindow("ru-RU"));
        window.Width = 820;
        window.Height = 640;
        window.UpdateLayout();
        var scroller = window.GetVisualDescendants().OfType<ScrollViewer>()
            .Single(scroll => scroll.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.Text == Loc.Get("Shell_Requirements")));
        var requirements = window.GetVisualDescendants().OfType<TextBlock>()
            .Single(text => text.Text == Loc.Get("Shell_Requirements"));
        var footer = window.GetVisualDescendants().OfType<TextBlock>()
            .Single(text => text.Text == window.ViewModel.FooterHint);

        scroller.Offset = new Vector(0, scroller.Extent.Height);
        window.UpdateLayout();
        var requirementBounds = BoundsIn(requirements, window);
        var footerBounds = BoundsIn(footer, window);
        Assert.True(requirementBounds.Bottom <= footerBounds.Top,
            $"Requirements end at {requirementBounds.Bottom}, footer starts at {footerBounds.Top}.");
        AssertVisibleTextWithinWindow(window);
        window.Close();
    }

    [AvaloniaFact]
    public async Task RussianDeviceAndAppListsFitCompactWindow()
    {
        var window = Track(CreateWindow("ru-RU"));
        window.Width = 820;
        window.Height = 640;
        var vm = window.ViewModel;
        vm.TransferNowCommand.Execute(null);
        await Until(() => vm.Source?.IsReady == true);
        window.UpdateLayout();
        AssertVisibleTextWithinWindow(window);
        var deviceProgress = window.GetVisualDescendants().OfType<ProgressBar>()
            .Single(progress => progress.IsIndeterminate && progress.GetVisualAncestors().OfType<Button>().Any());
        Assert.True(deviceProgress.Bounds.Width == 36,
            $"Bounds={deviceProgress.Bounds.Width}; Width={deviceProgress.Width}; MinWidth={deviceProgress.MinWidth}");

        await vm.PrimaryCommand.ExecuteAsync(null);
        await Until(() => vm.Apps.LoadedFromUdid is not null);
        window.UpdateLayout();
        Assert.NotEmpty(vm.AppList.Visible);
        AssertVisibleTextWithinWindow(window);
        window.Close();
    }

    [AvaloniaFact]
    public void RussianSettingsFitTheirWindow()
    {
        var window = Track(CreateWindow("ru-RU"));
        var settings = new SettingsWindow(window.ViewModel);
        settings.Show();
        settings.UpdateLayout();
        AssertVisibleTextWithinWindow(settings);
        settings.Close();
        window.Close();
    }

    [AvaloniaFact]
    public async Task DisabledPrimaryActionTextRemainsReadableInBothThemes()
    {
        var window = Track(CreateWindow("ru-RU"));
        var vm = window.ViewModel;
        vm.TransferNowCommand.Execute(null);
        await Until(() => vm.Source?.IsReady == true);
        await vm.PrimaryCommand.ExecuteAsync(null);
        await Until(() => vm.Apps.LoadedFromUdid is not null);
        window.UpdateLayout();
        var primary = window.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("primary"));
        var label = primary.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == vm.PrimaryText);
        var icon = primary.GetVisualDescendants().OfType<PathIcon>().Single(path => path.Classes.Contains("on-accent"));
        AssertDisabledContrast(window, primary, label);
        Assert.Equal(Assert.IsType<SolidColorBrush>(label.Foreground).Color,
            Assert.IsType<SolidColorBrush>(icon.Foreground).Color);

        await vm.Account.RefreshAsync();
        var signIn = new SignInWindow(vm.Account);
        signIn.Show();
        var signInButton = signIn.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("primary"));
        await Until(() => !signInButton.IsEffectivelyEnabled);
        var signInLabel = signIn.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == vm.Account.SignInButtonText);
        AssertDisabledContrast(signIn, signInButton, signInLabel);
        signIn.Close();
        window.Close();
    }

    private static void AssertDisabledContrast(Window window, Button button, TextBlock label)
    {
        Assert.False(button.IsEffectivelyEnabled);
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            Application.Current!.RequestedThemeVariant = theme;
            window.UpdateLayout();
            var foreground = Assert.IsType<SolidColorBrush>(label.Foreground).Color;
            var background = Assert.IsType<SolidColorBrush>(button.Background).Color;
            var surface = Assert.IsType<SolidColorBrush>(window.Background).Color;
            var effectiveBackground = Blend(background, surface, button.Opacity);
            var effectiveForeground = Blend(foreground, effectiveBackground, button.Opacity * label.Opacity);
            var contrast = Contrast(effectiveForeground, effectiveBackground);

            Assert.True(contrast >= 4.5, $"{theme} disabled primary contrast for '{label.Text}' is {contrast:F2}:1.");
        }
    }

    private static Color Blend(Color foreground, Color background, double opacity) => Color.FromRgb(
        (byte)Math.Round(foreground.R * opacity + background.R * (1 - opacity)),
        (byte)Math.Round(foreground.G * opacity + background.G * (1 - opacity)),
        (byte)Math.Round(foreground.B * opacity + background.B * (1 - opacity)));

    private static double Contrast(Color first, Color second)
    {
        var firstLuminance = Luminance(first);
        var secondLuminance = Luminance(second);
        return (Math.Max(firstLuminance, secondLuminance) + 0.05) / (Math.Min(firstLuminance, secondLuminance) + 0.05);
    }

    private static double Luminance(Color color)
    {
        static double Channel(byte value)
        {
            var normalized = value / 255.0;
            return normalized <= 0.04045 ? normalized / 12.92 : Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    private static Rect BoundsIn(Visual visual, Visual ancestor)
    {
        var origin = visual.TranslatePoint(default, ancestor);
        Assert.NotNull(origin);
        return new Rect(origin.Value, visual.Bounds.Size);
    }

    private static void AssertVisibleTextWithinWindow(Window window)
    {
        var viewport = new Rect(window.ClientSize);
        var controls = window.GetVisualDescendants().OfType<Control>().Where(control =>
            control.IsVisible && control.GetVisualAncestors().OfType<Visual>().All(ancestor => ancestor.IsVisible) &&
            (control is TextBlock text && !string.IsNullOrWhiteSpace(text.Text) || control is Button or TextBox or ComboBox));
        foreach (var control in controls)
        {
            var bounds = BoundsIn(control, window);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                continue;
            var description = control is TextBlock text ? text.Text : control.Name ?? control.GetType().Name;
            Assert.True(bounds.Left >= -0.01 && bounds.Right <= viewport.Width + 0.01,
                $"'{description}' has horizontal bounds {bounds}, window width is {viewport.Width}.");
            var verticallyScrollable = control.GetVisualAncestors().OfType<ScrollViewer>()
                .Any(scroll => scroll.VerticalScrollBarVisibility != Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled);
            if (!verticallyScrollable)
                Assert.True(bounds.Top >= -0.01 && bounds.Bottom <= viewport.Height + 0.01,
                    $"'{description}' has vertical bounds {bounds}, window height is {viewport.Height}.");
        }
    }

    [AvaloniaFact]
    public async Task SettingsUpdateComputedTextAndTheme()
    {
        var window = Track(CreateWindow("ru-RU"));
        var vm = window.ViewModel;
        Assert.Contains("Выберите", vm.NowSubtitle);
        var settings = new SettingsWindow(vm);
        settings.Show();
        var keepDownloads = settings.GetVisualDescendants().OfType<ToggleSwitch>().Single();
        Assert.Equal(Loc.Get("Options_Keep"), AutomationProperties.GetName(keepDownloads));
        settings.FindControl<ComboBox>("ThemeBox")!.SelectedIndex = (int)ThemePreference.Dark;
        settings.FindControl<ComboBox>("LanguageBox")!.SelectedIndex = 1;
        await Until(() => vm.NowSubtitle.StartsWith("Choose apps", StringComparison.Ordinal));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Home", vm.NavigationTitle);
        Assert.Equal(ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);
        var text = window.GetVisualDescendants().OfType<TextBlock>()
            .First(block => block.Text?.StartsWith("Choose apps on your old", StringComparison.Ordinal) == true);
        Assert.Equal(vm.NowSubtitle, text.Text);
        Assert.DoesNotContain("Выберите", text.Text);
        window.Close();
    }

    private MainWindow Track(MainWindow window)
    {
        _windows.Add(window);
        return window;
    }

    private static MainWindow CreateWindow(string language, double? width = null)
    {
        if (App.Instance?.MainWindow is { } existing)
        {
            existing.ViewModel.Services.Settings.Current.Language = language;
            Loc.ApplyLanguage(language);
            if (width.HasValue)
                existing.Width = width.Value;
            existing.Show();
            return existing;
        }

        Environment.SetEnvironmentVariable("PERENOS_DEMO", "1");
        Environment.SetEnvironmentVariable("PERENOS_LANGUAGE", language);
        var services = AppServices.Create();
        Loc.ApplyLanguage(language);
        var window = new MainWindow(services);
        if (width.HasValue)
            window.Width = width.Value;
        services.Watcher.Start();
        window.Show();
        return window;
    }

    public void Dispose()
    {
        foreach (var window in _windows)
        {
            window.Close();
            window.ViewModel.Services.Dispose();
            var directory = window.ViewModel.Services.Paths.DataDirectory;
            if (Directory.Exists(directory) && Path.GetFileName(directory).StartsWith("PerenosDemo-", StringComparison.Ordinal))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (condition())
            {
                Dispatcher.UIThread.RunJobs();
                return;
            }
            await Task.Delay(100);
            Dispatcher.UIThread.RunJobs();
        }
        throw new TimeoutException("The UI did not reach the expected state.");
    }
}
