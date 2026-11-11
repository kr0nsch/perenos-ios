using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Perenos.Core.Tests;

[SupportedOSPlatform("windows")]
public sealed class WindowsAppTests
{
    [WindowsFact]
    public void KeychainPassphraseIsStableAndProtected()
    {
        var dir = TestPaths.TempDirectory();
        try
        {
            var first = Ipatool.KeychainPassphrase.GetOrCreate(dir);
            var second = Ipatool.KeychainPassphrase.GetOrCreate(dir);
            Assert.Equal(first, second);
            Assert.True(first.Length >= 40);
            Assert.DoesNotContain(first, File.ReadAllText(Path.Combine(dir, "keychain.key")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [WindowsFact]
    public void ExistingSessionRemainsReadableAfterProductRename()
    {
        var dir = TestPaths.TempDirectory();
        try
        {
            const string passphrase = "existing-perenos-session-passphrase";
            var legacyKey = ProtectedData.Protect(Encoding.UTF8.GetBytes(passphrase),
                "AppBridge.ipatool.keychain.v1"u8.ToArray(), DataProtectionScope.CurrentUser);
            File.WriteAllBytes(Path.Combine(dir, "keychain.key"), legacyKey);
            var account = Path.Combine(dir, ".ipatool", "account");
            Directory.CreateDirectory(Path.GetDirectoryName(account)!);
            File.WriteAllText(account, "existing encrypted account");

            Assert.Equal(passphrase, Ipatool.KeychainPassphrase.GetOrCreate(dir));
            Assert.Equal(legacyKey, File.ReadAllBytes(Path.Combine(dir, "keychain.key")));
            Assert.Equal("existing encrypted account", File.ReadAllText(account));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [WindowsFact]
    public void UnreadablePassphraseStartsFreshSession()
    {
        var dir = TestPaths.TempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, ".ipatool"));
            File.WriteAllText(Path.Combine(dir, ".ipatool", "account"), "old encrypted session");
            File.WriteAllBytes(Path.Combine(dir, "keychain.key"), [1, 2, 3]);
            var passphrase = Ipatool.KeychainPassphrase.GetOrCreate(dir);
            Assert.False(string.IsNullOrEmpty(passphrase));
            Assert.False(File.Exists(Path.Combine(dir, ".ipatool", "account")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
