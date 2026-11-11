using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
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
