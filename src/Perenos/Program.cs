using Avalonia;
using Perenos.Core;
using Perenos.Services;

namespace Perenos;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Any(arg => arg is "--demo" or "--demo-saved"))
            Environment.SetEnvironmentVariable("PERENOS_DEMO", "1");

        SingleInstance? instance = null;
        try
        {
            var paths = AppPaths.Detect();
            paths.EnsureCreated();
            instance = new SingleInstance(paths.DataDirectory);
            if (!instance.IsFirst)
                return instance.ActivateOther() ? 0 : 1;
            instance.Start(() => App.Instance?.BringToFront());

            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            AppLog.Error("Fatal application error.", ex);
            return 1;
        }
        finally
        {
            instance?.Dispose();
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
