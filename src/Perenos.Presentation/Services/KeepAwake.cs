using System.Runtime.InteropServices;

namespace Perenos.Services;

public static partial class KeepAwake
{
    private const uint EsContinuous = 0x80000000;
    private const uint EsSystemRequired = 0x00000001;
    private static uint _macAssertion;

    public static void Enable()
    {
        if (OperatingSystem.IsWindows())
            SetThreadExecutionState(EsContinuous | EsSystemRequired);
        else if (OperatingSystem.IsMacOS() && _macAssertion == 0)
        {
            var type = CreateString("PreventUserIdleSystemSleep");
            var name = CreateString("Perenos is transferring applications");
            try
            {
                IOPMAssertionCreateWithName(type, 255, name, ref _macAssertion);
            }
            finally
            {
                CFRelease(type);
                CFRelease(name);
            }
        }
    }

    public static void Disable()
    {
        if (OperatingSystem.IsWindows())
            SetThreadExecutionState(EsContinuous);
        else if (OperatingSystem.IsMacOS() && _macAssertion != 0)
        {
            IOPMAssertionRelease(_macAssertion);
            _macAssertion = 0;
        }
    }

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint flags);

    private static IntPtr CreateString(string value) =>
        CFStringCreateWithCString(IntPtr.Zero, System.Text.Encoding.UTF8.GetBytes(value + "\0"), 0x08000100);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, byte[] value, uint encoding);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr value);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    private static extern int IOPMAssertionCreateWithName(IntPtr assertionType, uint level, IntPtr name, ref uint assertionId);

    [DllImport("/System/Library/Frameworks/IOKit.framework/IOKit")]
    private static extern int IOPMAssertionRelease(uint assertionId);
}
