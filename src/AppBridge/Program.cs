using System.Diagnostics;
using System.Runtime.InteropServices;
using AppBridge.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using WinRT;

namespace AppBridge;

public static partial class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ComWrappersSupport.InitializeComWrappers();

        // A second launch brings the running window to the front instead of opening another
        // copy: two copies would fight over the same iPhones and download folder.
        var mainInstance = AppInstance.FindOrRegisterForKey("AppBridge.Main");
        if (!mainInstance.IsCurrent)
        {
            return RedirectAndActivate(mainInstance) ? 0 : 1;
        }

        mainInstance.Activated += (_, _) => App.Instance?.BringToFront();

        Application.Start(_p =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
        return 0;
    }

    /// <summary>
    /// Redirects without blocking the STA thread (pattern from the Windows App SDK docs), then
    /// uses this process's foreground right to raise the existing window.
    /// </summary>
    private static bool RedirectAndActivate(AppInstance target)
    {
        try
        {
            var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            using var forwarding = new ActivationForwarder(() => target.RedirectActivationToAsync(activation).AsTask());
            if (!forwarding.Wait(TimeSpan.FromSeconds(5)) || forwarding.Error is not null)
            {
                Debug.WriteLine($"Activation forwarding failed or timed out: {forwarding.Error}");
                return false;
            }

            using var process = Process.GetProcessById((int)target.ProcessId);
            if (process.MainWindowHandle != IntPtr.Zero)
                SetForegroundWindow(process.MainWindowHandle);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Activation forwarding failed: {ex}");
            return false;
        }
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hwnd);
}
