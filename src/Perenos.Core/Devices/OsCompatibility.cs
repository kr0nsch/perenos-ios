namespace Perenos.Core.Devices;

/// <summary>Compares an app's minimum iOS against the connected device's iOS.</summary>
public static class OsCompatibility
{
    /// <summary>True when the device runs an iOS older than the app requires.</summary>
    public static bool RequiresNewerOs(string? minimumOsVersion, string? deviceOsVersion)
    {
        if (string.IsNullOrWhiteSpace(minimumOsVersion) || string.IsNullOrWhiteSpace(deviceOsVersion))
            return false;
        if (!DeviceService.TryParseVersion(minimumOsVersion, out var required))
            return false;
        if (!DeviceService.TryParseVersion(deviceOsVersion, out var actual))
            return false;
        return actual < required;
    }
}