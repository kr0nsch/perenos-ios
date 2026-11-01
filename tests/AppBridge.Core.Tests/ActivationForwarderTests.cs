using System.Diagnostics;
using AppBridge.Services;

namespace AppBridge.Core.Tests;

public class ActivationForwarderTests
{
    [Fact]
    public async Task SuccessfulForwardingSignalsStaWaitAndClosesHandle()
    {
        await OnStaThread(() =>
        {
            var forwarding = new ActivationForwarder(() => Task.CompletedTask);
            var handle = forwarding.CompletionHandle;
            try
            {
                Assert.True(forwarding.Wait(TimeSpan.FromSeconds(2)));
                Assert.Null(forwarding.Error);
            }
            finally
            {
                forwarding.Dispose();
            }
            Assert.True(handle.IsClosed);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedForwardingIsObservedAndSignalsStaWait(bool synchronous)
    {
        var failure = new InvalidOperationException("The target instance exited.");
        await OnStaThread(() =>
        {
            using var forwarding = new ActivationForwarder(() => synchronous ? throw failure : Task.FromException(failure));
            Assert.True(forwarding.Wait(TimeSpan.FromSeconds(2)));
            Assert.Same(failure, forwarding.Error);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StalledForwardingTimesOutClosesHandleAndAcceptsLateCompletion(bool failLate)
    {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new InvalidOperationException("Late forwarding failure.");
        ActivationForwarder? forwarding = null;
        await OnStaThread(() =>
        {
            forwarding = new ActivationForwarder(() => pending.Task);
            var handle = forwarding.CompletionHandle;
            try
            {
                var elapsed = Stopwatch.StartNew();
                Assert.False(forwarding.Wait(TimeSpan.FromMilliseconds(100)));
                Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(2));
            }
            finally
            {
                forwarding.Dispose();
            }
            Assert.True(handle.IsClosed);
        });

        // Completion after Dispose must neither signal a closed/reused native handle nor fault
        // the background task; late redirect failures must still be observed.
        if (failLate)
            pending.SetException(failure);
        else
            pending.SetResult();
        await forwarding!.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Same(failLate ? failure : null, forwarding.Error);
    }

    private static async Task OnStaThread(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
