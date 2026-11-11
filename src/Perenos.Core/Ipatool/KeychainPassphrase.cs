using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Perenos.Core.Ipatool;

/// <summary>Stores the generated session passphrase with the operating system's user secret store.</summary>
internal static class KeychainPassphrase
{
    private const string KeyFileName = "keychain.key";
    // Persistent Windows encryption contract: renaming the product must not invalidate sessions.
    private static readonly byte[] Entropy = "AppBridge.ipatool.keychain.v1"u8.ToArray();

    public static string GetOrCreate(string ipatoolHome)
    {
        if (OperatingSystem.IsMacOS())
            return GetOrCreateMac(ipatoolHome);
        if (OperatingSystem.IsWindows())
            return GetOrCreateWindows(ipatoolHome);
        throw new PlatformNotSupportedException("This build supports Windows and macOS.");
    }

    [SupportedOSPlatform("windows")]
    private static string GetOrCreateWindows(string ipatoolHome)
    {
        var keyFile = Path.Combine(ipatoolHome, KeyFileName);
        if (File.Exists(keyFile))
        {
            try
            {
                var plain = ProtectedData.Unprotect(File.ReadAllBytes(keyFile), Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch (CryptographicException ex)
            {
                AppLog.Warn("The saved Apple Account session belongs to another Windows user or PC; a new sign-in is required.", ex);
            }
        }

        DeleteSavedAccount(ipatoolHome);
        var passphrase = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        Directory.CreateDirectory(ipatoolHome);
        File.WriteAllBytes(keyFile, ProtectedData.Protect(Encoding.UTF8.GetBytes(passphrase), Entropy, DataProtectionScope.CurrentUser));
        return passphrase;
    }

    private static string GetOrCreateMac(string ipatoolHome)
    {
        if (MacKeychain.Read() is { Length: > 0 } existing)
            return existing;
        var passphrase = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        MacKeychain.Write(passphrase);
        return passphrase;
    }

    private static void DeleteSavedAccount(string ipatoolHome)
    {
        var account = Path.Combine(ipatoolHome, ".ipatool", "account");
        try
        {
            if (File.Exists(account))
                File.Delete(account);
        }
        catch (IOException ex)
        {
            AppLog.Warn("Could not remove the old ipatool session.", ex);
        }
    }
}
