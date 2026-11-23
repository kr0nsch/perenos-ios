using Perenos.Core;

namespace Perenos.Core.Tests;

public class MultiAccountTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"perenos-accounts-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    [Fact]
    public void AccountHomeIsIsolatedAndSanitized()
    {
        var paths = new AppPaths(_dir, true, _dir, Path.Combine(_dir, "Downloads"));
        var home = paths.AccountHome("User@Example.com");
        Assert.StartsWith(Path.Combine(_dir, "accounts"), home);
        Assert.NotEqual(paths.IpatoolHome, home);
        Assert.DoesNotContain(Path.DirectorySeparatorChar, Path.GetFileName(home));
        Assert.Equal(home, paths.AccountHome("User@Example.com"));
        Assert.NotEqual(home, paths.AccountHome("other@example.com"));
    }

    [Fact]
    public void SettingsRoundTripKnownAndActiveAccounts()
    {
        Directory.CreateDirectory(_dir);
        var file = Path.Combine(_dir, "settings.json");
        var store = new SettingsStore(file);
        store.Current.KnownAccounts.Add("a@example.com");
        store.Current.KnownAccounts.Add("b@example.com");
        store.Current.ActiveAccount = "b@example.com";
        store.Save();

        var reloaded = new SettingsStore(file);
        Assert.Equal(new[] { "a@example.com", "b@example.com" }, reloaded.Current.KnownAccounts);
        Assert.Equal("b@example.com", reloaded.Current.ActiveAccount);
    }
}