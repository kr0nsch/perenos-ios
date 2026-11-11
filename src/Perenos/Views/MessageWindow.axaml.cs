using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Perenos.Services;

namespace Perenos.Views;

public sealed partial class MessageWindow : Window
{
    public MessageWindow() => InitializeComponent();

    public MessageWindow(string title, string body, string confirm, string cancel)
    {
        InitializeComponent();
        Title = title;
        Heading.Text = title;
        Body.Text = body;
        ConfirmButton.Content = confirm;
        ContinueButton.Content = cancel;
    }

    public static Task<bool> ConfirmAsync(Window owner) =>
        new MessageWindow(Loc.Get("Close_Title"), Loc.Get("Close_Body"), Loc.Get("Close_Quit"), Loc.Get("Close_Continue"))
            .ShowDialog<bool>(owner);

    private void Confirm(object? sender, RoutedEventArgs e) => Close(true);

    private void Cancel(object? sender, RoutedEventArgs e) => Close(false);
}
