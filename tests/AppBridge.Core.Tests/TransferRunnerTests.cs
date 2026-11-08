using System.Collections.Concurrent;
using AppBridge.Core.Devices;
using AppBridge.Core.Ipatool;
using AppBridge.Core.Library;
using AppBridge.Core.Transfer;

namespace AppBridge.Core.Tests;

public sealed class TransferRunnerTests : IDisposable
{
    private readonly string _downloads = TestPaths.TempDirectory();
    private readonly FakeStore _store = new();
    private readonly FakeDevices _devices = new();
    private readonly ConcurrentDictionary<string, TransferUpdate> _final = new();

    public void Dispose() => Directory.Delete(_downloads, true);

    [Fact]
    public async Task InstallsEveryApp()
    {
        var apps = new[] { App("a"), App("b"), App("c") };

        await Run(apps);

        Assert.All(apps, a => Assert.Equal(TransferStage.Completed, _final[a.PackageKey].Stage));
        Assert.Equal(["a", "b", "c"], _devices.Installed);
    }

    [Fact]
    public async Task NonFatalFailureDoesNotStopOthers()
    {
        _store.Failures["b"] = IpatoolErrorKind.LicenseRequired;

        await Run([App("a"), App("b"), App("c")]);

        Assert.Equal(TransferStage.Completed, _final[App("a").PackageKey].Stage);
        Assert.Equal(TransferFailure.NotPurchased, _final[App("b").PackageKey].Failure);
        Assert.Equal(TransferStage.Completed, _final[App("c").PackageKey].Stage);
    }

    [Fact]
    public async Task UnpluggedPhoneStopsTheRun()
    {
        _devices.Failures["b"] = DeviceErrorKind.Disconnected;

        await Run([App("a"), App("b"), App("c"), App("d")]);

        Assert.Equal(TransferStage.Completed, _final[App("a").PackageKey].Stage);
        Assert.Equal(TransferFailure.DeviceDisconnected, _final[App("b").PackageKey].Failure);
        Assert.Equal(TransferStage.Canceled, _final[App("d").PackageKey].Stage);
        Assert.DoesNotContain("d", _devices.Installed);
    }

    [Fact]
    public async Task SignedOutStopsTheRun()
    {
        _store.Failures["a"] = IpatoolErrorKind.NotSignedIn;

        await Run([App("a"), App("b")]);

        Assert.Equal(TransferFailure.NotSignedIn, _final[App("a").PackageKey].Failure);
        Assert.Equal(TransferStage.Canceled, _final[App("b").PackageKey].Stage);
        Assert.Single(_store.Requests);
    }

    [Fact]
    public async Task ExactVersionIsDownloadedOnceAndReused()
    {
        var app = App("a");
        var runner = new TransferRunner(_store, _devices);

        await Run([app], runner, VersionPolicy.SameAsSource);
        await Run([app], runner, VersionPolicy.SameAsSource);

        Assert.Single(_store.Requests);
        Assert.Equal("ext-a", _store.Requests.Single().ExternalVersionId);
        Assert.Equal(2, _devices.Installed.Count);
    }

    [Fact]
    public async Task LatestVersionIsReusedOnRetryWithinSession()
    {
        var app = App("a");
        var runner = new TransferRunner(_store, _devices);

        await Run([app], runner);
        await Run([app], runner);

        Assert.Single(_store.Requests);
        Assert.Null(_store.Requests.Single().ExternalVersionId);
    }

    [Fact]
    public async Task DownloadsAreDeletedWhenNotKept()
    {
        await Run([App("a")], keep: false);

        Assert.Empty(Directory.GetFiles(_downloads, "*.ipa"));
    }

    [Fact]
    public async Task CleanupFailureDoesNotTurnSuccessfulInstallIntoFailure()
    {
        string? temporaryFile = null;
        _devices.OnInstall = _ =>
        {
            temporaryFile = Directory.GetFiles(Path.Combine(_downloads, TransferRunner.StagingFolderName), "*.ipa").Single();
            File.SetAttributes(temporaryFile, FileAttributes.ReadOnly);
        };

        try
        {
            await Run([App("a")], keep: false);

            Assert.Equal(TransferStage.Completed, _final[App("a").PackageKey].Stage);
            Assert.Equal(["a"], _devices.Installed);
        }
        finally
        {
            if (temporaryFile is not null && File.Exists(temporaryFile))
                File.SetAttributes(temporaryFile, FileAttributes.Normal);
        }
    }

    [Fact]
    public async Task WrongPackageIsRejected()
    {
        _store.WrongBundle.Add("a");

        await Run([App("a")]);

        Assert.Equal(TransferStage.Failed, _final[App("a").PackageKey].Stage);
        Assert.Empty(_devices.Installed);
    }

    [Fact]
    public async Task CancellationMarksRemainingAppsCanceled()
    {
        using var cts = new CancellationTokenSource();
        _devices.OnInstall = bundle =>
        {
            if (bundle == "a")
                cts.Cancel();
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Run([App("a"), App("b")], token: cts.Token));

        Assert.Equal(TransferStage.Canceled, _final[App("b").PackageKey].Stage);
    }

    [Fact]
    public async Task SavingDownloadsWithReadableNamesAndInstallsNothing()
    {
        await new TransferRunner(_store, _devices).SaveAsync([App("a"), App("b")],
            new TransferOptions(_downloads, true, VersionPolicy.Latest, AcquireFreeLicenses: true),
            (app, update) => _final[app.PackageKey] = update, CancellationToken.None);

        Assert.All(new[] { "a", "b" }, id => Assert.Equal(TransferStage.Completed, _final[App(id).PackageKey].Stage));
        Assert.Empty(_devices.Installed);
        Assert.Equal(["App a 1.2.3.ipa", "App b 1.2.3.ipa"], Directory.GetFiles(_downloads, "*.ipa").Select(Path.GetFileName).Order());
    }

    [Fact]
    public async Task SavingAfterChangingFoldersWritesToTheNewFolder()
    {
        var runner = new TransferRunner(_store, _devices);
        var options = new TransferOptions(_downloads, true, VersionPolicy.Latest, AcquireFreeLicenses: true);
        await runner.SaveAsync([App("a")], options, (app, update) => _final[app.PackageKey] = update, CancellationToken.None);
        var original = Assert.Single(Directory.GetFiles(_downloads, "*.ipa"));
        var newFolder = Path.Combine(_downloads, "new-library");
        Directory.CreateDirectory(newFolder);

        await runner.SaveAsync([App("a")], options with { DownloadDirectory = newFolder },
            (app, update) => _final[app.PackageKey] = update, CancellationToken.None);

        Assert.Equal(TransferStage.Completed, _final[App("a").PackageKey].Stage);
        var saved = Assert.Single(Directory.GetFiles(newFolder, "*.ipa"));
        Assert.Equal("a", IpaPackage.Read(saved).BundleId);
        Assert.True(File.Exists(original), "changing the folder must preserve the original library");
    }

    [Fact]
    public async Task SavedAppsInstallWithoutDownloading()
    {
        var saved = new[] { "a", "b", "c" }.Select(id => SavedAppLibrary.Read(TestIpa.Create(_downloads, id))).ToList();
        File.Delete(saved[1].FilePath);

        await new TransferRunner(_store, _devices).InstallSavedAsync(saved, "target", (app, update) => _final[app.PackageKey] = update, CancellationToken.None);

        Assert.Empty(_store.Requests);
        Assert.Equal(["a", "c"], _devices.Installed);
        Assert.Equal(TransferFailure.FileMissing, _final[saved[1].ToInstalledApp().PackageKey].Failure);
        Assert.True(File.Exists(saved[0].FilePath), "saved files are kept after installing");
    }

    [Fact]
    public async Task SavingNeverOverwritesAnotherSavedVersion()
    {
        // A file kept by 1.0 under the package name, and a saved file with the readable name.
        var old = TestIpa.Create(_downloads, "a", version: "0.9");
        File.Move(old, Path.Combine(_downloads, TransferRunner.PackageFileName(App("a"), null)));
        var clash = TestIpa.Create(_downloads, "other.app", version: "1.2.3");
        File.Move(clash, Path.Combine(_downloads, "App a 1.2.3.ipa"));

        await new TransferRunner(_store, _devices).SaveAsync([App("a")],
            new TransferOptions(_downloads, true, VersionPolicy.Latest, AcquireFreeLicenses: true),
            (app, update) => _final[app.PackageKey] = update, CancellationToken.None);

        var versions = Directory.GetFiles(_downloads, "*.ipa").Select(f => (Path.GetFileName(f), IpaPackage.Read(f).BundleId, IpaPackage.Read(f).Version)).Order().ToList();
        Assert.Equal(
            [($"a_{100 + 'a'}.ipa", "a", "0.9"), ("App a (2) 1.2.3.ipa", "a", "1.2.3"), ("App a 1.2.3.ipa", "other.app", "1.2.3")],
            versions);
    }

    [Fact]
    public async Task SavingLongNamesResolvesCollisionsWithoutHanging()
    {
        var app = App("a") with { Name = new string('A', 80) };
        var clash = Path.Combine(_downloads, TransferRunner.SavedFileName(app.Name, "1.2.3", app.BundleId));
        File.Move(TestIpa.Create(_downloads, "other.app"), clash);
        var run = new TransferRunner(_store, _devices).SaveAsync([app],
            new TransferOptions(_downloads, true, VersionPolicy.Latest, AcquireFreeLicenses: true),
            (item, update) => _final[item.PackageKey] = update, CancellationToken.None);

        try
        {
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            // Release a broken collision loop before reporting the timeout, so the test never
            // leaves a worker spinning or prevents the remaining suite from finishing.
            if (!run.IsCompleted)
            {
                for (var attempt = 0; attempt < 200 && File.Exists(clash); attempt++)
                {
                    try
                    {
                        File.Delete(clash);
                    }
                    catch (IOException)
                    {
                        await Task.Delay(10);
                    }
                }
                await run.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }

        Assert.Equal(TransferStage.Completed, _final[app.PackageKey].Stage);
        Assert.Equal("other.app", IpaPackage.Read(clash).BundleId);
        var saved = Assert.Single(Directory.GetFiles(_downloads, "*.ipa"), path => path != clash);
        Assert.Equal("a", IpaPackage.Read(saved).BundleId);
        Assert.EndsWith(" (2) 1.2.3.ipa", saved);
    }

    [Fact]
    public async Task TransferWithoutKeepingNeverDeletesSavedFiles()
    {
        var saved = SavedAppLibrary.Read(TestIpa.Create(_downloads, "a"));
        File.Move(saved.FilePath, Path.Combine(_downloads, TransferRunner.PackageFileName(App("a"), "ext-a")));

        await Run([App("a")], policy: VersionPolicy.SameAsSource, keep: false);

        Assert.Equal(TransferStage.Completed, _final[App("a").PackageKey].Stage);
        Assert.Single(Directory.GetFiles(_downloads, "*.ipa"));
        Assert.Empty(Directory.GetFiles(Path.Combine(_downloads, TransferRunner.StagingFolderName)));
    }

    [Theory]
    [InlineData("Weather", "2.1", "com.example", "Weather 2.1.ipa")]
    [InlineData("A/B: C?", "1.0", "com.example", "A_B_ C_ 1.0.ipa")]
    [InlineData("...", "", "com.example", "com.example.ipa")]
    [InlineData("Weather", "../../1:2?", "com.example", "Weather _.._1_2_.ipa")]
    public void SavedNamesAreReadableAndSafe(string name, string version, string bundle, string expected) =>
        Assert.Equal(expected, TransferRunner.SavedFileName(name, version, bundle));

    [Theory]
    [InlineData("com.example.app", null, "com.example.app_42.ipa")]
    [InlineData("com.example.app", "999", "com.example.app_42_v999.ipa")]
    [InlineData("com.example.app", "../999", "com.example.app_42_v_999.ipa")]
    [InlineData("bad/name:with*chars", null, "bad_name_with_chars_42.ipa")]
    public void PackageNamesAreSafe(string bundle, string? version, string expected)
    {
        var app = new InstalledApp(bundle, "x", "1", null, 42, version, null, null, null);
        Assert.Equal(expected, TransferRunner.PackageFileName(app, version));
    }

    [Fact]
    public async Task UnknownBundleIdsAreTrackedByAppStoreId()
    {
        var first = new InstalledApp("", "First", "1.0", null, 101, null, null, null, null);
        var second = new InstalledApp("", "Second", "1.0", null, 102, null, null, null, null);

        await Run([first, second]);

        Assert.Equal(TransferStage.Completed, _final["id:101"].Stage);
        Assert.Equal(TransferStage.Completed, _final["id:102"].Stage);
        Assert.Equal(["101", "102"], _devices.Installed);
    }

    [Fact]
    public async Task UnknownBundleIdRejectsWrongAppStoreId()
    {
        _store.WrongAppIds.Add(101);
        var app = new InstalledApp("", "First", "1.0", null, 101, null, null, null, null);

        await Run([app]);

        Assert.Equal(TransferFailure.Unknown, _final["id:101"].Failure);
        Assert.Empty(_devices.Installed);
    }

    [Fact]
    public async Task CatalogCandidateUsesItsVerifiedLatestVersion()
    {
        var app = new InstalledApp("", "First", "1.0", null, 101, "old-version", null, null, null, LatestExternalVersionId: "verified-latest");

        await Run([app]);

        Assert.Equal("verified-latest", _store.Requests.Single().ExternalVersionId);
        Assert.Equal(TransferStage.Completed, _final[app.PackageKey].Stage);
    }

    private Task Run(IReadOnlyList<InstalledApp> apps, TransferRunner? runner = null, VersionPolicy policy = VersionPolicy.Latest,
        bool keep = true, CancellationToken token = default) =>
        (runner ?? new TransferRunner(_store, _devices)).RunAsync(
            apps, "target", new TransferOptions(_downloads, keep, policy, AcquireFreeLicenses: true),
            (app, update) => _final[app.PackageKey] = update, token);

    private static InstalledApp App(string id) => new(id, $"App {id}", "1.0", "Dev", 100 + id[0], $"ext-{id}", null, null, 1000);

    private sealed class FakeStore : IAppStoreClient
    {
        public ConcurrentDictionary<string, IpatoolErrorKind> Failures { get; } = new();

        public ConcurrentBag<string> WrongBundle { get; } = [];

        public HashSet<long> WrongAppIds { get; } = [];

        public ConcurrentQueue<DownloadRequest> Requests { get; } = new();

        public bool IsAvailable => true;

        public Task<IpatoolAccount?> GetAccountAsync(CancellationToken cancellationToken = default) => Task.FromResult<IpatoolAccount?>(new("a@b.c", "A"));

        public Task<IReadOnlyList<OwnedApp>> GetOwnedAppsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<LoginResult> LoginAsync(string email, string password, string? authCode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string> DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Enqueue(request);
            if (Failures.TryGetValue(request.BundleId, out var kind))
                throw new IpatoolException(kind, kind.ToString());
            progress?.Report(new DownloadProgress(DownloadPhase.Downloading, 0.5, 10));
            var dir = Path.GetDirectoryName(request.OutputPath)!;
            var appId = WrongAppIds.Contains(request.AppId) ? request.AppId + 1 : request.AppId;
            var built = TestIpa.Create(dir, WrongBundle.Contains(request.BundleId) ? "other.app" : request.BundleId, appName: appId.ToString(), appStoreId: appId.ToString());
            File.Move(built, request.OutputPath, overwrite: true);
            return Task.FromResult(request.OutputPath);
        }
    }

    private sealed class FakeDevices : IDeviceService
    {
        private readonly ConcurrentQueue<string> _installed = new();

        public ConcurrentDictionary<string, DeviceErrorKind> Failures { get; } = new();

        public Action<string>? OnInstall { get; set; }

        public List<string> Installed => [.. _installed];

        public Task<DeviceInfo> GetInfoAsync(string udid, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> TrustAsync(string udid, IProgress<DeviceTrust>? progress, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<InstalledApp>> GetUserAppsAsync(string udid, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task LoadIconsAsync(string udid, IReadOnlyList<string> bundleIds, Action<string, byte[]> onIcon, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> UninstallAsync(string udid, string bundleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public async Task InstallAsync(string udid, string ipaPath, IProgress<InstallProgress>? progress, CancellationToken cancellationToken = default)
        {
            var bundle = IpaPackage.Read(ipaPath).BundleId;
            OnInstall?.Invoke(bundle);
            cancellationToken.ThrowIfCancellationRequested();
            if (Failures.TryGetValue(bundle, out var kind))
                throw new DeviceOperationException(kind, kind.ToString());
            progress?.Report(new InstallProgress(InstallPhase.Uploading, 1));
            await Task.Yield();
            _installed.Enqueue(bundle);
        }
    }
}
