using AppBridge.Core;
using AppBridge.Core.Devices;
using AppBridge.Core.Ipatool;
using AppBridge.Core.Storefront;
using AppBridge.Services;
using AppBridge.ViewModels;
using Microsoft.UI.Dispatching;

namespace AppBridge.ViewModel.Tests;

public sealed class AppsViewModelTests
{
    [Fact]
    public async Task MarksUnavailableAppsAndFiltersList()
    {
        var store = new FakeStore { Account = new IpatoolAccount("owner@example.com", "Owner") };
        var catalog = new FakeCatalog { Available = new HashSet<long> { 10 } };
        var vm = Create(store, catalog);

        await vm.LoadAsync(Source());

        Assert.Equal("Storefront_UnavailableCount: 1", vm.StorefrontStatus);
        Assert.True(vm.HasUnavailableApps);
        Assert.False(vm.IsStorefrontScanning);
        Assert.True(vm.Visible.Single(a => a.Name == "Beta").IsUnavailableInRussia);
        Assert.False(vm.Visible.Single(a => a.Name == "Other Account").IsUnavailableInRussia);

        vm.ToggleUnavailableFilterCommand.Execute(null);

        Assert.Equal("Beta", Assert.Single(vm.Visible).Name);
        Assert.Equal("Storefront_ShowAll", vm.UnavailableFilterText);

        vm.ToggleUnavailableFilterCommand.Execute(null);

        Assert.Equal(3, vm.Visible.Count);
    }

    [Fact]
    public async Task AccountChangeStartsAnotherCheck()
    {
        var store = new FakeStore();
        var catalog = new FakeCatalog { Available = new HashSet<long> { 10 } };
        var vm = Create(store, catalog);
        await vm.LoadAsync(Source());

        Assert.Equal("Storefront_SignIn", vm.StorefrontStatus);
        Assert.Equal(0, catalog.Calls);

        store.Account = new IpatoolAccount("owner@example.com", "Owner");
        vm.CheckStorefront();

        Assert.Equal(1, catalog.Calls);
        Assert.True(vm.HasUnavailableApps);
        Assert.Equal("Storefront_UnavailableCount: 1", vm.StorefrontStatus);
        Assert.True(vm.Visible.Single(a => a.Name == "Beta").IsUnavailableInRussia);
    }

    [Fact]
    public async Task ResetCancelsPendingCheck()
    {
        var store = new FakeStore { Account = new IpatoolAccount("owner@example.com", "Owner") };
        var catalog = new FakeCatalog { WaitForCancellation = true };
        var vm = Create(store, catalog);
        await vm.LoadAsync(Source());
        Assert.True(vm.IsStorefrontScanning);

        vm.Reset();
        await catalog.Cancellation.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(vm.IsStorefrontScanning);
        Assert.False(vm.ShowStorefrontStatus);
        Assert.Empty(vm.Visible);
        Assert.True(catalog.WasCanceled);
    }

    [Fact]
    public async Task FailedCheckDoesNotMarkApps()
    {
        var store = new FakeStore { Account = new IpatoolAccount("owner@example.com", "Owner") };
        var catalog = new FakeCatalog { Error = new HttpRequestException("Offline") };
        var vm = Create(store, catalog);

        await vm.LoadAsync(Source());

        Assert.Equal("Storefront_CheckFailed", vm.StorefrontStatus);
        Assert.False(vm.IsStorefrontScanning);
        Assert.False(vm.HasUnavailableApps);
        Assert.All(vm.Visible, item => Assert.False(item.IsUnavailableInRussia));
    }

    private static AppsViewModel Create(FakeStore store, FakeCatalog catalog) =>
        new(new FakeDevices(), new UnavailableAppScanner(store, catalog), new IconCache(), new DispatcherQueue());

    private static DeviceItemViewModel Source() => new("source", _ => Task.CompletedTask);

    private static InstalledApp App(long? id, string name, string owner = "owner@example.com", long size = 100) =>
        new($"app.{id}", name, "1.0", "Developer", id, null, owner, null, size);

    private sealed class FakeDevices : IDeviceService
    {
        public Task<DeviceInfo> GetInfoAsync(string udid, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> TrustAsync(string udid, IProgress<DeviceTrust>? progress, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<InstalledApp>> GetUserAppsAsync(string udid, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<InstalledApp>>(
            [
                App(10, "Alpha", size: 100),
                App(20, "Beta", size: 200),
                App(30, "Other Account", "other@example.com", 300),
            ]);

        public Task LoadIconsAsync(string udid, IReadOnlyList<string> bundleIds, Action<string, byte[]> onIcon, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task InstallAsync(string udid, string ipaPath, IProgress<InstallProgress>? progress, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> UninstallAsync(string udid, string bundleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeStore : IAppStoreClient
    {
        public IpatoolAccount? Account { get; set; }

        public bool IsAvailable => true;

        public Task<IpatoolAccount?> GetAccountAsync(CancellationToken cancellationToken = default) => Task.FromResult(Account);

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

        public bool WasCanceled { get; private set; }

        public TaskCompletionSource Cancellation { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool WaitForCancellation { get; init; }

        public IReadOnlySet<long> Available { get; init; } = new HashSet<long>();

        public Exception? Error { get; init; }

        public async Task<IReadOnlySet<long>> GetAvailableAppIdsAsync(
            IEnumerable<long> appIds, string storefront, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Error is not null)
                throw Error;
            if (WaitForCancellation)
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    WasCanceled = true;
                    Cancellation.TrySetResult();
                    throw;
                }
            }
            return Available;
        }
    }
}
