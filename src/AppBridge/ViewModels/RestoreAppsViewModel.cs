using AppBridge.Core;
using AppBridge.Core.Devices;
using AppBridge.Core.Ipatool;
using AppBridge.Core.Storefront;
using AppBridge.Services;
using Microsoft.UI.Dispatching;

namespace AppBridge.ViewModels;

/// <summary>Owned apps absent from the Russian storefront, for updating or reinstalling on one iPhone.</summary>
public sealed partial class RestoreAppsViewModel(
    IDeviceService devices,
    RestorableAppScanner scanner,
    IconCache icons,
    DispatcherQueue dispatcher) : AppListViewModel
{
    private CancellationTokenSource? _loading;

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

            var items = apps.Select(app => new AppItemViewModel(app.ToInstalledApp())
            {
                IsSelected = true,
                IsUnavailableInRussia = true,
                Caption = Loc.Get(app.Installed is null ? "Restore_Reinstall" : "Restore_Update"),
            }).ToList();
            SetItems(items);
            LoadedFromUdid = target.Udid;
            _ = LoadIconsAsync(target.Udid, apps.Where(app => app.Installed is not null).Select(app => app.Owned.BundleId).ToList(), token);
        }
        catch (OperationCanceledException)
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
            Error = Loc.Get(ex is HttpRequestException ? "Restore_Network" : "Restore_HistoryFailed");
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
