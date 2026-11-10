using Perenos.Core;
using Perenos.Core.Devices;
using Perenos.Core.Ipatool;
using Perenos.Core.Storefront;
using Perenos.Services;
using Perenos.ViewModels;
using Microsoft.UI.Dispatching;

namespace Perenos.ViewModel.Tests;

public sealed class RestoreAppsViewModelTests
{
    [Fact]
    public async Task ListsAllPurchasesWithoutSelectingThemAutomatically()
    {
        var store = new FakeStore
        {
            Owned =
            [
                new OwnedApp(1, "app.listed", "Listed", "1.0", null),
                new OwnedApp(2, "app.installed", "Installed", "1.0", null),
                new OwnedApp(3, "app.removed", "Removed", "3.0", null),
            ],
        };
        var devices = new FakeDevices { Installed = [App(2, "Installed", "2.0")] };
        var vm = new RestoreAppsViewModel(devices, new RestorableAppScanner(store, new FakeCatalog { Available = new HashSet<long> { 1 } }), new IconCache(), new DispatcherQueue());

        await vm.LoadAsync(Target());

        Assert.Equal(3, vm.TotalCount);
        Assert.Equal(0, vm.SelectedCount);
        Assert.Equal("Restore_Update", vm.Visible.Single(item => item.App.AppStoreId == 2).Caption);
        Assert.Equal("2.0", vm.Visible.Single(item => item.App.AppStoreId == 2).App.Version);
        Assert.Equal("Restore_Reinstall", vm.Visible.Single(item => item.App.AppStoreId == 3).Caption);
        Assert.False(vm.Visible.Single(item => item.App.AppStoreId == 1).IsUnavailableInRussia);
        Assert.True(vm.Visible.Single(item => item.App.AppStoreId == 2).IsUnavailableInRussia);
        Assert.True(vm.Visible.Single(item => item.App.AppStoreId == 3).IsUnavailableInRussia);
    }

    [Fact]
    public async Task HistoryFailureNeverFallsBackToInstalledApps()
    {
        var store = new FakeStore { Error = new IpatoolException(IpatoolErrorKind.Unknown, "purchase history returned HTTP 401") };
        var devices = new FakeDevices { Installed = [App(2, "Installed", "2.0")] };
        var vm = new RestoreAppsViewModel(devices, new RestorableAppScanner(store, new FakeCatalog()), new IconCache(), new DispatcherQueue());

        await vm.LoadAsync(Target());

        Assert.Equal("Restore_HistoryFailed", vm.Error);
        Assert.Empty(vm.Visible);
        Assert.False(vm.HasSelection);
        Assert.False(vm.NeedsSignIn);
    }

    [Fact]
    public async Task ExpiredSessionCanBeRetriedAfterSignIn()
    {
        var store = new FakeStore { Error = new IpatoolException(IpatoolErrorKind.SessionExpired, "expired") };
        var vm = new RestoreAppsViewModel(new FakeDevices(), new RestorableAppScanner(store, new FakeCatalog()), new IconCache(), new DispatcherQueue());

        await vm.LoadAsync(Target());
        Assert.True(vm.NeedsSignIn);

        store.Error = null;
        store.Owned = [new OwnedApp(1, "app.one", "Owned", "1.0", null)];
        await vm.LoadAsync(Target());

        Assert.False(vm.NeedsSignIn);
        Assert.Null(vm.Error);
        Assert.Equal(1, vm.TotalCount);
    }

    [Fact]
    public async Task FiltersUnavailableAndRussianServicesWithoutSelectingThem()
    {
        var store = new FakeStore
        {
            Owned =
            [
                new OwnedApp(1, "app.listed", "Listed", "1.0", null),
                new OwnedApp(2, "app.installed", "Installed", "1.0", null),
                new OwnedApp(3, "app.removed", "Removed", "3.0", null),
            ],
        };
        var services = new[]
        {
            new Perenos.Core.Catalog.RussianServiceApp(3, "app.removed", "Removed", "Family", null, "other", null),
            new Perenos.Core.Catalog.RussianServiceApp(1054372220, "", "ВК Музыка", "ВК Музыка", "VK Музыка", "music", null),
        };
        var devices = new FakeDevices { Installed = [App(2, "Installed", "2.0")] };
        var vm = new RestoreAppsViewModel(devices, new RestorableAppScanner(store, new FakeCatalog { Available = new HashSet<long> { 1 } }, services), new IconCache(), new DispatcherQueue());
        await vm.LoadAsync(Target());

        Assert.Equal(4, vm.TotalCount);
        Assert.Equal(0, vm.SelectedCount);
        Assert.DoesNotContain(vm.Visible, item => item.App.AppStoreId == 1054372220);

        vm.SetFilterCommand.Execute("Unavailable");
        Assert.Equal(RestoreListFilter.Unavailable, vm.Filter);
        Assert.True(vm.IsUnavailableFilter);
        Assert.Equal(2, vm.Visible.Count);

        vm.SetFilterCommand.Execute("RussianServices");
        Assert.Equal(RestoreListFilter.RussianServices, vm.Filter);
        Assert.True(vm.IsRussianServicesFilter);
        Assert.Equal(2, vm.Visible.Count);
        var candidate = vm.Visible.Single(item => item.App.AppStoreId == 1054372220);
        Assert.Equal("Restore_CatalogCandidate", candidate.Caption);
        vm.ToggleSelectAllCommand.Execute(null);
        Assert.Equal(2, vm.SelectedCount);
        Assert.Equal(new long[] { 3, 1054372220 }, vm.SelectedApps.Select(app => app.AppStoreId.GetValueOrDefault()).Order().ToArray());

        vm.ClearSelection();
        vm.SearchText = "VK";
        Assert.Equal(0, vm.SelectedCount);
        Assert.Single(vm.Visible);
        vm.ToggleSelectAllCommand.Execute(null);
        Assert.Equal(1054372220, Assert.Single(vm.SelectedApps).AppStoreId);
    }

    private static DeviceItemViewModel Target() => new("target", _ => Task.CompletedTask);

    private static InstalledApp App(long id, string name, string version) =>
        new($"app.{id}", name, version, null, id, null, "owner@example.com", null, 100);

    private sealed class FakeStore : IAppStoreClient
    {
        public IReadOnlyList<OwnedApp> Owned { get; set; } = [];

        public Exception? Error { get; set; }

        public bool IsAvailable => true;

        public Task<IpatoolAccount?> GetAccountAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<OwnedApp>> GetOwnedAppsAsync(CancellationToken cancellationToken = default) =>
            Error is null ? Task.FromResult(Owned) : Task.FromException<IReadOnlyList<OwnedApp>>(Error);

        public Task<LoginResult> LoginAsync(string email, string password, string? authCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SignOutAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<string> DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeDevices : IDeviceService
    {
        public IReadOnlyList<InstalledApp> Installed { get; init; } = [];

        public Task<DeviceInfo> GetInfoAsync(string udid, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> TrustAsync(string udid, IProgress<DeviceTrust>? progress, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<InstalledApp>> GetUserAppsAsync(string udid, CancellationToken cancellationToken = default) => Task.FromResult(Installed);

        public Task LoadIconsAsync(string udid, IReadOnlyList<string> bundleIds, Action<string, byte[]> onIcon, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task InstallAsync(string udid, string ipaPath, IProgress<InstallProgress>? progress, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> UninstallAsync(string udid, string bundleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeCatalog : IStorefrontCatalog
    {
        public IReadOnlySet<long> Available { get; init; } = new HashSet<long>();

        public Task<IReadOnlySet<long>> GetAvailableAppIdsAsync(IEnumerable<long> appIds, string storefront, CancellationToken cancellationToken = default) => Task.FromResult(Available);
    }
}
