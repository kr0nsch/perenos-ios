using System.Reflection;
using Perenos.Core;
using Perenos.Core.Catalog;
using Perenos.Core.Devices;
using Perenos.Core.Ipatool;
using Perenos.Core.Storefront;
using Perenos.Core.Transfer;
using Perenos.Demo;

namespace Perenos.Services;

/// <summary>Long-lived services shared by the whole app.</summary>
public sealed class AppServices : IDisposable
{
    private readonly string? _demoDownloads;

    private AppServices(AppPaths paths, SettingsStore settings, bool demo)
    {
        Paths = paths;
        IsDemo = demo;
        Settings = settings;
        IStorefrontCatalog storefront;

        if (demo)
        {
            // Simulated iPhones and App Store for UI work and screenshots; touches nothing real.
            var backend = new DemoBackend();
            AppStore = backend;
            Devices = backend;
            Watcher = backend;
            storefront = backend;
            _demoDownloads = Path.Combine(Path.GetTempPath(), "PerenosDemo");
            DemoBackend.PrepareSavedApps(_demoDownloads, HasFlag("--demo-saved") || DemoEnvironmentEnabled("DEMO_SAVED"));
        }
        else
        {
            AppStore = new IpatoolClient(paths.IpatoolExecutable, paths.IpatoolHome);
            Devices = new DeviceService(paths.PairRecordsDirectory);
            Watcher = new DeviceWatcher();
            storefront = new StorefrontCatalog();
        }

        Transfers = new TransferRunner(AppStore, Devices);
        StorefrontScanner = new UnavailableAppScanner(AppStore, storefront);
        RestorableAppScanner = new RestorableAppScanner(AppStore, storefront, RussianServiceCatalog.LoadDefault());
    }

    public static string Version { get; } =
        typeof(AppServices).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    public const string RepositoryUrl = "https://github.com/kr0nsch/perenos-ios";

    public AppPaths Paths { get; }

    public bool IsDemo { get; }

    public SettingsStore Settings { get; }

    public IAppStoreClient AppStore { get; }

    public IDeviceService Devices { get; }

    public IDeviceWatcher Watcher { get; }

    public TransferRunner Transfers { get; }

    public UnavailableAppScanner StorefrontScanner { get; }

    public RestorableAppScanner RestorableAppScanner { get; }

    public string IconsDirectory => _demoDownloads is null ? Paths.IconsDirectory : Path.Combine(Path.GetTempPath(), "PerenosDemoIcons");

    public string DownloadDirectory =>
        _demoDownloads
        ?? (string.IsNullOrWhiteSpace(Settings.Current.DownloadDirectory) ? Paths.DefaultDownloadDirectory : Settings.Current.DownloadDirectory);

    public static AppServices Create()
    {
        var paths = AppPaths.Detect();
        paths.EnsureCreated();
        AppLog.Initialize(paths.LogsDirectory);
        var settings = new SettingsStore(paths.SettingsFile);
        paths.MigrateLegacyDownloadDirectory(settings);
        foreach (var warning in paths.MigrationWarnings)
            AppLog.Warn(warning);
        var demo = HasFlag("--demo") || HasFlag("--demo-saved") || DemoEnvironmentEnabled("DEMO");
        return new AppServices(paths, settings, demo);
    }

    public void Dispose() => Watcher.Dispose();

    private static bool DemoEnvironmentEnabled(string suffix) =>
        Environment.GetEnvironmentVariable($"PERENOS_{suffix}") == "1"
        || Environment.GetEnvironmentVariable($"{AppPaths.LegacyProductFolderName.ToUpperInvariant()}_{suffix}") == "1";

    private static bool HasFlag(string flag) => Environment.GetCommandLineArgs().Any(a => a.Equals(flag, StringComparison.OrdinalIgnoreCase));
}
