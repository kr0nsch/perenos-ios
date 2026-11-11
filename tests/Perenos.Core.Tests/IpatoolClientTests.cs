using Perenos.Core.Ipatool;

namespace Perenos.Core.Tests;

public class IpatoolClientTests
{
    [WindowsFact]
    public void PrivateHomeSplitsDriveAndPath()
    {
        var environment = new Dictionary<string, string?> { ["XDG_STATE_HOME"] = @"C:\state", ["XDG_DATA_HOME"] = @"C:\data" };

        IpatoolClient.ConfigureEnvironment(environment, @"C:\Users\anna\AppData\Local\Perenos\ipatool");

        Assert.Equal("C:", environment["HOMEDRIVE"]);
        Assert.Equal(@"\Users\anna\AppData\Local\Perenos\ipatool", environment["HOMEPATH"]);
        Assert.False(environment.ContainsKey("XDG_STATE_HOME"));
        Assert.False(environment.ContainsKey("XDG_DATA_HOME"));
    }

    [WindowsFact]
    public void PrivateHomeSupportsUncPaths()
    {
        var environment = new Dictionary<string, string?>();

        IpatoolClient.ConfigureEnvironment(environment, @"\\server\share\Perenos\Data\ipatool");

        Assert.Equal("", environment["HOMEDRIVE"]);
        Assert.Equal(@"\\server\share\Perenos\Data\ipatool", environment["HOMEPATH"]);
    }

    [Fact]
    public async Task MissingExecutableIsReported()
    {
        var client = new IpatoolClient(@"C:\does-not-exist\ipatool.exe", Path.Combine(Path.GetTempPath(), "fr-missing"));

        Assert.False(client.IsAvailable);
        var ex = await Assert.ThrowsAsync<IpatoolException>(() => client.GetAccountAsync());
        Assert.Equal(IpatoolErrorKind.ToolMissing, ex.Kind);
    }

    /// <summary>
    /// Runs the real bundled ipatool against an empty private home: it must report "not signed
    /// in" and must not touch any other ipatool session on this machine.
    /// </summary>
    [WindowsFact]
    public async Task RealIpatoolReportsSignedOutForFreshHome()
    {
        var exe = TestPaths.Ipatool();
        if (exe is null)
            return; // Run build\Get-Ipatool.ps1 first to enable this test.

        var home = Path.Combine(Path.GetTempPath(), "fr-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var client = new IpatoolClient(exe, home);
            var account = await client.GetAccountAsync();

            Assert.Null(account);
            Assert.True(File.Exists(Path.Combine(home, "keychain.key")), "the DPAPI-protected passphrase should be created");
            Assert.True(Directory.Exists(Path.Combine(home, ".ipatool")), "ipatool should keep its state inside the private home");
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    /// <summary>
    /// A transient keychain failure (for example keychain.key locked by another process) must
    /// not poison the client: the next command resolves the passphrase again instead of
    /// rethrowing the cached exception.
    /// </summary>
    [Fact]
    public async Task PassphraseFailureIsRetriedOnTheNextCommand()
    {
        var home = Path.Combine(Path.GetTempPath(), "fr-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(home);
        var exe = Path.Combine(home, "ipatool.exe");
        await File.WriteAllBytesAsync(exe, []); // IsAvailable only checks that the file exists.

        var attempts = 0;
        var client = new IpatoolClient(exe, home, () =>
            ++attempts == 1 ? throw new IOException("keychain.key is temporarily locked") : "passphrase");
        try
        {
            await Assert.ThrowsAsync<IOException>(() => client.GetAccountAsync());
            Assert.Equal(1, attempts);

            // The passphrase is now resolved, so the command gets past it and fails only
            // because the dummy executable cannot be started.
            var ex = await Assert.ThrowsAnyAsync<Exception>(() => client.GetAccountAsync());
            Assert.Equal(2, attempts);
            Assert.IsNotType<IOException>(ex);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    /// <summary>A resolved passphrase is reused; the keychain is not read again per command.</summary>
    [Fact]
    public async Task PassphraseIsResolvedOnceAndReused()
    {
        var home = Path.Combine(Path.GetTempPath(), "fr-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(home);
        var exe = Path.Combine(home, "ipatool.exe");
        await File.WriteAllBytesAsync(exe, []);

        var attempts = 0;
        var client = new IpatoolClient(exe, home, () =>
        {
            attempts++;
            return "passphrase";
        });
        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => client.GetAccountAsync());
            await Assert.ThrowsAnyAsync<Exception>(() => client.GetAccountAsync());
            Assert.Equal(1, attempts);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    /// <summary>Concurrent commands are serialized, so the passphrase is resolved exactly once.</summary>
    [Fact]
    public async Task ParallelCommandsShareOnePassphraseLookup()
    {
        var home = Path.Combine(Path.GetTempPath(), "fr-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(home);
        var exe = Path.Combine(home, "ipatool.exe");
        await File.WriteAllBytesAsync(exe, []);

        var attempts = 0;
        var client = new IpatoolClient(exe, home, () =>
        {
            Interlocked.Increment(ref attempts);
            Thread.Sleep(50); // Widen the race window: an unguarded lookup would run twice.
            return "passphrase";
        });
        try
        {
            var commands = Enumerable.Range(0, 4)
                .Select(_ => client.GetAccountAsync())
                .ToArray();
            foreach (var command in commands)
                await Assert.ThrowsAnyAsync<Exception>(() => command);
            Assert.Equal(1, attempts);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }
}
