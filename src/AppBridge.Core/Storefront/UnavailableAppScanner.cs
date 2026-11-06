using AppBridge.Core.Devices;

namespace AppBridge.Core.Storefront;

public sealed record StorefrontScanResult(bool RequiresSignIn, int OwnedCount, IReadOnlySet<long> UnavailableAppIds);

/// <summary>Finds installed apps owned by the signed-in account but absent from a storefront.</summary>
public sealed class UnavailableAppScanner(IAppStoreClient appStore, IStorefrontCatalog catalog)
{
    public async Task<StorefrontScanResult> ScanAsync(
        IReadOnlyList<InstalledApp> apps, string storefront = "ru", CancellationToken cancellationToken = default)
    {
        var account = await appStore.GetAccountAsync(cancellationToken).ConfigureAwait(false);
        if (account is null || string.IsNullOrWhiteSpace(account.Email))
            return new StorefrontScanResult(RequiresSignIn: true, 0, new HashSet<long>());

        var owned = apps
            .Where(a => a.AppStoreId is > 0 && string.Equals(
                a.PurchaserAppleId?.Trim(), account.Email.Trim(), StringComparison.OrdinalIgnoreCase))
            .DistinctBy(a => a.AppStoreId)
            .ToList();
        if (owned.Count == 0)
            return new StorefrontScanResult(RequiresSignIn: false, 0, new HashSet<long>());

        var ids = owned.Select(a => a.AppStoreId!.Value).ToArray();
        var available = await catalog.GetAvailableAppIdsAsync(ids, storefront, cancellationToken).ConfigureAwait(false);
        var unavailable = ids.Where(id => !available.Contains(id)).ToHashSet();
        return new StorefrontScanResult(RequiresSignIn: false, owned.Count, unavailable);
    }
}
