using System.Net;
using AppBridge.Core.Devices;
using AppBridge.Core.Ipatool;
using AppBridge.Core.Storefront;

namespace AppBridge.Core.Tests;

public sealed class UnavailableAppScannerTests
{
    [Fact]
    public async Task SignedOutDoesNotQueryCatalog()
    {
        var catalog = new FakeCatalog();
        var scanner = new UnavailableAppScanner(new FakeAppStore(null), catalog);

        var result = await scanner.ScanAsync([App(10)]);

        Assert.True(result.RequiresSignIn);
        Assert.Equal(0, result.OwnedCount);
        Assert.Empty(result.UnavailableAppIds);
        Assert.Equal(0, catalog.Calls);
    }

    [Fact]
    public async Task ChecksOnlyAppsOwnedByCurrentAccount()
    {
        var store = new FakeAppStore(new IpatoolAccount("Owner@Example.com", "Owner"));
        var catalog = new FakeCatalog { Available = new HashSet<long> { 10 } };
        var scanner = new UnavailableAppScanner(store, catalog);
        var apps = new[]
        {
            App(10, " owner@example.COM "),
            App(20, "someone-else@example.com"),
            App(30),
            App(30),
            App(null),
            App(-1),
        };

        var result = await scanner.ScanAsync(apps);

        Assert.False(result.RequiresSignIn);
        Assert.Equal(2, result.OwnedCount);
        Assert.Equal([30L], result.UnavailableAppIds.Order());
        Assert.Equal([10L, 30L], catalog.Requested);
        Assert.Equal("ru", catalog.Storefront);
    }

    [Fact]
    public async Task CatalogFailureIsNotReportedAsUnavailable()
    {
        var store = new FakeAppStore(new IpatoolAccount("owner@example.com", "Owner"));
        var catalog = new FakeCatalog { Error = new HttpRequestException("Offline") };
        var scanner = new UnavailableAppScanner(store, catalog);

        await Assert.ThrowsAsync<HttpRequestException>(() => scanner.ScanAsync([App(10)]));
    }

    private static InstalledApp App(long? id, string owner = "owner@example.com") =>
        new($"app.{id}", $"App {id}", "1.0", "Developer", id, null, owner, null, 100);

    private sealed class FakeAppStore(IpatoolAccount? account) : IAppStoreClient
    {
        public bool IsAvailable => true;

        public Task<IpatoolAccount?> GetAccountAsync(CancellationToken cancellationToken = default) => Task.FromResult(account);

        public Task<IReadOnlyList<OwnedApp>> GetOwnedAppsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<LoginResult> LoginAsync(string email, string password, string? authCode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SignOutAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<string> DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeCatalog : IStorefrontCatalog
    {
        public int Calls { get; private set; }

        public List<long> Requested { get; } = [];

        public string? Storefront { get; private set; }

        public IReadOnlySet<long> Available { get; init; } = new HashSet<long>();

        public Exception? Error { get; init; }

        public Task<IReadOnlySet<long>> GetAvailableAppIdsAsync(
            IEnumerable<long> appIds, string storefront, CancellationToken cancellationToken = default)
        {
            Calls++;
            Requested.AddRange(appIds);
            Storefront = storefront;
            if (Error is not null)
                throw Error;
            return Task.FromResult(Available);
        }
    }
}
