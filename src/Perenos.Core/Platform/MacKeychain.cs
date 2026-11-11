using System.Runtime.InteropServices;
using System.Text;

namespace Perenos.Core.Ipatool;

/// <summary>Stores the ipatool passphrase in the current user's macOS login keychain.</summary>
internal static partial class MacKeychain
{
    private const int Success = 0;
    private const int ItemNotFound = -25300;
    private const string Service = "Perenos.ipatool.session";
    private const string Account = "ipatool-keychain-passphrase";

    public static string? Read()
    {
        var service = Encoding.UTF8.GetBytes(Service);
        var account = Encoding.UTF8.GetBytes(Account);
        var status = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)service.Length, service, (uint)account.Length, account, out var length, out var data, out _);
        if (status == ItemNotFound)
            return null;
        try
        {
            ThrowIfFailed(status, "read the login keychain");
            return Marshal.PtrToStringUTF8(data, checked((int)length))
                ?? throw new InvalidOperationException("The keychain returned no data.");
        }
        finally
        {
            if (data != IntPtr.Zero)
                SecKeychainItemFreeContent(IntPtr.Zero, data);
        }
    }

    public static void Write(string passphrase)
    {
        var service = Encoding.UTF8.GetBytes(Service);
        var account = Encoding.UTF8.GetBytes(Account);
        var secret = Encoding.UTF8.GetBytes(passphrase);
        var status = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)service.Length, service, (uint)account.Length, account, out _, out _, out var item);
        try
        {
            if (status == Success)
                status = SecKeychainItemModifyAttributesAndData(item, IntPtr.Zero, (uint)secret.Length, secret);
            else if (status == ItemNotFound)
                status = SecKeychainAddGenericPassword(IntPtr.Zero, (uint)service.Length, service, (uint)account.Length, account, (uint)secret.Length, secret, IntPtr.Zero);
            ThrowIfFailed(status, "save to the login keychain");
        }
        finally
        {
            if (item != IntPtr.Zero)
                CFRelease(item);
        }
    }

    private static void ThrowIfFailed(int status, string operation)
    {
        if (status != Success)
            throw new InvalidOperationException($"Could not {operation} (OSStatus {status}).");
    }

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainFindGenericPassword(IntPtr keychain, uint serviceLength, byte[] service, uint accountLength, byte[] account, out uint passwordLength, out IntPtr passwordData, out IntPtr item);

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainAddGenericPassword(IntPtr keychain, uint serviceLength, byte[] service, uint accountLength, byte[] account, uint passwordLength, byte[] password, IntPtr item);

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainItemModifyAttributesAndData(IntPtr item, IntPtr attributes, uint length, byte[] data);

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainItemFreeContent(IntPtr attributes, IntPtr data);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr value);
}
