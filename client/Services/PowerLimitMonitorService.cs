namespace client.Services;

public class PowerLimitMonitorService : BackgroundService
{
    private readonly PowerLimitState _state;
    private const string CpuBasePath = "/sys/devices/virtual/powercap/intel-rapl/intel-rapl:0/";
    private const string PathLimit0 = CpuBasePath + "constraint_0_power_limit_uw";
    private const string PathLimit1 = CpuBasePath + "constraint_1_power_limit_uw";
    private const string PathLimit2 = CpuBasePath + "constraint_2_power_limit_uw";
    private static long cpuMax;
    
    public PowerLimitMonitorService(PowerLimitState state)
    {
        cpuMax = long.Parse(File.ReadAllText(CpuBasePath + "constraint_0_max_power_uw"));
        Console.WriteLine($"Max cpu: {cpuMax/1000000} W");
        _state = state;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (_state.TargetLimitPercent <= 0) 
                continue;

            try
            {
                var targetLimitUw = (long)(cpuMax * (_state.TargetLimitPercent / 100.0));
                var pl2Target = (long)(cpuMax * 1.6);
                var current0 = long.Parse(await File.ReadAllTextAsync(PathLimit0, stoppingToken));
                var current1 = long.Parse(await File.ReadAllTextAsync(PathLimit1, stoppingToken));
                var current2 = long.Parse(await File.ReadAllTextAsync(PathLimit2, stoppingToken));
                
                if (current0 != targetLimitUw || current1 != targetLimitUw)
                {
                    Console.WriteLine($"[Monitor]: Wykryto zmianę limitu. Przywracanie wartości: {targetLimitUw}");
                    await File.WriteAllTextAsync(PathLimit0, targetLimitUw.ToString(), stoppingToken);
                    await File.WriteAllTextAsync(PathLimit1, targetLimitUw.ToString(), stoppingToken);
                    //await File.WriteAllTextAsync(PathLimit2, pl2Target.ToString(), stoppingToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Monitor]: Błąd podczas sprawdzania limitów RAPL: {ex.Message}");
            }
        }
    }
}