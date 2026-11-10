using System.Globalization;
using System.Text.Json;

namespace Perenos.Core.Storefront;

/// <summary>Reads the public Apple catalog without sending account details or credentials.</summary>
public sealed class StorefrontCatalog : IStorefrontCatalog
{
    private const int BatchSize = 100;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);
    private static readonly HttpClient SharedHttp = new();
    private readonly HttpClient _http;

    public StorefrontCatalog() => _http = SharedHttp;

    internal StorefrontCatalog(HttpClient http) => _http = http;

    public async Task<IReadOnlySet<long>> GetAvailableAppIdsAsync(
        IEnumerable<long> appIds, string storefront, CancellationToken cancellationToken = default)
    {
        var requested = appIds.Where(id => id > 0).Distinct().ToArray();
        var available = new HashSet<long>();
        foreach (var batch in requested.Chunk(BatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(RequestTimeout);
            var json = await _http.GetStringAsync(BuildLookupUri(batch, storefront), timeout.Token).ConfigureAwait(false);
            available.UnionWith(ParseAvailableIds(json, batch.ToHashSet()));
        }

        AppLog.Info($"Storefront {storefront}: {available.Count} of {requested.Length} app(s) are listed.");
        return available;
    }

    internal static string BuildLookupUri(IEnumerable<long> appIds, string storefront) =>
        "https://itunes.apple.com/lookup?id="
        + string.Join(',', appIds.Select(id => id.ToString(CultureInfo.InvariantCulture)))
        + "&country=" + Uri.EscapeDataString(storefront)
        + "&entity=software&limit=200";

    internal static IReadOnlySet<long> ParseAvailableIds(string json, IReadOnlySet<long> requested)
    {
        using var document = JsonDocument.Parse(json);
        var available = new HashSet<long>();
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("results", out var results)
            || results.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Apple returned an invalid catalog response.");

        foreach (var item in results.EnumerateArray())
        {
            if (!item.TryGetProperty("trackId", out var id))
                continue;
            long parsed = 0;
            if (id.ValueKind == JsonValueKind.Number)
                id.TryGetInt64(out parsed);
            else if (id.ValueKind == JsonValueKind.String)
                long.TryParse(id.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out parsed);
            if (requested.Contains(parsed))
                available.Add(parsed);
        }

        return available;
    }
}
