namespace Perenos.ViewModel.Tests;

public sealed class SingleInstanceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"perenos-instance-{Guid.NewGuid():N}");

    [Fact]
    public async Task SecondInstanceActivatesFirst()
    {
        using var first = new Perenos.Services.SingleInstance(_folder);
        using var second = new Perenos.Services.SingleInstance(_folder);
        var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        first.Start(() => activated.TrySetResult());

        Assert.True(first.IsFirst);
        Assert.False(second.IsFirst);
        Assert.True(second.ActivateOther());
        await activated.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ActivationFailureDoesNotStopTheListener()
    {
        using var first = new Perenos.Services.SingleInstance(_folder);
        using var second = new Perenos.Services.SingleInstance(_folder);
        var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        first.Start(() =>
        {
            if (++attempts == 1)
                throw new InvalidOperationException("The window could not be activated.");
            activated.TrySetResult();
        });

        Assert.False(second.ActivateOther());
        Assert.True(second.ActivateOther());
        await activated.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UnresponsiveActivationEndpointTimesOut()
    {
        using var second = new Perenos.Services.SingleInstance(_folder);
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            File.WriteAllText(Path.Combine(_folder, "ui.endpoint"), $"{port}\nnot-a-secret\n");

            var activated = await Task.Run(second.ActivateOther).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(activated);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public void LockCanBeAcquiredAfterExit()
    {
        var first = new Perenos.Services.SingleInstance(_folder);
        Assert.True(first.IsFirst);
        first.Dispose();

        using var second = new Perenos.Services.SingleInstance(_folder);
        Assert.True(second.IsFirst);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }
}
