using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perenos.Core;
using Perenos.Core.Devices;
using Perenos.Core.Library;
using Perenos.Services;
using Microsoft.UI.Dispatching;

namespace Perenos.ViewModels;

/// <summary>Step 2: the App Store apps found on the source iPhone and which ones to copy or save.</summary>
public sealed partial class AppsViewModel(
    IDeviceService devices,
    Core.Storefront.UnavailableAppScanner storefrontScanner,
    IconCache icons,
    DispatcherQueue dispatcher) : AppListViewModel
{
    private CancellationTokenSource? _loading;
    private CancellationTokenSource? _storefront;
    private int _notFromStore;

    public string? LoadedFromUdid { get; private set; }

    public override string LoadingText => Loc.Get("Apps_LoadingText");

    public override string EmptyText => Loc.Get("Apps_EmptyText");

    public IReadOnlyList<InstalledApp> SelectedApps => All.Where(a => a.IsSelected).Select(a => a.App).ToList();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowStorefrontStatus))]
    public partial string StorefrontStatus { get; set; } = "";

    public bool ShowStorefrontStatus => StorefrontStatus.Length > 0;

    [ObservableProperty]
    public partial bool IsStorefrontScanning { get; set; }

    [ObservableProperty]
    public partial bool HasUnavailableApps { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UnavailableFilterText))]
    public partial bool ShowUnavailableOnly { get; set; }

    public string UnavailableFilterText => Loc.Get(ShowUnavailableOnly ? "Storefront_ShowAll" : "Storefront_ShowUnavailable");

    public async Task LoadAsync(DeviceItemViewModel source)
    {
        if (LoadedFromUdid == source.Udid && Error is null)
            return;

        _loading?.Cancel();
        ResetStorefront();
        var loading = new CancellationTokenSource();
        _loading = loading;
        var token = loading.Token;
        LoadedFromUdid = null;

        IsLoading = true;
        Error = null;
        Clear();
        try
        {
            var apps = await devices.GetUserAppsAsync(source.Udid, token);
            token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(_loading, loading))
                return;
            _notFromStore = apps.Count(a => !a.IsFromAppStore);
            SetItems(apps.Where(a => a.IsFromAppStore).Select(a => new AppItemViewModel(a)).ToList());
            UpdateFootnote();
            LoadedFromUdid = source.Udid;
            _ = LoadIconsAsync(source.Udid, token);
            CheckStorefront();
        }
        catch (OperationCanceledException)
        {
        }
        catch (DeviceOperationException ex) when (!token.IsCancellationRequested && ReferenceEquals(_loading, loading))
        {
            AppLog.Warn($"Could not read apps: {ex.Kind} {ex.Message}");
            Error = ex.Kind switch
            {
                DeviceErrorKind.Disconnected or DeviceErrorKind.ServiceUnavailable => Loc.Get("Apps_Error_Disconnected"),
                DeviceErrorKind.Locked => Loc.Get("Device_Status_Unlock"),
                DeviceErrorKind.NotTrusted => Loc.Get("Device_Status_TapTrust"),
                _ => Loc.Format("Apps_Error_Generic", ex.Message),
            };
        }
        finally
        {
            if (!token.IsCancellationRequested && ReferenceEquals(_loading, loading))
                IsLoading = false;
        }
    }

    /// <summary>Marks apps already saved on this PC at the same or a newer version; null clears the marks.</summary>
    public void ShowSaved(LibraryScan? library)
    {
        foreach (var item in All)
        {
            var saved = library?.Apps.FirstOrDefault(s => s.BundleId == item.App.BundleId);
            item.Caption = saved is not null && SavedAppLibrary.CompareVersions(saved.Version, item.App.Version) >= 0 ? Loc.Get("Apps_AlreadySaved") : null;
        }
    }

    public void Reset()
    {
        _loading?.Cancel();
        ResetStorefront();
        LoadedFromUdid = null;
        Error = null;
        IsLoading = false;
        Clear();
    }

    /// <summary>Checks the loaded source iPhone again after the account changes.</summary>
    public void CheckStorefront()
    {
        if (All.Count == 0 || HasError)
            return;

        _storefront?.Cancel();
        foreach (var item in All)
            item.IsUnavailableInRussia = false;
        ShowUnavailableOnly = false;
        HasUnavailableApps = false;
        StorefrontStatus = Loc.Get("Storefront_Checking");
        IsStorefrontScanning = true;
        var scan = new CancellationTokenSource();
        _storefront = scan;
        _ = ScanStorefrontAsync(All.Select(a => a.App).ToList(), scan);
    }

    [RelayCommand]
    private void ToggleUnavailableFilter() => ShowUnavailableOnly = !ShowUnavailableOnly;

    partial void OnShowUnavailableOnlyChanged(bool value)
    {
        ApplyFilter();
        OnPropertyChanged(nameof(SelectAllText));
    }

    protected override bool IncludeInFilter(AppItemViewModel item) => !ShowUnavailableOnly || item.IsUnavailableInRussia;

    private async Task ScanStorefrontAsync(IReadOnlyList<InstalledApp> apps, CancellationTokenSource scan)
    {
        try
        {
            var result = await storefrontScanner.ScanAsync(apps, cancellationToken: scan.Token);
            scan.Token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(_storefront, scan))
                return;

            if (result.RequiresSignIn)
            {
                StorefrontStatus = Loc.Get("Storefront_SignIn");
            }
            else if (result.OwnedCount == 0)
            {
                StorefrontStatus = Loc.Get("Storefront_NoOwned");
            }
            else
            {
                foreach (var item in All)
                    item.IsUnavailableInRussia = item.App.AppStoreId is > 0 && result.UnavailableAppIds.Contains(item.App.AppStoreId.Value);
                HasUnavailableApps = result.UnavailableAppIds.Count > 0;
                StorefrontStatus = HasUnavailableApps
                    ? Loc.Plural("Storefront_UnavailableCount", result.UnavailableAppIds.Count)
                    : Loc.Get("Storefront_AllAvailable");
                ApplyFilter();
                OnPropertyChanged(nameof(SelectAllText));
            }
        }
        catch (OperationCanceledException) when (scan.Token.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (!scan.Token.IsCancellationRequested && ReferenceEquals(_storefront, scan))
        {
            AppLog.Warn($"Could not check the Russian App Store: {ex.Message}");
            StorefrontStatus = Loc.Get("Storefront_CheckFailed");
        }
        finally
        {
            if (!scan.Token.IsCancellationRequested && ReferenceEquals(_storefront, scan))
                IsStorefrontScanning = false;
        }
    }

    private void ResetStorefront()
    {
        _storefront?.Cancel();
        _storefront = null;
        foreach (var item in All)
            item.IsUnavailableInRussia = false;
        ShowUnavailableOnly = false;
        HasUnavailableApps = false;
        IsStorefrontScanning = false;
        StorefrontStatus = "";
    }

    private void UpdateFootnote() => Footnote = _notFromStore > 0 ? Loc.Plural("Apps_NotFromStore", _notFromStore) : "";

    private async Task LoadIconsAsync(string udid, CancellationToken token)
    {
        try
        {
            var ids = All.Select(a => a.App.BundleId).ToList();
            await devices.LoadIconsAsync(udid, ids, (bundleId, png) =>
            {
                if (token.IsCancellationRequested)
                    return;
                icons.Store(bundleId, png);
                dispatcher.TryEnqueue(async () =>
                {
                    if (token.IsCancellationRequested)
                        return;
                    var item = All.FirstOrDefault(a => a.App.BundleId == bundleId);
                    if (item is null)
                        return;
                    var icon = await icons.GetAsync(bundleId);
                    if (!token.IsCancellationRequested)
                        item.Icon = icon;
                });
            }, token);
        }
        catch (Exception ex) when (ex is OperationCanceledException or DeviceOperationException)
        {
            AppLog.Debug($"Icon loading stopped: {ex.Message}");
        }
    }
}
