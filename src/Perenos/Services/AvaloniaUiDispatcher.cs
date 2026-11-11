using Avalonia.Threading;
using Perenos.Core;

namespace Perenos.Services;

public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public bool TryEnqueue(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return true;
        }

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                AppLog.Error("Unhandled UI callback exception.", ex);
            }
        });
        return true;
    }
}
