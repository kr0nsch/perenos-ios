using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Perenos.Services;

namespace Perenos.Views;

public sealed partial class DeviceStepView : UserControl
{
    public DeviceStepView() => AvaloniaXamlLoader.Load(this);

    private void GetAppleDevices(object? sender, RoutedEventArgs e) =>
        Shell.Open("ms-windows-store://pdp/?ProductId=9NP83LWLPZ9K");
}
