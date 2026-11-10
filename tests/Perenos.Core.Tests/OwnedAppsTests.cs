using System.Text.Json;
using Perenos.Core.Ipatool;

namespace Perenos.Core.Tests;

public sealed class OwnedAppsTests
{
    [Fact]
    public void ReadsPurchasePageWithoutRequiringSuccessField()
    {
        using var document = JsonDocument.Parse("""
            {"page":1,"totalCount":1,"apps":[{"id":1054372220,"bundleID":"com.music.vk","name":"VK Музыка","version":"8.30.1","purchaseDate":"2024-05-06T07:08:09Z"}]}
            """);

        var page = IpatoolOutput.ReadOwnedAppsPage(document.RootElement);

        Assert.NotNull(page);
        var app = Assert.Single(page.Apps);
        Assert.Equal(1054372220, app.AppId);
        Assert.Equal("com.music.vk", app.BundleId);
        Assert.Equal("VK Музыка", app.Name);
        Assert.Equal(new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.Zero), app.PurchaseDate);
    }

    [Fact]
    public void RejectsOwnedAppWithoutIdentifier()
    {
        using var document = JsonDocument.Parse("""{"page":1,"totalCount":1,"apps":[{"bundleID":"app.one"}]}""");

        Assert.Throws<InvalidDataException>(() => IpatoolOutput.ReadOwnedAppsPage(document.RootElement));
    }

    [Fact]
    public async Task CollectsEveryPurchasePage()
    {
        var pages = new List<int>();
        var owned = await IpatoolClient.CollectOwnedAppsAsync((page, _) =>
        {
            pages.Add(page);
            IReadOnlyList<OwnedApp> apps = page == 1
                ? Enumerable.Range(1, 100).Select(id => new OwnedApp(id, $"app.{id}", $"App {id}", "1.0", null)).ToList()
                : [new OwnedApp(101, "app.101", "App 101", "1.0", null)];
            return Task.FromResult(new OwnedAppsPage(page, 101, apps));
        });

        Assert.Equal([1, 2], pages);
        Assert.Equal(101, owned.Count);
    }

    [Fact]
    public async Task RejectsEmptyIntermediatePage()
    {
        await Assert.ThrowsAsync<IpatoolException>(() => IpatoolClient.CollectOwnedAppsAsync(
            (page, _) => Task.FromResult(new OwnedAppsPage(page, 101, []))));
    }

    [Fact]
    public async Task RejectsMissingItemsOnLastPage()
    {
        await Assert.ThrowsAsync<IpatoolException>(() => IpatoolClient.CollectOwnedAppsAsync(
            (page, _) => Task.FromResult(new OwnedAppsPage(page, 2, [new OwnedApp(1, "app.one", "One", "1.0", null)]))));
    }
}
