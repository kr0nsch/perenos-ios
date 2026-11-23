using Netimobiledevice.Plist;
using Perenos.Core.Devices;

namespace Perenos.Core.Tests;

public class OffloadedTests
{
    [Fact]
    public void AppWithoutBinaryPathIsOffloaded()
    {
        var node = new DictionaryNode { { "CFBundleIdentifier", new StringNode("com.example.offloaded") } };

        var app = DeviceService.ParseApp(node);

        Assert.NotNull(app);
        Assert.True(app!.IsOffloaded);
    }

    [Fact]
    public void AppWithBinaryPathIsInstalled()
    {
        var node = new DictionaryNode
        {
            { "CFBundleIdentifier", new StringNode("com.example.installed") },
            { "Path", new StringNode("/private/var/containers/Bundle/Application/X/Example.app") },
        };

        var app = DeviceService.ParseApp(node);

        Assert.NotNull(app);
        Assert.False(app!.IsOffloaded);
    }
}