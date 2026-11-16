using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Perenos.ViewModels;

namespace Perenos.Views;

public sealed partial class SignInWindow : Window
{
    public SignInWindow() => AvaloniaXamlLoader.Load(this);

    public SignInWindow(AccountViewModel account)
    {
        DataContext = account;
        AvaloniaXamlLoader.Load(this);
        if (OperatingSystem.IsMacOS())
        {
            ExtendClientAreaToDecorationsHint = false;
            DialogTitleBar.IsVisible = false;
        }
        account.PropertyChanged += OnAccountChanged;
        Closed += (_, _) => account.PropertyChanged -= OnAccountChanged;
    }

    private void DragTitleBar(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void CloseWindow(object? sender, RoutedEventArgs e) => Close();

    private void OnAccountChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AccountViewModel.State) && DataContext is AccountViewModel { IsSignedIn: true })
            Close();
    }
}
