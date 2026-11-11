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
