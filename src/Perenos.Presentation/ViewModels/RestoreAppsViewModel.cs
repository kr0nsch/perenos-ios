using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perenos.Core;
using Perenos.Core.Devices;
using Perenos.Core.Ipatool;
using Perenos.Core.Storefront;
using Perenos.Services;

namespace Perenos.ViewModels;

/// <summary>All owned apps and catalog candidates in one searchable list, without filters.</summary>
public sealed partial class RestoreAppsViewModel(
    IDeviceService devices,
    RestorableAppScanner scanner,
    IconCache icons,
    IUiDispatcher dispatcher) : AppListViewModel
{
    private CancellationTokenSource? _loading;
    private CancellationTokenSource? _versions;

    public string? LoadedFromUdid { get; private set; }

    public bool NeedsSignIn { get; private set; }

    public override string LoadingText => Loc.Get("Restore_Loading");

    public override string EmptyText => Loc.Get("Restore_Empty");

    public IReadOnlyList<InstalledApp> SelectedApps => All.Where(app => app.IsSelected).Select(app => app.App).ToList();

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

            var items = apps.Select(app =>
            {
                var installed = app.ToInstalledApp();
                return new AppItemViewModel(installed)
                {
                    Restore = app,
                    IsUnavailableInRussia = app.IsUnavailableInRussia,
                    OsWarning = OsCompatibility.RequiresNewerOs(installed.MinimumOsVersion, target.OsVersion)
                        ? Loc.Format("Restore_MinOs", installed.MinimumOsVersion)
                        : null,
                    Caption = Loc.Get(app.Installed?.IsOffloaded == true ? "Restore_Offloaded"
                        : !app.InPurchaseHistory ? "Restore_CatalogCandidate"
                        : app.Installed is null ? "Restore_Reinstall" : "Restore_Update"),
                };
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
        _versions?.Cancel();
        LoadedFromUdid = null;
        NeedsSignIn = false;
        IsLoading = false;
        Error = null;
        Clear();
    }

    /// <summary>Fills the version picker for one row: every build with its display version, date and minimum iOS.</summary>
    [RelayCommand]
    private async Task LoadVersionsAsync(AppItemViewModel item)
    {
        if (item.IsLoadingVersions || item.App.AppStoreId is not > 0)
            return;
        var versions = new CancellationTokenSource();
        _versions = versions;
        var token = versions.Token;
        item.IsLoadingVersions = true;
        void OnItemChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppItemViewModel.IsVersionsOpen) && !item.IsVersionsOpen)
                versions.Cancel();
        }
        item.PropertyChanged += OnItemChanged;
        try
        {
            var appId = item.App.AppStoreId!.Value;
            var ids = await scanner.GetVersionsAsync(appId, token);
            item.Versions.Clear();
            // Newest first in the picker; cap the list so the flyout stays usable.
            foreach (var id in ids.Reverse().Take(30))
            {
                AppVersionInfo info;
                try
                {
                    info = await scanner.GetVersionMetadataAsync(appId, id, token);
                }
                catch (IpatoolException) when (!token.IsCancellationRequested)
                {
                    info = new AppVersionInfo(id);
                }
                item.Versions.Add(info);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (IpatoolException ex) when (!token.IsCancellationRequested)
        {
            AppLog.Warn($"Could not list versions for {item.App.BundleId}: {ex.Message}");
        }
        finally
        {
            item.PropertyChanged -= OnItemChanged;
            if (ReferenceEquals(_versions, versions))
                _versions = null;
            versions.Dispose();
            item.IsLoadingVersions = false;
        }
    }

    /// <summary>Stops a running version-list load so the row's version button unblocks at once.</summary>
    [RelayCommand]
    private void CancelVersions() => _versions?.Cancel();

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
