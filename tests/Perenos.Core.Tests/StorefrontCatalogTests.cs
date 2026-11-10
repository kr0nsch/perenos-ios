using System.Net;
using System.Text;
using System.Text.Json;
using Perenos.Core.Storefront;

namespace Perenos.Core.Tests;

public sealed class StorefrontCatalogTests
{
    [Fact]
    public void LookupUsesOnlyNumericIdsAndStorefront()
    {
        var uri = StorefrontCatalog.BuildLookupUri([544007664, 324684580], "ru");

        Assert.Equal(
            "https://itunes.apple.com/lookup?id=544007664,324684580&country=ru&entity=software&limit=200",
            uri);
    }

    [Fact]
    public void ParsesNumericAndStringIdsAndIgnoresUnknownResults()
    {
        const string json = """
            {"resultCount":3,"results":[{"trackId":10},{"trackId":"20"},{"trackId":999}]}
            """;

        var available = StorefrontCatalog.ParseAvailableIds(json, new HashSet<long> { 10, 20, 30 });

        Assert.Equal([10L, 20L], available.Order());
    }

    [Fact]
    public void ResponseWithoutResultsIsInvalid()
    {
        Assert.Throws<InvalidDataException>(() =>
            StorefrontCatalog.ParseAvailableIds("""{"resultCount":0}""", new HashSet<long> { 10 }));
    }

    [Fact]
    public async Task SplitsLargeLookupIntoSequentialBatches()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        var catalog = new StorefrontCatalog(http);
        var ids = Enumerable.Range(1, 101).Select(i => (long)i).ToArray();

        var available = await catalog.GetAvailableAppIdsAsync(ids, "ru");

        Assert.Equal(ids, available.Order());
        Assert.Equal(2, handler.Requests.Count);
        Assert.EndsWith("id=101&country=ru&entity=software&limit=200", handler.Requests[1]);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!.ToString();
            Requests.Add(uri);
            var idList = uri.Split("?id=")[1].Split('&')[0];
            var ids = idList.Split(',').Select(long.Parse).ToArray();
            var json = JsonSerializer.Serialize(new
            {
                resultCount = ids.Length,
                results = ids.Select(id => new { trackId = id }),
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
