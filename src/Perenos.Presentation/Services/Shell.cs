using System.Diagnostics;
using Perenos.Core;

namespace Perenos.Services;

public static class Shell
{
    public static void Open(string target)
    {
        try
        {
            if (OperatingSystem.IsMacOS())
                Process.Start(new ProcessStartInfo("open") { ArgumentList = { target } });
            else
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Could not open {target}.", ex);
        }
    }
}
