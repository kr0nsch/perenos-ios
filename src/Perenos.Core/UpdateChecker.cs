using System.Security.Cryptography;
using System.Text.Json;

namespace Perenos.Core;

/// <summary>An available update for the app or the bundled ipatool engine.</summary>
public sealed record AppUpdateInfo(string Component, string Latest, string Url);

/// <summary>
/// Checks GitHub for a newer Perenos release or ipatool commit, and downloads artifacts
/// only after verifying their SHA-256, so an update can never install a tampered package.
/// </summary>
public sealed class UpdateChecker(HttpClient http)
{
    public async Task<AppUpdateInfo?> CheckAppAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        var json = await GetJsonAsync("https://api.github.com/repos/kr0nsch/perenos-ios/releases/latest", cancellationToken).ConfigureAwait(false);
        var tag = json.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
        var url = json.TryGetProperty("html_url", out var u) ? u.GetString() : null;
        if (string.IsNullOrWhiteSpace(tag))
            return null;
        var latest = tag.TrimStart('v', 'V');
        return IsNewer(currentVersion, latest) ? new AppUpdateInfo("Perenos", latest, url ?? "") : null;
    }

    public async Task<AppUpdateInfo?> CheckIpatoolAsync(string currentCommit, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentCommit))
            return null;
        var json = await GetJsonAsync("https://api.github.com/repos/majd/ipatool/commits/main", cancellationToken).ConfigureAwait(false);
        var sha = json.TryGetProperty("sha", out var s) ? s.GetString() : null;
        if (string.IsNullOrWhiteSpace(sha) || sha.StartsWith(currentCommit, StringComparison.OrdinalIgnoreCase))
            return null;
        return new AppUpdateInfo("ipatool", sha[..Math.Min(7, sha.Length)], $"https://github.com/majd/ipatool/commit/{sha}");
    }

    /// <summary>True when <paramref name="latest"/> is a strictly newer semantic version.</summary>
    public static bool IsNewer(string current, string latest)
    {
        if (!Version.TryParse(Pad(current), out var c) || !Version.TryParse(Pad(latest), out var l))
            return false;
        return l > c;
    }

    private static string Pad(string version)
    {
        var parts = version.Trim().Split('.', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (parts.Count is < 2 or > 4)
            return version;
        while (parts.Count < 3)
            parts.Add("0");
        return string.Join('.', parts);
    }

    /// <summary>Downloads <paramref name="url"/> and writes it only if its SHA-256 matches.</summary>
    public async Task DownloadVerifiedAsync(string url, string expectedSha256, string destination, CancellationToken cancellationToken = default)
    {
        var bytes = await http.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!string.Equals(hash, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The downloaded file failed its SHA-256 check.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        await File.WriteAllBytesAsync(destination, bytes, cancellationToken).ConfigureAwait(false);
    }

    private async Task<JsonElement> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        return document.RootElement.Clone();
    }
}