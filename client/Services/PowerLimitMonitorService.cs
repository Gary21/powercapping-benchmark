namespace client.Services;

public class PowerLimitMonitorService(PowerLimitEnforcer enforcer) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await enforcer.EnforceAsync(stoppingToken);
        }
    }
}
