using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Perenos.ViewModels;

namespace Perenos.Views;

public sealed partial class SignInPanel : UserControl
{
    public SignInPanel() => AvaloniaXamlLoader.Load(this);

    private async void SubmitOnEnter(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not AccountViewModel account || !account.SignInCommand.CanExecute(null))
            return;
        e.Handled = true;
        await account.SignInCommand.ExecuteAsync(null);
    }
}
