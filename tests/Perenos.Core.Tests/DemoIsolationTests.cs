namespace Perenos.Core.Tests;

[Collection("Environment")]
public sealed class DemoIsolationTests
{
    [Fact]
    public void DemoNeverUsesRealApplicationData()
    {
        var previous = Environment.GetEnvironmentVariable("PERENOS_DEMO");
        var directory = TestPaths.TempDirectory();
        try
        {
            Environment.SetEnvironmentVariable("PERENOS_DEMO", "1");
            var paths = AppPaths.Detect(directory);
            Assert.True(paths.IsPortable);
            Assert.StartsWith(Path.GetTempPath(), paths.DataDirectory);
            Assert.Contains($"PerenosDemo-{Environment.ProcessId}", paths.DataDirectory);
            Assert.NotEqual(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppPaths.ProductFolderName), paths.DataDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PERENOS_DEMO", previous);
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
