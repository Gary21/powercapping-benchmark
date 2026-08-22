using System.Diagnostics;

namespace client.Services;

public class PowerLimitMonitorService : BackgroundService
{
    private readonly PowerLimitState _state;
    private const string CpuBasePath = "/sys/devices/virtual/powercap/intel-rapl/intel-rapl:0/";
    private const string PathLimit0 = CpuBasePath + "constraint_0_power_limit_uw";
    private const string PathLimit1 = CpuBasePath + "constraint_1_power_limit_uw";
    private const string PathTime0 = CpuBasePath + "constraint_0_time_window_us";
    private const string PathTime1 = CpuBasePath + "constraint_0_time_window_us";
    private static long cpuMax;
    private static long gpuMin;
    private static long gpuMax;
    
    
    public PowerLimitMonitorService(PowerLimitState state)
    {
        cpuMax = long.Parse(File.ReadAllText(CpuBasePath + "constraint_0_max_power_uw"));
        Console.WriteLine($"Max cpu: {cpuMax/1000000} W");
        (gpuMin, gpuMax) = GetGpuLimits();
        Console.WriteLine($"Min gpu: {gpuMin} W, Max gpu: {gpuMax} W");
        _state = state;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
                
            if (_state.TargetLimitPercent <= 0)
                continue;
            if (!_state.isGpu)
            {
                try
                {
                    var targetLimitUw = (long)(cpuMax * (_state.TargetLimitPercent / 100.0));
                    var targetTimeWindowUs = 976L;
                    var currentLimit0 = long.Parse(await File.ReadAllTextAsync(PathLimit0, stoppingToken));
                    var currentLimit1 = long.Parse(await File.ReadAllTextAsync(PathLimit1, stoppingToken));
                    var currentTimeWindow0 = long.Parse(await File.ReadAllTextAsync(PathTime0, stoppingToken));
                    var currentTimeWindow1 = long.Parse(await File.ReadAllTextAsync(PathTime1, stoppingToken));

                    if (currentLimit0 != targetLimitUw || currentLimit1 != targetLimitUw ||
                        currentTimeWindow0 != targetTimeWindowUs || currentTimeWindow1 != targetTimeWindowUs)
                    {
                        Console.WriteLine(
                            $"[Monitor]: Wykryto zmianę limitu. Przywracanie wartości: {targetLimitUw}");
                        await File.WriteAllTextAsync(PathLimit0, targetLimitUw.ToString(), stoppingToken);
                        await File.WriteAllTextAsync(PathLimit1, targetLimitUw.ToString(), stoppingToken);
                        await File.WriteAllTextAsync(PathTime0, targetTimeWindowUs.ToString(), stoppingToken);
                        await File.WriteAllTextAsync(PathTime1, targetTimeWindowUs.ToString(), stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Monitor]: Błąd podczas sprawdzania limitów RAPL: {ex.Message}");
                }
            }
            else
            {
                try
                {
                    var targetLimit = (long)((gpuMax - gpuMin) * (_state.TargetLimitPercent / 100.0)) + gpuMin;
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = "sudo",
                        Arguments = $"-n nvidia-smi -pl {targetLimit}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(processInfo);
                    if (process != null)
                    {
                        var output = await process.StandardOutput.ReadToEndAsync();
                        var error = await process.StandardError.ReadToEndAsync();

                        if (!string.IsNullOrWhiteSpace(error))
                        {
                            Console.WriteLine($"[Monitor]: Błąd podczas ustawiania maksymalnej mocy GPU: {error}");
                        }
                        else
                        {
                            Console.WriteLine($"[Monitor]: Maksymalna moc GPU ustawiona na {targetLimit}W");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Monitor]: Błąd podczas ustawiania maksymalnej mocy GPU: {ex.Message}");
                }
            }
        }
    }

    private (long, long) GetGpuLimits()
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=power.min_limit,power.max_limit --format=csv,noheader",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();

                if (!string.IsNullOrWhiteSpace(error))
                {
                    Console.WriteLine($"[Monitor]: Błąd podczas odczytu limitów mocy GPU: {error}");
                }
                else
                {
                    var powerLimits = output.Split(',');
                    if (powerLimits.Length >= 2 &&
                        double.TryParse(powerLimits[0].Trim().Replace(" W", ""), out var minPower) &&
                        double.TryParse(powerLimits[1].Trim().Replace(" W", ""), out var maxPower))
                    {
                        Console.WriteLine($"[Monitor]: Odczytano limity mocy GPU: Min = {minPower} W, Max = {maxPower} W");
                        return ((long)Math.Round(minPower), (long)Math.Round(maxPower));
                    }
                    else
                    {
                        Console.WriteLine("[Monitor]: Nie udało się sparsować limitów mocy GPU.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Monitor]: Błąd podczas odczytu limitów mocy GPU: {ex.Message}");
        }
        Console.WriteLine("[Monitor]: Błąd podczas odczytu limitów mocy GPU");
        return (0, 0);
    }
}