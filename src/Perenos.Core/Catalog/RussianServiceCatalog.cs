using System.Reflection;
using System.Text.Json;

namespace Perenos.Core.Catalog;

public sealed record RussianServiceApp(
    long AppId,
    string BundleId,
    string Name,
    string Family,
    string? Alias,
    string Category,
    string? Developer,
    string? ExternalVersionId = null,
    bool ListedInRussia = false);

public static class RussianServiceCatalog
{
    public static IReadOnlyList<RussianServiceApp> LoadDefault()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Perenos.Core.Catalog.russian-app-catalog.json")
            ?? throw new InvalidDataException("The Russian service catalog is missing.");
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.GetProperty("apps").EnumerateArray()
            .Select(item => new RussianServiceApp(
                item.GetProperty("appId").GetInt64(),
                GetString(item, "bundleId") ?? "",
                GetString(item, "name") ?? "",
                GetString(item, "family") ?? "",
                GetString(item, "alias"),
                GetString(item, "category") ?? "other",
                GetString(item, "developer"),
                GetString(item, "externalVersionId"),
                item.TryGetProperty("storefronts", out var storefronts)
                    && storefronts.TryGetProperty("ru", out var ru)
                    && ru.ValueKind == JsonValueKind.True))
            .ToArray();
    }

    private static string? GetString(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
