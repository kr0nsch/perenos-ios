using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Perenos.Services;

namespace Perenos.Views;

public sealed partial class DeviceStepView : UserControl
{
    private readonly DispatcherTimer _pulse = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly Stopwatch _clock = new();
    private Ellipse? _pulseA;
    private Ellipse? _pulseB;

    public DeviceStepView()
    {
        AvaloniaXamlLoader.Load(this);
        _pulseA = this.FindControl<Ellipse>("PulseA");
        _pulseB = this.FindControl<Ellipse>("PulseB");
        _pulse.Tick += (_, _) => Pulse();
        AttachedToVisualTree += (_, _) =>
        {
            _clock.Restart();
            _pulse.Start();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _pulse.Stop();
            _clock.Stop();
        };
    }

    private void GetAppleDevices(object? sender, RoutedEventArgs e) =>
        Shell.Open("ms-windows-store://pdp/?ProductId=9NP83LWLPZ9K");

    private void Pulse()
    {
        if (!IsEffectivelyVisible)
            return;
        var seconds = _clock.Elapsed.TotalSeconds;
        Animate(_pulseA, seconds % 2.2);
        Animate(_pulseB, (seconds + 1.1) % 2.2);
    }

    private static void Animate(Ellipse? ring, double phase)
    {
        if (ring is null)
            return;
        var fraction = phase / 2.2;
        var scale = 0.35 + 0.65 * fraction;
        if (ring.RenderTransform is ScaleTransform transform)
        {
            transform.ScaleX = scale;
            transform.ScaleY = scale;
        }
        ring.Opacity = 0.7 * (1 - fraction);
    }
}
