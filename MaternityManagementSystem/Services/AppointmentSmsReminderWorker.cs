using System.Diagnostics;

namespace MaternityManagementSystem.Services;

/// <summary>Runs reminder dispatch passes while the WPF application is alive.</summary>
public sealed class AppointmentSmsReminderWorker
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(1);

    private readonly Func<CancellationToken, Task> _dispatchCycle;
    private readonly TimeSpan _interval;

    public AppointmentSmsReminderWorker(
        AppointmentSmsReminderDispatchService dispatchService,
        TimeSpan? interval = null)
        : this(
            cancellationToken => dispatchService.DispatchDueRemindersAsync(
                cancellationToken: cancellationToken),
            interval)
    {
        ArgumentNullException.ThrowIfNull(dispatchService);
    }

    public AppointmentSmsReminderWorker(
        Func<CancellationToken, Task> dispatchCycle,
        TimeSpan? interval = null)
    {
        ArgumentNullException.ThrowIfNull(dispatchCycle);
        _dispatchCycle = dispatchCycle;
        _interval = interval ?? DefaultInterval;
        if (_interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval));
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _dispatchCycle(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                Debug.WriteLine("Appointment reminder dispatch cycle failed; polling will continue.");
            }

            try
            {
                await Task.Delay(_interval, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public async Task RunAfterReconciliationAsync(
        Task reconciliation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reconciliation);
        await reconciliation;
        await RunAsync(cancellationToken);
    }

    public static bool Stop(
        CancellationTokenSource cancellation,
        Task workerTask,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(cancellation);
        ArgumentNullException.ThrowIfNull(workerTask);
        if (timeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        cancellation.Cancel();
        try
        {
            return workerTask.Wait(timeout);
        }
        catch (AggregateException)
        {
            if (workerTask.IsFaulted)
            {
                Debug.WriteLine("Appointment reminder worker stopped after an unexpected failure.");
            }
            return true;
        }
    }
}
