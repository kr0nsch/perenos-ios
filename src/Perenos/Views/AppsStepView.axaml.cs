using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Perenos.ViewModels;

namespace Perenos.Views;

public sealed partial class AppsStepView : UserControl
{
    public AppsStepView()
    {
        AvaloniaXamlLoader.Load(this);
        SizeChanged += (_, _) => UpdateListHeight();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(DoubleTappedEvent, OnDoubleTapped, RoutingStrategies.Bubble);
    }

    /// <summary>Double-click a row to install just that app, without ticking checkboxes.</summary>
    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || (e.Source as Control)?.DataContext is not AppItemViewModel item || !item.IsSelectable)
            return;
        vm.AppList.ClearSelection();
        item.IsSelected = true;
        if (vm.PrimaryCommand.CanExecute(null))
        {
            vm.PrimaryCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>Ctrl+F search, F5 rescan, Enter continue — mirrors the on-screen buttons.</summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            SearchBox.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.F5)
        {
            vm.RetryAppsCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && e.Source is not TextBox && vm.PrimaryCommand.CanExecute(null))
        {
            vm.PrimaryCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void UpdateListHeight()
    {
        var listBox = this.GetVisualDescendants().OfType<ListBox>().Single();
        listBox.MaxHeight = Math.Max(128, Bounds.Height - 280);
    }
}
