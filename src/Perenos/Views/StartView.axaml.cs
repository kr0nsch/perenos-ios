using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Perenos.Views;

public sealed partial class StartView : UserControl
{
    public StartView() => AvaloniaXamlLoader.Load(this);
}
