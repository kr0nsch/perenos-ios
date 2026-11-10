using System.Collections.Concurrent;
using Perenos.Core;
using Perenos.Core.Devices;
using Perenos.Core.Ipatool;
using Perenos.Core.Library;
using Perenos.Core.Tests;
using Perenos.Core.Transfer;
using Perenos.Services;
using Perenos.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;

namespace Perenos.ViewModel.Tests;

public sealed class InstallViewModelTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"perenos-vm-{Guid.NewGuid():N}");
    private readonly FakeStore _store = new();
    private readonly FakeDevices _devices = new();
    private readonly IconCache _icons = new();

    public InstallViewModelTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Theory]
    [InlineData(Flow.Now)]
    [InlineData(Flow.Save)]
    [InlineData(Flow.InstallSaved)]
    [InlineData(Flow.Restore)]
    public async Task StopDuringIconLoadingPreventsTransferAndKeepsAllAppsRetryable(Flow flow)
    {
        var icon = new TaskCompletionSource<ImageSource?>();
        _icons.Load = _ => icon.Task;
        var vm = CreateViewModel();
        var apps = new[] { App("a"), App("b"), App("c") };
        var run = Start(vm, apps, flow);

        vm.Cancel();
        icon.SetResult(null);
        await run.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(vm.WasCanceled);
        Assert.Equal(InstallState.Finished, vm.State);
        Assert.Equal(apps.Length, vm.Items.Count);
        Assert.Equal(apps.Length, vm.RetryableCount);
        Assert.All(vm.Items, item => Assert.Equal(TransferStage.Canceled, item.Stage));
        Assert.Empty(_store.Downloads);
        Assert.Empty(_devices.Installed);
    }

    [Fact]
    public async Task StopDoesNotWaitForAnUnfinishedIconRead()
    {
        var icon = new TaskCompletionSource<ImageSource?>();
        _icons.Load = _ => icon.Task;
        var vm = CreateViewModel();
        var run = vm.StartSaveAsync([App("a")]);

        vm.Cancel();
        try
        {
            await run.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            icon.TrySetResult(null);
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.True(vm.WasCanceled);
        Assert.Empty(_store.Downloads);
    }

    [Fact]
    public async Task StopDuringIconLoadingAlsoWorksAfterAPreviousRun()
    {
        var vm = CreateViewModel();
        await vm.StartSaveAsync([App("previous")]);
        var icon = new TaskCompletionSource<ImageSource?>();
        _icons.Load = _ => icon.Task;

        var run = vm.StartSaveAsync([App("a"), App("b")]);
        vm.Cancel();
        icon.SetResult(null);
        await run.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(vm.WasCanceled);
        Assert.Equal(2, vm.RetryableCount);
        Assert.Equal("previous", Assert.Single(_store.Downloads));
    }

    [Fact]
    public async Task RetryAfterStoppingIconLoadingUsesAFreshTokenAndTransfersEveryApp()
    {
        var icon = new TaskCompletionSource<ImageSource?>();
        _icons.Load = _ => icon.Task;
        var vm = CreateViewModel();
        var run = vm.StartAsync([App("a"), App("b")], Target());
        vm.Cancel();
        icon.SetResult(null);
        await run.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, vm.RetryableCount);
        await vm.RetryAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(vm.WasCanceled);
        Assert.Equal(2, vm.CompletedCount);
        Assert.Equal(0, vm.RetryableCount);
        Assert.Equal(["a", "b"], _store.Downloads);
        Assert.Equal(["a", "b"], _devices.Installed);
    }

    [Fact]
    public async Task QueuedProgressFromAnOldRunCannotChangeANewRun()
    {
        var queued = new Queue<Action>();
        var dispatcher = new DispatcherQueue
        {
            Handler = callback =>
            {
                queued.Enqueue(callback);
                return true;
            },
        };
        var vm = CreateViewModel(dispatcher);
        await vm.StartSaveAsync([App("a")]);
        Assert.NotEmpty(queued);

        vm.Reset();
        var icon = new TaskCompletionSource<ImageSource?>();
        _icons.Load = _ => icon.Task;
        var currentRun = vm.StartSaveAsync([App("a")]);
        while (queued.Count > 0)
            queued.Dequeue()();

        Assert.Equal(InstallState.Running, vm.State);
        Assert.Equal(0, vm.CompletedCount);
        Assert.All(vm.Items, item => Assert.False(item.IsCompleted));

        icon.SetResult(null);
        await currentRun.WaitAsync(TimeSpan.FromSeconds(5));
        while (queued.Count > 0)
            queued.Dequeue()();
        Assert.Equal(1, vm.CompletedCount);
    }

    [Fact]
    public async Task RestoreUsesLatestVersionAndNeverAcquiresLicenses()
    {
        var vm = CreateViewModel();
        vm.UseSourceVersion = true;
        vm.AcquireFreeLicenses = true;

        await vm.StartRestoreAsync([App("a")], Target());

        var request = Assert.Single(_store.Requests);
        Assert.Null(request.ExternalVersionId);
        Assert.False(request.AcquireLicense);
        Assert.Equal(Flow.Restore, vm.Flow);
    }

    private InstallViewModel CreateViewModel(DispatcherQueue? dispatcher = null) => new(
        new AppServices(_folder, new TransferRunner(_store, _devices)), _icons, dispatcher ?? new DispatcherQueue());

    private Task Start(InstallViewModel vm, IReadOnlyList<InstalledApp> apps, Flow flow) => flow switch
    {
        Flow.Save => vm.StartSaveAsync(apps),
        Flow.InstallSaved => vm.StartInstallSavedAsync(apps.Select(app => SavedAppLibrary.Read(TestIpa.Create(_folder, app.BundleId))).ToList(), Target()),
        Flow.Restore => vm.StartRestoreAsync(apps, Target()),
        _ => vm.StartAsync(apps, Target()),
    };

    private static InstalledApp App(string id) => new(id, $"App {id}", "1.0", null, 100 + id[0], null, null, null, null);

    private static DeviceItemViewModel Target() => new("target", _ => Task.CompletedTask);

    private sealed class FakeStore : IAppStoreClient
    {
        public ConcurrentQueue<string> Downloads { get; } = new();

        public ConcurrentQueue<DownloadRequest> Requests { get; } = new();

        public bool IsAvailable => true;

        public Task<IpatoolAccount?> GetAccountAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<OwnedApp>> GetOwnedAppsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<LoginResult> LoginAsync(string email, string password, string? authCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SignOutAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<string> DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Downloads.Enqueue(request.BundleId);
            Requests.Enqueue(request);
            var path = TestIpa.Create(Path.GetDirectoryName(request.OutputPath)!, request.BundleId);
            File.Move(path, request.OutputPath);
            return Task.FromResult(request.OutputPath);
        }
    }

    private sealed class FakeDevices : IDeviceService
    {
        public ConcurrentQueue<string> Installed { get; } = new();

        public Task<DeviceInfo> GetInfoAsync(string udid, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> TrustAsync(string udid, IProgress<DeviceTrust>? progress, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<InstalledApp>> GetUserAppsAsync(string udid, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task LoadIconsAsync(string udid, IReadOnlyList<string> bundleIds, Action<string, byte[]> onIcon, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task InstallAsync(string udid, string ipaPath, IProgress<InstallProgress>? progress, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Installed.Enqueue(IpaPackage.Read(ipaPath).BundleId);
            return Task.CompletedTask;
        }

        public Task<bool> UninstallAsync(string udid, string bundleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
