using Microsoft.Extensions.Hosting;

namespace SharedKernel;

public abstract class StatusBackgroundService: BackgroundService, IServiceStatus
{
    private object _statusLock = new object();

    public ServiceStatus Status { get; private set; }
    public event EventHandler? StatusChanged;
    
    protected void SetStatus(ServiceStatus status)
    {
        lock (_statusLock) { Status = status; }
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }
    
    public sealed override async Task StartAsync(CancellationToken cancellationToken)
    {
        SetStatus(ServiceStatus.Starting);
        try
        {
            await base.StartAsync(cancellationToken).ConfigureAwait(false);
            // NOTE: we mark Running inside ExecuteAsync when the worker actually starts.
        }
        catch
        {
            SetStatus(ServiceStatus.Faulted);
            throw;
        }
    }

    public sealed override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Signal we're stopping before we ask BackgroundService to cancel the worker
        if (Status != ServiceStatus.Faulted)
            SetStatus(ServiceStatus.Stopping);

        try
        {
            await base.StopAsync(cancellationToken).ConfigureAwait(false);
            if (Status != ServiceStatus.Faulted)
                SetStatus(ServiceStatus.Stopped);
        }
        catch
        {
            SetStatus(ServiceStatus.Faulted);
            throw;
        }
    }

    // Wrap the worker to flip Running/Faulted safely.
    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Mark Running when the worker actually begins.
        SetStatus(ServiceStatus.Running);

        try
        {
            await ExecuteCoreAsync(stoppingToken).ConfigureAwait(false);
            // Normal completion: StopAsync will set Stopped.
            if (stoppingToken.IsCancellationRequested && Status != ServiceStatus.Faulted)
            {
                // Let StopAsync finalize the Stopped state.
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during StopAsync(); StopAsync will set Stopped.
        }
        catch
        {
            SetStatus(ServiceStatus.Faulted);
            throw;
        }
    }

    /// <summary>
    /// Implement your service logic here. Respect <paramref name="stoppingToken"/>.
    /// Do NOT set status here—base class handles it.
    /// </summary>
    protected abstract Task ExecuteCoreAsync(CancellationToken stoppingToken);
}