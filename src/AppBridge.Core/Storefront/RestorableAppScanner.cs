using AppBridge.Core.Devices;
using AppBridge.Core.Ipatool;

namespace AppBridge.Core.Storefront;

public sealed record RestorableApp(OwnedApp Owned, InstalledApp? Installed)
{
    public InstalledApp ToInstalledApp() => Installed ?? new InstalledApp(
        Owned.BundleId, Owned.Name, Owned.Version, null, Owned.AppId, null, null, null, null);
}

/// <summary>Finds owned iPhone apps missing from a storefront, whether installed or removed from the device.</summary>
public sealed class RestorableAppScanner(IAppStoreClient appStore, IStorefrontCatalog catalog)
{
    public async Task<IReadOnlyList<RestorableApp>> ScanAsync(
        IReadOnlyList<InstalledApp> installedApps, string storefront = "ru", CancellationToken cancellationToken = default)
    {
        var owned = (await appStore.GetOwnedAppsAsync(cancellationToken).ConfigureAwait(false))
            .Where(app => app.AppId > 0)
            .GroupBy(app => app.AppId)
            .Select(group => group.First())
            .ToList();
        if (owned.Count == 0)
            return [];

        var listed = await catalog.GetAvailableAppIdsAsync(owned.Select(app => app.AppId), storefront, cancellationToken).ConfigureAwait(false);
        var installedById = installedApps.Where(app => app.AppStoreId is > 0)
            .GroupBy(app => app.AppStoreId!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        var installedByBundle = installedApps.GroupBy(app => app.BundleId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        return owned.Where(app => !listed.Contains(app.AppId)).Select(app => new RestorableApp(
            app,
            installedById.TryGetValue(app.AppId, out var installed) ? installed : installedByBundle.GetValueOrDefault(app.BundleId))).ToList();
    }
}
