using Perenos.Core.Transfer;

namespace Perenos.Core.Tests;

public class DownloadJournalTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"perenos-journal-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    [Fact]
    public void PendingListsInterruptedAndDropsCompleted()
    {
        Directory.CreateDirectory(_dir);
        var interrupted = Path.Combine(_dir, "a.ipa");
        var completed = Path.Combine(_dir, "b.ipa");
        File.WriteAllText(completed, "x");

        DownloadJournal.Record(_dir, new PendingDownload(1, "app.a", "A", "1.0", null, interrupted));
        DownloadJournal.Record(_dir, new PendingDownload(2, "app.b", "B", "2.0", null, completed));

        var pending = DownloadJournal.Pending(_dir);
        Assert.Single(pending);
        Assert.Equal("app.a", pending[0].BundleId);

        DownloadJournal.Complete(_dir, interrupted);
        Assert.Empty(DownloadJournal.Pending(_dir));
    }

    [Fact]
    public void RecordReplacesSameTarget()
    {
        Directory.CreateDirectory(_dir);
        var target = Path.Combine(_dir, "a.ipa");
        DownloadJournal.Record(_dir, new PendingDownload(1, "app.a", "A", "1.0", null, target));
        DownloadJournal.Record(_dir, new PendingDownload(1, "app.a", "A", "1.1", "999", target));

        var pending = DownloadJournal.Pending(_dir);
        Assert.Single(pending);
        Assert.Equal("1.1", pending[0].Version);
        Assert.Equal("999", pending[0].ExternalVersionId);
    }

    [Fact]
    public void MissingOrCorruptJournalIsEmpty()
    {
        Assert.Empty(DownloadJournal.Pending(_dir));
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, ".perenos-downloads.json"), "{not json");
        Assert.Empty(DownloadJournal.Pending(_dir));
    }
}