using System.Net;
using System.Security.Cryptography;
using System.Text;
using Perenos.Core;

namespace Perenos.Core.Tests;

public class UpdateCheckerTests
{
    [Theory]
    [InlineData("1.9.0", "1.10.0", true)]
    [InlineData("1.9.0", "1.9.0", false)]
    [InlineData("1.9.0", "1.8.0", false)]
    [InlineData("1.9", "1.9.1", true)]
    [InlineData("1.9.0", "2.0", true)]
    [InlineData("bad", "1.0.0", false)]
    public void ComparesVersions(string current, string latest, bool expected) =>
        Assert.Equal(expected, UpdateChecker.IsNewer(current, latest));

    [Fact]
    public async Task DetectsNewerAppRelease()
    {
        var checker = new UpdateChecker(new HttpClient(new FakeHandler("""{"tag_name":"v1.10.0","html_url":"https://github.com/kr0nsch/perenos-ios/releases/tag/v1.10.0"}""")));

        var update = await checker.CheckAppAsync("1.9.0");

        Assert.NotNull(update);
        Assert.Equal("1.10.0", update!.Latest);
    }

    [Fact]
    public async Task NoUpdateWhenCurrentIsLatest()
    {
        var checker = new UpdateChecker(new HttpClient(new FakeHandler("""{"tag_name":"v1.9.0"}""")));

        Assert.Null(await checker.CheckAppAsync("1.9.0"));
    }

    [Fact]
    public async Task DetectsNewerIpatoolCommit()
    {
        var checker = new UpdateChecker(new HttpClient(new FakeHandler("""{"sha":"abcdef1234567890"}""")));

        var update = await checker.CheckIpatoolAsync("3b02b4c");

        Assert.NotNull(update);
        Assert.Equal("abcdef1", update!.Latest);
    }

    [Fact]
    public async Task NoIpatoolUpdateWhenCommitMatches()
    {
        var checker = new UpdateChecker(new HttpClient(new FakeHandler("""{"sha":"3b02b4c55576562f"}""")));

        Assert.Null(await checker.CheckIpatoolAsync("3b02b4c"));
    }

    [Fact]
    public async Task DownloadVerifiedRejectsBadChecksum()
    {
        var checker = new UpdateChecker(new HttpClient(new FakeHandler("payload", "text/plain")));
        var path = Path.Combine(Path.GetTempPath(), $"perenos-update-{Guid.NewGuid():N}.bin");

        await Assert.ThrowsAsync<InvalidDataException>(() => checker.DownloadVerifiedAsync("https://example/x", new string('0', 64), path));

        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task DownloadVerifiedWritesMatchingFile()
    {
        var bytes = Encoding.UTF8.GetBytes("payload");
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var checker = new UpdateChecker(new HttpClient(new FakeHandler("payload", "text/plain")));
        var path = Path.Combine(Path.GetTempPath(), $"perenos-update-{Guid.NewGuid():N}.bin");
        try
        {
            await checker.DownloadVerifiedAsync("https://example/x", sha, path);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class FakeHandler(string body, string contentType = "application/json") : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, contentType),
            });
    }
}