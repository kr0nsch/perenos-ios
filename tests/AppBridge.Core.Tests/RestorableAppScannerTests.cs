using AppBridge.Core.Devices;
using AppBridge.Core.Ipatool;
using AppBridge.Core.Storefront;

namespace AppBridge.Core.Tests;

public sealed class RestorableAppScannerTests
{
    [Fact]
    public async Task IncludesInstalledUpdatesAndPurchasesRemovedFromTheDevice()
    {
        var installed = new InstalledApp("app.two", "Installed name", "1.2", "Developer", 2, "external-2", "owner@example.com", "16.0", 123);
        var store = new FakeStore
        {
            Owned =
            [
                new OwnedApp(1, "app.one", "Listed", "1.0", null),
                new OwnedApp(2, "app.two", "Old history name", "1.0", null),
                new OwnedApp(3, "app.three", "Removed from device", "3.0", null),
            ],
        };
        var catalog = new FakeCatalog { Available = new HashSet<long> { 1 } };

        var result = await new RestorableAppScanner(store, catalog).ScanAsync([installed]);

        Assert.Equal([2L, 3L], result.Select(app => app.Owned.AppId));
        Assert.Same(installed, result[0].Installed);
        Assert.Null(result[1].Installed);
        Assert.Equal("Removed from device", result[1].ToInstalledApp().Name);
        Assert.Equal([1L, 2L, 3L], catalog.Requested);
    }

    [Fact]
    public async Task MatchesInstalledAppByBundleWhenItsStoreMetadataIsIncomplete()
    {
        var installed = new InstalledApp("app.one", "Installed", "2.0", null, null, null, null, null, null);
        var store = new FakeStore { Owned = [new OwnedApp(1, "app.one", "History", "1.0", null)] };

        var result = await new RestorableAppScanner(store, new FakeCatalog()).ScanAsync([installed]);

        Assert.Same(installed, Assert.Single(result).Installed);
    }

    [Fact]
    public async Task EmptyHistoryDoesNotQueryCatalog()
    {
        var catalog = new FakeCatalog();

        var result = await new RestorableAppScanner(new FakeStore(), catalog).ScanAsync([]);

        Assert.Empty(result);
        Assert.Equal(0, catalog.Calls);
    }

    private sealed class FakeStore : IAppStoreClient
    {
        public IReadOnlyList<OwnedApp> Owned { get; init; } = [];

        public bool IsAvailable => true;

        public Task<IpatoolAccount?> GetAccountAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<OwnedApp>> GetOwnedAppsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Owned);

        public Task<LoginResult> LoginAsync(string email, string password, string? authCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SignOutAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<string> DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeCatalog : IStorefrontCatalog
    {
        public int Calls { get; private set; }

        public List<long> Requested { get; } = [];

        public IReadOnlySet<long> Available { get; init; } = new HashSet<long>();

        public Task<IReadOnlySet<long>> GetAvailableAppIdsAsync(IEnumerable<long> appIds, string storefront, CancellationToken cancellationToken = default)
        {
            Calls++;
            Requested.AddRange(appIds);
            return Task.FromResult(Available);
        }
    }
}
