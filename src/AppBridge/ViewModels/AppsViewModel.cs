using AppBridge.Core;
using AppBridge.Core.Devices;
using AppBridge.Core.Library;
using AppBridge.Services;
using Microsoft.UI.Dispatching;

namespace AppBridge.ViewModels;

/// <summary>Step 2: the App Store apps found on the source iPhone and which ones to copy or save.</summary>
public sealed partial class AppsViewModel(IDeviceService devices, IconCache icons, DispatcherQueue dispatcher) : AppListViewModel
{
    private CancellationTokenSource? _loading;
    private int _notFromStore;

    public string? LoadedFromUdid { get; private set; }

    public override string LoadingText => Loc.Get("Apps_LoadingText");

    public override string EmptyText => Loc.Get("Apps_EmptyText");

    public IReadOnlyList<InstalledApp> SelectedApps => All.Where(a => a.IsSelected).Select(a => a.App).ToList();

    public async Task LoadAsync(DeviceItemViewModel source)
    {
        if (LoadedFromUdid == source.Udid && Error is null)
            return;

        _loading?.Cancel();
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
        LoadedFromUdid = null;
        Error = null;
        IsLoading = false;
        Clear();
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
