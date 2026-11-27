using MaternityManagementSystem.Services;

namespace MaternityManagementSystem.Tests;

public sealed class AppointmentSmsReminderWorkerTests
{
    [Fact]
    public async Task RunAsync_ContinuesPollingAfterFailedCycle()
    {
        int cycles = 0;
        using var cancellation = new CancellationTokenSource();
        var worker = new AppointmentSmsReminderWorker(_ =>
        {
            if (Interlocked.Increment(ref cycles) == 1)
            {
                throw new InvalidOperationException("simulated");
            }

            cancellation.Cancel();
            return Task.CompletedTask;
        }, TimeSpan.FromMilliseconds(5));

        await worker.RunAsync(cancellation.Token);

        Assert.True(cycles >= 2);
    }

    [Fact]
    public async Task RunAsync_DoesNotOverlapCycles()
    {
        int active = 0;
        int maximumActive = 0;
        int cycles = 0;
        using var cancellation = new CancellationTokenSource();
        var worker = new AppointmentSmsReminderWorker(async token =>
        {
            int current = Interlocked.Increment(ref active);
            Interlocked.Exchange(ref maximumActive, Math.Max(maximumActive, current));
            try
            {
                if (Interlocked.Increment(ref cycles) >= 3)
                {
                    cancellation.Cancel();
                }
                await Task.Delay(2, token);
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        }, TimeSpan.FromMilliseconds(1));

        await worker.RunAsync(cancellation.Token);

        Assert.Equal(1, maximumActive);
        Assert.Equal(0, active);
    }

    [Fact]
    public async Task RunAsync_CancellationStopsPollingAndAwaitsActiveCycle()
    {
        using var cancellation = new CancellationTokenSource();
        var cycleStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        bool cycleCompleted = false;
        var worker = new AppointmentSmsReminderWorker(async token =>
        {
            cycleStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            cycleCompleted = true;
        }, TimeSpan.FromHours(1));

        Task run = worker.RunAsync(cancellation.Token);
        await cycleStarted.Task;
        cancellation.Cancel();
        await run;

        Assert.False(cycleCompleted);
    }

    [Fact]
    public async Task RunAfterReconciliationAsync_WaitsForStartupReconciliation()
    {
        var reconciliation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        int cycles = 0;
        var worker = new AppointmentSmsReminderWorker(_ =>
        {
            Interlocked.Increment(ref cycles);
            cancellation.Cancel();
            return Task.CompletedTask;
        }, TimeSpan.FromMilliseconds(1));

        Task run = worker.RunAfterReconciliationAsync(
            reconciliation.Task,
            cancellation.Token);
        await Task.Delay(20);
        Assert.Equal(0, cycles);

        reconciliation.SetResult();
        await run;
        Assert.Equal(1, cycles);
    }

    [Fact]
    public async Task StopAsync_AwaitsGracefulCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = new AppointmentSmsReminderWorker(async token =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        });
        Task run = worker.RunAsync(cancellation.Token);
        await started.Task;

        bool stopped = await Task.Run(() => AppointmentSmsReminderWorker.Stop(
            cancellation,
            run,
            TimeSpan.FromSeconds(1)));

        Assert.True(stopped);
        Assert.True(run.IsCompleted);
    }

    [Fact]
    public async Task StopAsync_ReturnsAtTimeoutWhenCycleIgnoresCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var finish = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        bool stopped = await Task.Run(() => AppointmentSmsReminderWorker.Stop(
            cancellation,
            finish.Task,
            TimeSpan.FromMilliseconds(20)));

        Assert.False(stopped);
        finish.SetResult();
    }
}
