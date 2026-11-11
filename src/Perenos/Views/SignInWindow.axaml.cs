using System.ComponentModel;
using Avalonia.Controls;
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
        account.PropertyChanged += OnAccountChanged;
        Closed += (_, _) => account.PropertyChanged -= OnAccountChanged;
    }

    private void OnAccountChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AccountViewModel.State) && DataContext is AccountViewModel { IsSignedIn: true })
            Close();
    }
}
