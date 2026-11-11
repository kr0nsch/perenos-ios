using System.Runtime.InteropServices;

namespace Perenos.Core;

/// <summary>
/// Resolves platform-specific application data, with a portable Data folder override.
/// </summary>
public sealed class AppPaths
{
    public const string PortableMarkerFileName = "portable.txt";
    public const string ProductFolderName = "Perenos";
    public const string LegacyProductFolderName = "AppBridge";

    private readonly string? _legacyDataDirectory;
    private readonly string? _legacyDefaultDownloadDirectory;
    private readonly List<string> _migrationWarnings = [];

    internal AppPaths(
        string appDirectory,
        bool isPortable,
        string dataDirectory,
        string defaultDownloadDirectory,
        string? legacyDataDirectory = null,
        string? legacyDefaultDownloadDirectory = null)
    {
        AppDirectory = appDirectory;
        IsPortable = isPortable;
        DataDirectory = dataDirectory;
        DefaultDownloadDirectory = defaultDownloadDirectory;
        _legacyDataDirectory = legacyDataDirectory;
        _legacyDefaultDownloadDirectory = legacyDefaultDownloadDirectory;
    }

    public string AppDirectory { get; }

    public bool IsPortable { get; }

    public string DataDirectory { get; }

    public string DefaultDownloadDirectory { get; }

    public string LogsDirectory => Path.Combine(DataDirectory, "Logs");

    public string IconsDirectory => Path.Combine(DataDirectory, "Icons");

    public string IpatoolHome => Path.Combine(DataDirectory, "ipatool");

    public string PairRecordsDirectory => Path.Combine(DataDirectory, "PairRecords");

    public string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public string IpatoolExecutable => Path.Combine(AppDirectory, "tools", OperatingSystem.IsWindows() ? "ipatool.exe" : "ipatool");

    public IReadOnlyList<string> MigrationWarnings => _migrationWarnings;

    public static AppPaths Detect(string? appDirectory = null)
    {
        appDirectory ??= AppContext.BaseDirectory;
        appDirectory = Path.GetFullPath(appDirectory);

        if (Environment.GetEnvironmentVariable("PERENOS_DEMO") == "1")
        {
            var demoData = Path.Combine(Path.GetTempPath(), $"PerenosDemo-{Environment.ProcessId}");
            return new AppPaths(appDirectory, true, demoData, Path.Combine(demoData, "Downloads"));
        }

        if (File.Exists(Path.Combine(appDirectory, PortableMarkerFileName)))
        {
            var data = Path.Combine(appDirectory, "Data");
            return new AppPaths(appDirectory, true, data, Path.Combine(data, "Downloads"));
        }

        var localAppData = OperatingSystem.IsMacOS()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support")
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var downloads = GetDownloadsFolder();
        return new AppPaths(
            appDirectory,
            false,
            Path.Combine(localAppData, ProductFolderName),
            Path.Combine(downloads, ProductFolderName),
            Path.Combine(localAppData, LegacyProductFolderName),
            Path.Combine(downloads, LegacyProductFolderName));
    }

    public void EnsureCreated()
    {
        Migrate(_legacyDataDirectory, DataDirectory);
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(IpatoolHome);
        Directory.CreateDirectory(PairRecordsDirectory);
    }

    public void MigrateLegacyDownloadDirectory(SettingsStore settings)
    {
        if (_legacyDefaultDownloadDirectory is null)
            return;

        var configured = settings.Current.DownloadDirectory;
        if (!string.IsNullOrWhiteSpace(configured)
            && !SamePath(configured, _legacyDefaultDownloadDirectory)
            && !SamePath(configured, DefaultDownloadDirectory))
            return;

        Migrate(_legacyDefaultDownloadDirectory, DefaultDownloadDirectory);
        settings.Current.DownloadDirectory = DefaultDownloadDirectory;
        settings.Save();
    }

    private void Migrate(string? legacyDirectory, string currentDirectory)
    {
        if (legacyDirectory is null)
            return;

        try
        {
            MigrateDirectory(legacyDirectory, currentDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _migrationWarnings.Add($"Previous data was left in place because it could not be migrated: {ex.Message}");
        }
    }

    internal static void MigrateDirectory(string legacyDirectory, string currentDirectory)
    {
        legacyDirectory = Path.GetFullPath(legacyDirectory);
        currentDirectory = Path.GetFullPath(currentDirectory);
        if (!Directory.Exists(legacyDirectory) || SamePath(legacyDirectory, currentDirectory))
            return;

        if (!Directory.Exists(currentDirectory))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(currentDirectory)!);
            Directory.Move(legacyDirectory, currentDirectory);
            return;
        }

        foreach (var source in EnumerateRegularFiles(legacyDirectory))
        {
            var destination = Path.Combine(currentDirectory, Path.GetRelativePath(legacyDirectory, source.FullName));
            if (File.Exists(destination))
                continue;
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            source.CopyTo(destination);
        }
    }

    private static IEnumerable<FileInfo> EnumerateRegularFiles(string root)
    {
        var pending = new Stack<DirectoryInfo>([new DirectoryInfo(root)]);
        while (pending.Count > 0)
        {
            foreach (var entry in pending.Pop().EnumerateFileSystemInfos())
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                    continue;
                if (entry is DirectoryInfo directory)
                    pending.Push(directory);
                else if (entry is FileInfo file)
                    yield return file;
            }
        }
    }

    private static bool SamePath(string first, string second) =>
        string.Equals(Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static string GetDownloadsFolder()
    {
        var downloads = new Guid("374DE290-123F-4565-9164-39C4925E467B");
        if (OperatingSystem.IsWindows() && SHGetKnownFolderPath(downloads, 0, IntPtr.Zero, out var pathPtr) == 0)
        {
            try
            {
                var path = Marshal.PtrToStringUni(pathPtr);
                if (!string.IsNullOrEmpty(path))
                    return path;
            }
            finally
            {
                Marshal.FreeCoTaskMem(pathPtr);
            }
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint flags, IntPtr token, out IntPtr path);
}
