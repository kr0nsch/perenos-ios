using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AppBridge.Core.Ipatool;

/// <summary>Parses ipatool's "--format json" output: one zerolog JSON object per line.</summary>
internal sealed partial class IpatoolOutput
{
    private readonly List<JsonElement> _events = [];

    public IReadOnlyList<JsonElement> Events => _events;

    public bool Succeeded => _events.Any(e => GetBool(e, "success") == true);

    public string? Error => _events.Select(e => GetString(e, "error")).LastOrDefault(e => !string.IsNullOrEmpty(e));

    public IEnumerable<string> Messages => _events.Select(e => GetString(e, "message")).OfType<string>();

    public JsonElement? SuccessEvent => _events.LastOrDefault(e => GetBool(e, "success") == true) is { ValueKind: JsonValueKind.Object } found ? found : null;

    /// <summary>Adds one output segment; returns false when the segment is not a JSON event.</summary>
    public bool TryAdd(string segment)
    {
        var text = segment.Trim();
        if (text.Length < 2 || text[0] != '{')
            return false;

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;
            _events.Add(doc.RootElement.Clone());
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string? GetString(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public static bool? GetBool(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;

    internal static string? GetLatestVersionId(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("externalVersionIdentifiers", out var versions) || versions.ValueKind != JsonValueKind.Array)
            return null;

        string? latest = null;
        foreach (var version in versions.EnumerateArray())
        {
            if (version.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(version.GetString()))
                latest = version.GetString();
            else if (version.ValueKind == JsonValueKind.Number)
                latest = version.ToString();
        }

        return latest;
    }

    internal static OwnedAppsPage? ReadOwnedAppsPage(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object
            || !e.TryGetProperty("apps", out var items) || items.ValueKind != JsonValueKind.Array
            || !e.TryGetProperty("page", out var pageElement) || !pageElement.TryGetInt32(out var page)
            || !e.TryGetProperty("totalCount", out var totalElement) || !totalElement.TryGetInt32(out var total))
            return null;

        var apps = new List<OwnedApp>();
        foreach (var item in items.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idElement))
                throw new InvalidDataException("An owned app has no App Store ID.");
            long id;
            if (idElement.ValueKind == JsonValueKind.Number)
                idElement.TryGetInt64(out id);
            else
                long.TryParse(idElement.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
            var bundleId = GetString(item, "bundleID") ?? "";
            if (id <= 0 || string.IsNullOrWhiteSpace(bundleId))
                throw new InvalidDataException("An owned app has an invalid App Store ID or bundle identifier.");

            DateTimeOffset? purchased = null;
            if (DateTimeOffset.TryParse(GetString(item, "purchaseDate"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date))
                purchased = date;
            apps.Add(new OwnedApp(id, bundleId, GetString(item, "name") ?? bundleId, GetString(item, "version") ?? "", purchased));
        }

        return new OwnedAppsPage(page, total, apps);
    }

    public static IpatoolErrorKind Classify(string? error)
    {
        if (string.IsNullOrEmpty(error))
            return IpatoolErrorKind.Unknown;

        var e = error.ToLowerInvariant();
        if (e.Contains("purchase history") && (e.Contains("http 401") || e.Contains("http 403") || e.Contains("daap status 401") || e.Contains("daap status 403") || e.Contains("framework rejected response")))
            return IpatoolErrorKind.HistoryUnavailable;
        if (e.Contains("failed to get account") || e.Contains("keychain passphrase") || e.Contains("integrity check failed"))
            return IpatoolErrorKind.NotSignedIn;
        if (e.Contains("password token is expired") || e.Contains("auth code is required"))
            return IpatoolErrorKind.SessionExpired;
        if (e.Contains("license is required"))
            return IpatoolErrorKind.LicenseRequired;
        if (e.Contains("purchasing paid apps is not supported"))
            return IpatoolErrorKind.PaidAppNotSupported;
        if (e.Contains("subscription required"))
            return IpatoolErrorKind.SubscriptionRequired;
        if (e.Contains("temporarily unavailable"))
            return IpatoolErrorKind.TemporarilyUnavailable;
        if (e.Contains("app not found") || e.Contains("item not found") || e.Contains("not available in your country"))
            return IpatoolErrorKind.AppNotFound;
        if (e.Contains("request failed") || e.Contains("no such host") || e.Contains("connection") || e.Contains("timeout"))
            return IpatoolErrorKind.Network;
        return IpatoolErrorKind.Unknown;
    }

    /// <summary>
    /// Reads the progress bar ipatool draws in interactive mode, e.g.
    /// "downloading  45% |█████     | (123/274 MB, 12 MB/s)".
    /// </summary>
    public static double? ParseProgressFraction(string segment)
    {
        var match = PercentRegex().Match(segment);
        if (!match.Success)
            return null;
        var percent = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        return Math.Clamp(percent / 100.0, 0, 1);
    }

    [GeneratedRegex(@"(?<![\d.])(\d{1,3})%")]
    private static partial Regex PercentRegex();
}
