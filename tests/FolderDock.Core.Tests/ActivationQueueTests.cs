using System.Collections.Concurrent;
using FolderDock.Core;
using Xunit;

namespace FolderDock.Core.Tests;

public sealed class ActivationQueueTests
{
    [Fact]
    public void ClicksDuringColdStartupWaitForInitialLaunchAndKeepTheirOrder()
    {
        var activations = new ActivationQueue();
        var scheduled = new Queue<string>();

        activations.Enqueue("--folder first");
        activations.Enqueue("--folder second");
        Assert.Empty(scheduled);

        activations.Start(scheduled.Enqueue);
        activations.Enqueue("--folder third");

        Assert.Equal(new[] { "--folder first", "--folder second", "--folder third" }, scheduled);
    }

    [Fact]
    public async Task ClickArrivingWhileBufferedClicksAreScheduledCannotOvertakeThem()
    {
        var activations = new ActivationQueue();
        var scheduled = new ConcurrentQueue<string>();
        using var dispatchEntered = new ManualResetEventSlim();
        using var releaseDispatch = new ManualResetEventSlim();
        using var enqueueStarted = new ManualResetEventSlim();
        activations.Enqueue("first");
        activations.Enqueue("second");

        var start = Task.Run(() => activations.Start(arguments =>
        {
            if (arguments == "first")
            {
                dispatchEntered.Set();
                Assert.True(releaseDispatch.Wait(TimeSpan.FromSeconds(10)));
            }
            scheduled.Enqueue(arguments);
        }));

        Assert.True(dispatchEntered.Wait(TimeSpan.FromSeconds(10)));
        var arrival = Task.Run(() =>
        {
            enqueueStarted.Set();
            activations.Enqueue("third");
        });
        try
        {
            Assert.True(enqueueStarted.Wait(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            releaseDispatch.Set();
        }
        await Task.WhenAll(start, arrival);

        Assert.Equal(new[] { "first", "second", "third" }, scheduled);
    }

    [Fact]
    public async Task ConcurrentRedirectsAreDeliveredExactlyOnceAcrossStartup()
    {
        var activations = new ActivationQueue();
        var scheduled = new ConcurrentQueue<string>();
        var arrivals = Enumerable.Range(0, 100)
            .Select(index => Task.Run(() => activations.Enqueue(index.ToString())))
            .ToArray();

        activations.Start(scheduled.Enqueue);
        await Task.WhenAll(arrivals);

        Assert.Equal(100, scheduled.Count);
        Assert.Equal(100, scheduled.Distinct().Count());
    }
}
