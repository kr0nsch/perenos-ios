using Perenos.Core.Devices;

namespace Perenos.Core.Tests;

public class OsCompatibilityTests
{
    [Theory]
    [InlineData("16.0", "15.0", true)]
    [InlineData("16.0", "16.0", false)]
    [InlineData("16.0", "17.2", false)]
    [InlineData("15.0", "15.4.1", false)]
    [InlineData("16.0", "", false)]
    [InlineData("", "15.0", false)]
    [InlineData(null, "15.0", false)]
    [InlineData("16.0", null, false)]
    [InlineData("not-a-version", "15.0", false)]
    public void DetectsOlderDeviceOs(string? minimum, string? device, bool expected) =>
        Assert.Equal(expected, OsCompatibility.RequiresNewerOs(minimum, device));
}