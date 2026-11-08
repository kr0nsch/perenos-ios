using AppBridge.Core.Catalog;

namespace AppBridge.Core.Tests;

public sealed class RussianServiceCatalogTests
{
    [Fact]
    public void EmbeddedCatalogHasUniqueKnownRussianServices()
    {
        var apps = RussianServiceCatalog.LoadDefault();

        Assert.Equal(243, apps.Count);
        Assert.Equal(apps.Count, apps.Select(app => app.AppId).Distinct().Count());
        Assert.All(apps, app =>
        {
            Assert.True(app.AppId > 0);
            Assert.False(string.IsNullOrWhiteSpace(app.Family));
            Assert.False(string.IsNullOrWhiteSpace(app.Category));
        });
        var vkMusic = Assert.Single(apps, app => app.AppId == 1054372220);
        Assert.Equal("ВК Музыка", vkMusic.Family);
        Assert.Equal("VK Музыка", vkMusic.Alias);
        Assert.Equal("com.music.vk", vkMusic.BundleId);
        Assert.Equal("887160241", vkMusic.ExternalVersionId);
    }
}
