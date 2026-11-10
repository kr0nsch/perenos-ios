using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Perenos.Core.Tests;

public class AppTests
{
    [Fact]
    public void SettingsRoundTrip()
    {
        var dir = TestPaths.TempDirectory();
        try
        {
            var file = Path.Combine(dir, "settings.json");
            var store = new SettingsStore(file);
            store.Current.Language = "ru-RU";
            store.Current.VersionPolicy = VersionPolicy.SameAsSource;
            store.Current.KeepDownloads = false;
            store.Save();

            var reloaded = new SettingsStore(file).Current;

            Assert.Equal("ru-RU", reloaded.Language);
            Assert.Equal(VersionPolicy.SameAsSource, reloaded.VersionPolicy);
            Assert.False(reloaded.KeepDownloads);
            Assert.Contains("\"SameAsSource\"", File.ReadAllText(file));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void CorruptSettingsFallBackToDefaults()
    {
        var dir = TestPaths.TempDirectory();
        try
        {
            var file = Path.Combine(dir, "settings.json");
            File.WriteAllText(file, "{ this is not json");

            var settings = new SettingsStore(file).Current;

            Assert.True(settings.AcquireFreeLicenses);
            Assert.Equal(VersionPolicy.Latest, settings.VersionPolicy);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void PortableMarkerKeepsDataNextToApp()
    {
        var dir = TestPaths.TempDirectory();
        try
        {
            Assert.False(AppPaths.Detect(dir).IsPortable);

            File.WriteAllText(Path.Combine(dir, AppPaths.PortableMarkerFileName), "");
            var paths = AppPaths.Detect(dir);

            Assert.True(paths.IsPortable);
            Assert.Equal(Path.Combine(dir, "Data"), paths.DataDirectory);
            Assert.StartsWith(paths.DataDirectory, paths.IpatoolHome);
            Assert.StartsWith(paths.DataDirectory, paths.DefaultDownloadDirectory);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void PreviousDataFolderMovesWithoutChangingItsFiles()
    {
        var root = TestPaths.TempDirectory();
        try
        {
            var legacy = Path.Combine(root, "old");
            var current = Path.Combine(root, "new");
            Directory.CreateDirectory(Path.Combine(legacy, ".ipatool"));
            File.WriteAllText(Path.Combine(legacy, ".ipatool", "account"), "encrypted session");

            AppPaths.MigrateDirectory(legacy, current);

            Assert.False(Directory.Exists(legacy));
            Assert.Equal("encrypted session", File.ReadAllText(Path.Combine(current, ".ipatool", "account")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void PreviousDataMergeNeverOverwritesCurrentFiles()
    {
        var root = TestPaths.TempDirectory();
        try
        {
            var legacy = Path.Combine(root, "old");
            var current = Path.Combine(root, "new");
            Directory.CreateDirectory(legacy);
            Directory.CreateDirectory(current);
            File.WriteAllText(Path.Combine(legacy, "settings.json"), "old settings");
            File.WriteAllText(Path.Combine(current, "settings.json"), "current settings");
            File.WriteAllText(Path.Combine(legacy, "library.ipa"), "saved app");

            AppPaths.MigrateDirectory(legacy, current);

            Assert.Equal("current settings", File.ReadAllText(Path.Combine(current, "settings.json")));
            Assert.Equal("saved app", File.ReadAllText(Path.Combine(current, "library.ipa")));
            Assert.Equal("old settings", File.ReadAllText(Path.Combine(legacy, "settings.json")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DefaultLibraryMovesAndSettingsUseTheNewPath()
    {
        var root = TestPaths.TempDirectory();
        try
        {
            var legacyData = Path.Combine(root, "legacy-data");
            var legacyDownloads = Path.Combine(root, "legacy-downloads");
            var currentData = Path.Combine(root, "current-data");
            var currentDownloads = Path.Combine(root, "current-downloads");
            Directory.CreateDirectory(legacyData);
            Directory.CreateDirectory(legacyDownloads);
            File.WriteAllText(Path.Combine(legacyDownloads, "Owned.ipa"), "licensed app");
            var legacySettings = new SettingsStore(Path.Combine(legacyData, "settings.json"));
            legacySettings.Current.DownloadDirectory = legacyDownloads;
            legacySettings.Save();
            var paths = new AppPaths(root, false, currentData, currentDownloads, legacyData, legacyDownloads);
            paths.EnsureCreated();
            var settings = new SettingsStore(paths.SettingsFile);

            paths.MigrateLegacyDownloadDirectory(settings);

            Assert.Equal(currentDownloads, settings.Current.DownloadDirectory);
            Assert.Equal("licensed app", File.ReadAllText(Path.Combine(currentDownloads, "Owned.ipa")));
            Assert.Equal(currentDownloads, new SettingsStore(paths.SettingsFile).Current.DownloadDirectory);
            Assert.Empty(paths.MigrationWarnings);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CustomLibraryPathIsNotMoved()
    {
        var root = TestPaths.TempDirectory();
        try
        {
            var custom = Path.Combine(root, "custom-library");
            var currentDownloads = Path.Combine(root, "current-downloads");
            var currentData = Path.Combine(root, "current-data");
            Directory.CreateDirectory(custom);
            var settings = new SettingsStore(Path.Combine(currentData, "settings.json"));
            settings.Current.DownloadDirectory = custom;
            var paths = new AppPaths(root, false, currentData, currentDownloads, Path.Combine(root, "legacy-data"), Path.Combine(root, "legacy-downloads"));

            paths.MigrateLegacyDownloadDirectory(settings);

            Assert.Equal(custom, settings.Current.DownloadDirectory);
            Assert.False(Directory.Exists(currentDownloads));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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

    /// <summary>Every UI string must exist in every language with the same placeholders.</summary>
    [Fact]
    public void TranslationsMatchEnglish()
    {
        var strings = Path.Combine(TestPaths.RepositoryRoot, "src", "Perenos", "Strings");
        var english = Load(Path.Combine(strings, "en-US", "Resources.resw"));
        foreach (var dir in Directory.GetDirectories(strings).Where(d => !d.EndsWith("en-US", StringComparison.Ordinal)))
        {
            var other = Load(Path.Combine(dir, "Resources.resw"));
            Assert.Empty(english.Keys.Except(other.Keys));
            Assert.Empty(other.Keys.Except(english.Keys));
            foreach (var (key, value) in english)
                Assert.True(Placeholders(value).SetEquals(Placeholders(other[key])), $"{Path.GetFileName(dir)}: placeholders differ for {key}");
        }
    }

    private static Dictionary<string, string> Load(string path) =>
        XDocument.Load(path).Root!.Elements("data").ToDictionary(e => (string)e.Attribute("name")!, e => (string)e.Element("value")!);

    private static HashSet<string> Placeholders(string text) => [.. Regex.Matches(text, @"\{\d+\}").Select(m => m.Value)];
}
