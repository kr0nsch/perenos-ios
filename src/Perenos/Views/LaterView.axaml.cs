using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Perenos.ViewModels;

namespace Perenos.Views;

public sealed partial class LaterView : UserControl
{
    public LaterView() => AvaloniaXamlLoader.Load(this);

    private async void ShowSignIn(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && TopLevel.GetTopLevel(this) is Window owner)
            await new SignInWindow(viewModel.Account).ShowDialog(owner);
    }
}
