using System.Text.RegularExpressions;
using System.Xml.Linq;
using Perenos.Services;

namespace Perenos.Core.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public void AvaloniaTemplatesUseExistingStrings()
    {
        var resources = Path.Combine(TestPaths.RepositoryRoot, "src", "Perenos.Presentation", "Strings");
        var english = XDocument.Load(Path.Combine(resources, "Resources.resx"))
            .Root!.Elements("data").Select(e => e.Attribute("name")!.Value).ToHashSet();
        var russian = XDocument.Load(Path.Combine(resources, "Resources.ru.resx"))
            .Root!.Elements("data").Select(e => e.Attribute("name")!.Value).ToHashSet();
        var keys = Directory.GetFiles(Path.Combine(TestPaths.RepositoryRoot, "src", "Perenos"), "*.axaml", SearchOption.AllDirectories)
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), @"\{s:Text\s+([^\s}]+)\}").Select(m => m.Groups[1].Value)).ToHashSet();
        Assert.NotEmpty(keys);
        Assert.Empty(keys.Except(english));
        Assert.Empty(keys.Except(russian));
        foreach (var key in keys)
        {
            Assert.NotEqual(key, Loc.Get(key));
        }
    }

    [Fact]
    public void RussianResourcesAreLoadedThroughDotnetResourceManager()
    {
        Loc.ApplyLanguage("ru-RU");
        Assert.Equal("Выберите приложения", Loc.Get("Apps_Title"));
        Assert.Equal("Настройки", Loc.Get("Settings_Button"));
        Loc.ApplyLanguage("en-US");
        Assert.Equal("Choose Apps", Loc.Get("Apps_Title"));
    }
}
