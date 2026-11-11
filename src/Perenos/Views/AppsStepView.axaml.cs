using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

namespace Perenos.Views;

public sealed partial class AppsStepView : UserControl
{
    public AppsStepView()
    {
        AvaloniaXamlLoader.Load(this);
        SizeChanged += (_, _) => UpdateListHeight();
    }

    private void UpdateListHeight()
    {
        var listBox = this.GetVisualDescendants().OfType<ListBox>().Single();
        listBox.MaxHeight = Math.Max(128, Bounds.Height - 280);
    }
}
