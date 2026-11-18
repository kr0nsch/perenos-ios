using Perenos.Core.Catalog;

namespace Perenos.Core.Tests;

public sealed class RussianServiceCatalogTests
{
    [Fact]
    public void EmbeddedCatalogHasUniqueKnownRussianServices()
    {
        var apps = RussianServiceCatalog.LoadDefault();

        Assert.Equal(256, apps.Count);
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

        var max = Assert.Single(apps, app => app.AppId == 6739530834);
        Assert.Equal("MAX", max.Family);
        Assert.Equal("МАКС", max.Alias);
        Assert.Equal("ru.oneme.app", max.BundleId);

        var domclick = Assert.Single(apps, app => app.AppId == 1143031400);
        Assert.Equal("ДомКлик", domclick.Family);
        Assert.Equal("Дом Клик", domclick.Alias);
        Assert.Equal("ru.domclick.mortgage", domclick.BundleId);

        var lukoil = Assert.Single(apps, app => app.AppId == 959900548);
        Assert.Equal("АЗС Лукойл", lukoil.Family);
        Assert.Equal("Lukoil", lukoil.Alias);
        Assert.Equal("eae.potemka.LUKOIL", lukoil.BundleId);

        var vtbAllSimple = Assert.Single(apps, app => app.AppId == 1673509494);
        Assert.Equal("ВТБ Онлайн", vtbAllSimple.Family);
        Assert.Equal("Всё просто", vtbAllSimple.Alias);
        Assert.Equal("com.bscmsc.vsepoluchitsy", vtbAllSimple.BundleId);

        Assert.Contains(apps, app => app.AppId == 1621707689 && app.Alias == "Прайм баланс Юником");
        Assert.Contains(apps, app => app.AppId == 6755773033 && app.Alias == "Алтай");
        Assert.Contains(apps, app => app.AppId == 6760753007 && app.Family == "ДомКлик");
    }
}
