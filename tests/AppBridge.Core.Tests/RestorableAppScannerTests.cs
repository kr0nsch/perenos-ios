using AppBridge.Core.Devices;
using AppBridge.Core.Ipatool;
using AppBridge.Core.Storefront;

namespace AppBridge.Core.Tests;

public sealed class RestorableAppScannerTests
{
    [Fact]
    public async Task IncludesAllPurchasesAndMarksAppsMissingFromTheStorefront()
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

        Assert.Equal([1L, 2L, 3L], result.Select(app => app.Owned.AppId));
        Assert.False(result[0].IsUnavailableInRussia);
        Assert.True(result[1].IsUnavailableInRussia);
        Assert.Same(installed, result[1].Installed);
        Assert.True(result[2].IsUnavailableInRussia);
        Assert.Null(result[2].Installed);
        Assert.Equal("Removed from device", result[2].ToInstalledApp().Name);
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
    public async Task AddsCatalogCandidatesWithoutDuplicatingPurchaseHistory()
    {
        var store = new FakeStore { Owned = [new OwnedApp(3, "app.three", "Owned", "1.0", null)] };
        var services = new[]
        {
            new AppBridge.Core.Catalog.RussianServiceApp(3, "app.three", "Owned", "Family", null, "other", null),
            new AppBridge.Core.Catalog.RussianServiceApp(1054372220, "", "ВК Музыка", "ВК Музыка", "VK Музыка", "music", null),
        };

        var result = await new RestorableAppScanner(store, new FakeCatalog(), services).ScanAsync([]);

        Assert.Equal(2, result.Count);
        Assert.Equal("Family", result[0].ServiceFamily);
        Assert.True(result[0].InPurchaseHistory);
        var candidate = result[1];
        Assert.Equal(1054372220, candidate.Owned.AppId);
        Assert.False(candidate.InPurchaseHistory);
        Assert.True(candidate.IsUnavailableInRussia);
        Assert.Equal("ВК Музыка", candidate.ServiceFamily);
    }

    [Fact]
    public async Task EmptyHistoryStillListsCandidatesWithoutQueryingStorefront()
    {
        var catalog = new FakeCatalog();
        var services = new[]
        {
            new AppBridge.Core.Catalog.RussianServiceApp(1054372220, "", "ВК Музыка", "ВК Музыка", "VK Музыка", "music", null),
        };

        var result = await new RestorableAppScanner(new FakeStore(), catalog, services).ScanAsync([]);

        var candidate = Assert.Single(result);
        Assert.False(candidate.InPurchaseHistory);
        Assert.Equal(1054372220, candidate.Owned.AppId);
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
