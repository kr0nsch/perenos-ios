using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Perenos.Services;

/// <summary>A bounded STA wait for activation forwarding, including failed or late completions.</summary>
internal sealed class ActivationForwarder : IDisposable
{
    private const uint DispatchCallsAndWindowMessages = 1 | 2;
    private const int CallPending = unchecked((int)0x80010115);
    private readonly object _gate = new();
    private readonly EventWaitHandle _done = new(false, EventResetMode.ManualReset);
    private bool _disposed;
    private Exception? _error;

    public ActivationForwarder(Func<Task> redirect)
    {
        ArgumentNullException.ThrowIfNull(redirect);
        Completion = Task.Run(async () =>
        {
            try
            {
                await redirect().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Observe failures even when they arrive after the startup wait timed out.
                Volatile.Write(ref _error, ex);
            }
            finally
            {
                lock (_gate)
                {
                    // A timed-out caller already closed the handle. Never signal it afterward.
                    if (!_disposed)
                        _done.Set();
                }
            }
        });
    }

    public Task Completion { get; }

    public Exception? Error => Volatile.Read(ref _error);

    internal SafeWaitHandle CompletionHandle => _done.SafeWaitHandle;

    public bool Wait(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds >= uint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        SafeWaitHandle handle;
        var added = false;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            handle = _done.SafeWaitHandle;
            handle.DangerousAddRef(ref added);
        }

        try
        {
            // Unlike Task.Wait, this continues to dispatch STA COM calls and window messages.
            var result = CoWaitForMultipleObjects(DispatchCallsAndWindowMessages,
                (uint)Math.Ceiling(timeout.TotalMilliseconds), 1, [handle.DangerousGetHandle()], out _);
            if (result == CallPending)
                return false;
            Marshal.ThrowExceptionForHR(result);
            return true;
        }
        finally
        {
            if (added)
                handle.DangerousRelease();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            _done.Dispose();
        }
    }

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoWaitForMultipleObjects(uint flags, uint timeout, uint count,
        [In] IntPtr[] handles, out uint index);
}
