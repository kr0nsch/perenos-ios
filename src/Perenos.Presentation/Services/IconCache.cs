using System.Collections.Concurrent;
using Perenos.Core;
using Perenos.Core.Library;

namespace Perenos.Services;

/// <summary>Keeps original PNG bytes independent of the UI framework.</summary>
public sealed class IconCache(string folder)
{
    private readonly ConcurrentDictionary<string, byte[]> _png = new();

    public void Store(string bundleId, byte[] png)
    {
        _png[bundleId] = png;
        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(FilePath(bundleId), png);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Debug($"Could not keep the icon of {bundleId}: {ex.Message}");
        }
    }

    public async Task<byte[]?> GetAsync(string bundleId)
    {
        if (_png.TryGetValue(bundleId, out var cached))
            return cached;
        var png = await Task.Run(() => TryReadFile(bundleId));
        if (png is null || !_png.TryAdd(bundleId, png))
            return png;
        return png;
    }

    public async Task<byte[]?> GetAsync(SavedApp app)
    {
        if (await GetAsync(app.BundleId) is { } known)
            return known;
        var png = await Task.Run(() => SavedAppLibrary.ReadIcon(app.FilePath));
        if (png is null)
            return null;
        Store(app.BundleId, png);
        return png;
    }

    private string FilePath(string bundleId) =>
        Path.Combine(folder, string.Concat(bundleId.Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_')) + ".png");

    private byte[]? TryReadFile(string bundleId)
    {
        try
        {
            var path = FilePath(bundleId);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
