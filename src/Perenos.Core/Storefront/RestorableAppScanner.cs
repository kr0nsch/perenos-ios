using Perenos.Core.Catalog;
using Perenos.Core.Devices;
using Perenos.Core.Ipatool;

namespace Perenos.Core.Storefront;

public sealed record RestorableApp(
    OwnedApp Owned,
    InstalledApp? Installed,
    bool IsUnavailableInRussia,
    bool InPurchaseHistory = true,
    string? ServiceFamily = null,
    string? ServiceAlias = null)
{
    public InstalledApp ToInstalledApp()
    {
        var app = Installed ?? new InstalledApp(
            Owned.BundleId, Owned.Name, Owned.Version, null, Owned.AppId, null, null, null, null);
        return app with { LatestExternalVersionId = Owned.LatestExternalVersionId ?? app.LatestExternalVersionId };
    }
}

/// <summary>Lists all owned iPhone apps, marks storefront availability and adds optional service candidates.</summary>
public sealed class RestorableAppScanner(
    IAppStoreClient appStore,
    IStorefrontCatalog catalog,
    IReadOnlyList<RussianServiceApp>? russianServices = null)
{
    public async Task<IReadOnlyList<RestorableApp>> ScanAsync(
        IReadOnlyList<InstalledApp> installedApps, string storefront = "ru", CancellationToken cancellationToken = default)
    {
        var owned = (await appStore.GetOwnedAppsAsync(cancellationToken).ConfigureAwait(false))
            .Where(app => app.AppId > 0)
            .GroupBy(app => app.AppId)
            .Select(group => group.First())
            .ToList();
        var listed = owned.Count > 0
            ? await catalog.GetAvailableAppIdsAsync(owned.Select(app => app.AppId), storefront, cancellationToken).ConfigureAwait(false)
            : new HashSet<long>();
        var installedById = installedApps.Where(app => app.AppStoreId is > 0)
            .GroupBy(app => app.AppStoreId!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        var installedByBundle = installedApps.GroupBy(app => app.BundleId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var services = (russianServices ?? []).Where(app => app.AppId > 0)
            .GroupBy(app => app.AppId)
            .ToDictionary(group => group.Key, group => group.First());
        var ownedIds = owned.Select(app => app.AppId).ToHashSet();

        var result = owned.Select(app => new RestorableApp(
            app,
            installedById.TryGetValue(app.AppId, out var installed) ? installed : installedByBundle.GetValueOrDefault(app.BundleId),
            !listed.Contains(app.AppId),
            InPurchaseHistory: true,
            ServiceFamily: services.TryGetValue(app.AppId, out var service) ? service.Family : null,
            ServiceAlias: service?.Alias)).ToList();

        result.AddRange(services.Values.Where(service => !ownedIds.Contains(service.AppId)).Select(service => new RestorableApp(
            new OwnedApp(service.AppId, service.BundleId, service.Name, "", null, service.ExternalVersionId),
            installedById.TryGetValue(service.AppId, out var installed) ? installed : installedByBundle.GetValueOrDefault(service.BundleId),
            IsUnavailableInRussia: true,
            InPurchaseHistory: false,
            ServiceFamily: service.Family,
            ServiceAlias: service.Alias)));
        return result;
    }
}
