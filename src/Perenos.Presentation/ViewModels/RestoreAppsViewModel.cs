using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perenos.Core;
using Perenos.Core.Devices;
using Perenos.Core.Ipatool;
using Perenos.Core.Storefront;
using Perenos.Services;

namespace Perenos.ViewModels;

public enum RestoreListFilter
{
    Purchases,
    Unavailable,
    RussianServices,
}

/// <summary>All owned apps, with optional filters for unavailable apps and Russian services.</summary>
public sealed partial class RestoreAppsViewModel(
    IDeviceService devices,
    RestorableAppScanner scanner,
    IconCache icons,
    IUiDispatcher dispatcher) : AppListViewModel
{
    private CancellationTokenSource? _loading;

    public string? LoadedFromUdid { get; private set; }

    public bool NeedsSignIn { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilterText), nameof(IsPurchasesFilter), nameof(IsUnavailableFilter), nameof(IsRussianServicesFilter))]
    public partial RestoreListFilter Filter { get; set; }

    public string FilterText => Loc.Format("Restore_FilterLabel", Filter switch
    {
        RestoreListFilter.Purchases => Loc.Get("Restore_FilterAll"),
        RestoreListFilter.Unavailable => Loc.Get("Restore_FilterUnavailable"),
        _ => Loc.Get("Restore_FilterRussian"),
    });

    public string FilterAllText => Loc.Get("Restore_FilterAll");

    public string FilterUnavailableText => Loc.Get("Restore_FilterUnavailable");

    public string FilterRussianText => Loc.Get("Restore_FilterRussian");

    public bool IsPurchasesFilter => Filter == RestoreListFilter.Purchases;

    public bool IsUnavailableFilter => Filter == RestoreListFilter.Unavailable;

    public bool IsRussianServicesFilter => Filter == RestoreListFilter.RussianServices;

    public override string LoadingText => Loc.Get("Restore_Loading");

    public override string EmptyText => Loc.Get("Restore_Empty");

    public IReadOnlyList<InstalledApp> SelectedApps => All.Where(app => app.IsSelected).Select(app => app.App).ToList();

    [RelayCommand]
    private void SetFilter(string filter) => Filter = Enum.Parse<RestoreListFilter>(filter);

    partial void OnFilterChanged(RestoreListFilter value)
    {
        ApplyFilter();
        OnPropertyChanged(nameof(SelectAllText));
    }

    protected override bool IncludeInFilter(AppItemViewModel item) => Filter switch
    {
        RestoreListFilter.Purchases => item.Restore?.InPurchaseHistory != false,
        RestoreListFilter.Unavailable => item.Restore?.InPurchaseHistory == true && item.IsUnavailableInRussia,
        _ => item.Restore?.ServiceFamily is not null,
    };

    public async Task LoadAsync(DeviceItemViewModel target)
    {
        if (LoadedFromUdid == target.Udid && Error is null)
            return;

        _loading?.Cancel();
        var loading = new CancellationTokenSource();
        _loading = loading;
        var token = loading.Token;
        LoadedFromUdid = null;
        NeedsSignIn = false;
        IsLoading = true;
        Error = null;
        Clear();
        try
        {
            var installed = await devices.GetUserAppsAsync(target.Udid, token);
            var apps = await scanner.ScanAsync(installed, cancellationToken: token);
            token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(_loading, loading))
                return;

            var items = apps.Select(app => new AppItemViewModel(app.ToInstalledApp())
            {
                Restore = app,
                IsUnavailableInRussia = app.IsUnavailableInRussia,
                Caption = Loc.Get(!app.InPurchaseHistory ? "Restore_CatalogCandidate" : app.Installed is null ? "Restore_Reinstall" : "Restore_Update"),
            }).ToList();
            SetItems(items);
            LoadedFromUdid = target.Udid;
            _ = LoadIconsAsync(target.Udid, apps.Where(app => app.Installed is not null).Select(app => app.Owned.BundleId).ToList(), token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (IpatoolException ex) when (!token.IsCancellationRequested && ReferenceEquals(_loading, loading))
        {
            AppLog.Warn($"Could not load purchase history: {ex.Kind} {ex.Message}");
            NeedsSignIn = ex.Kind is IpatoolErrorKind.NotSignedIn or IpatoolErrorKind.SessionExpired;
            Error = Loc.Get(ex.Kind == IpatoolErrorKind.Network ? "Restore_Network" : "Restore_HistoryFailed");
        }
        catch (DeviceOperationException ex) when (!token.IsCancellationRequested && ReferenceEquals(_loading, loading))
        {
            AppLog.Warn($"Could not read apps for restoration: {ex.Kind} {ex.Message}");
            Error = ex.Kind switch
            {
                DeviceErrorKind.Disconnected or DeviceErrorKind.ServiceUnavailable => Loc.Get("Apps_Error_Disconnected"),
                DeviceErrorKind.Locked => Loc.Get("Device_Status_Unlock"),
                DeviceErrorKind.NotTrusted => Loc.Get("Device_Status_TapTrust"),
                _ => Loc.Format("Apps_Error_Generic", ex.Message),
            };
        }
        catch (Exception ex) when (!token.IsCancellationRequested && ReferenceEquals(_loading, loading))
        {
            AppLog.Warn("Could not prepare the restoration list.", ex);
            Error = Loc.Get(ex is HttpRequestException or OperationCanceledException ? "Restore_Network" : "Restore_HistoryFailed");
        }
        finally
        {
            if (!token.IsCancellationRequested && ReferenceEquals(_loading, loading))
                IsLoading = false;
        }
    }

    public void Reset()
    {
        _loading?.Cancel();
        LoadedFromUdid = null;
        NeedsSignIn = false;
        IsLoading = false;
        Error = null;
        Filter = RestoreListFilter.Purchases;
        Clear();
    }

    private async Task LoadIconsAsync(string udid, IReadOnlyList<string> installedIds, CancellationToken token)
    {
        try
        {
            await devices.LoadIconsAsync(udid, installedIds, (bundleId, png) =>
            {
                if (token.IsCancellationRequested)
                    return;
                icons.Store(bundleId, png);
                dispatcher.TryEnqueue(async () =>
                {
                    if (token.IsCancellationRequested)
                        return;
                    var item = All.FirstOrDefault(app => app.App.BundleId == bundleId);
                    if (item is not null)
                        item.Icon = await icons.GetAsync(bundleId);
                });
            }, token);
            foreach (var item in All)
            {
                if (token.IsCancellationRequested)
                    return;
                if (!item.HasIcon)
                    item.Icon = await icons.GetAsync(item.App.BundleId);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or DeviceOperationException)
        {
            AppLog.Debug($"Icon loading stopped: {ex.Message}");
        }
    }
}
