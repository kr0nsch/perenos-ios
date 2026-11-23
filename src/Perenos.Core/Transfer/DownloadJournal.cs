using System.Text.Json;
using System.Text.Json.Serialization;

namespace Perenos.Core.Transfer;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<PendingDownload>))]
internal sealed partial class DownloadJournalJsonContext : JsonSerializerContext;

/// <summary>An interrupted download that can be resumed after the app restarts.</summary>
public sealed record PendingDownload(
    long AppId,
    string BundleId,
    string Name,
    string Version,
    string? ExternalVersionId,
    string TargetPath);

/// <summary>
/// Persists in-flight downloads next to the download folder so a restart can resume them
/// (ipatool continues a partial <c>.tmp</c> when re-run with the same target path).
/// </summary>
public static class DownloadJournal
{
    private static string JournalPath(string downloadDirectory) =>
        Path.Combine(downloadDirectory, ".perenos-downloads.json");

    public static List<PendingDownload> Load(string downloadDirectory)
    {
        try
        {
            var path = JournalPath(downloadDirectory);
            if (!File.Exists(path))
                return [];
            return JsonSerializer.Deserialize(File.ReadAllText(path), DownloadJournalJsonContext.Default.ListPendingDownload) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public static void Record(string downloadDirectory, PendingDownload entry)
    {
        var list = Load(downloadDirectory);
        list.RemoveAll(e => string.Equals(e.TargetPath, entry.TargetPath, StringComparison.OrdinalIgnoreCase));
        list.Add(entry);
        Save(downloadDirectory, list);
    }

    public static void Complete(string downloadDirectory, string targetPath)
    {
        var list = Load(downloadDirectory);
        if (list.RemoveAll(e => string.Equals(e.TargetPath, targetPath, StringComparison.OrdinalIgnoreCase)) > 0)
            Save(downloadDirectory, list);
    }

    /// <summary>Journaled downloads whose finished package is still missing.</summary>
    public static List<PendingDownload> Pending(string downloadDirectory) =>
        Load(downloadDirectory).Where(e => !File.Exists(e.TargetPath)).ToList();

    private static void Save(string downloadDirectory, List<PendingDownload> list)
    {
        try
        {
            Directory.CreateDirectory(downloadDirectory);
            File.WriteAllText(JournalPath(downloadDirectory), JsonSerializer.Serialize(list, DownloadJournalJsonContext.Default.ListPendingDownload));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Debug($"Could not persist the download journal: {ex.Message}");
        }
    }
}