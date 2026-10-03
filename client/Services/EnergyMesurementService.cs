using System.Diagnostics;

namespace client.Services;

public class EnergyMeasurementService
{
    private const string CpuBasePath = "/sys/devices/virtual/powercap/intel-rapl/intel-rapl:0/";
    private const string CpuEnergyPath = CpuBasePath + "energy_uj";
    private Stopwatch watch = null;
    private ulong gpuStartEnergy = 0;
    private long cpuStartEnergy = 0;
    
    public EnergyMeasurementService()
    {
        NvmlProxy.Init();
    }
    
    public void StartGpuPowerMeasurement()
    {
        watch = Stopwatch.StartNew();
        gpuStartEnergy = NvmlProxy.GetEnergyMilliJoules(0);
    }

    public (double, double) GetGpuEnergyUsed()
    {
        var currentEnergy = NvmlProxy.GetEnergyMilliJoules(0);
        watch.Stop();
        var energyUsed = (currentEnergy - gpuStartEnergy) / 1000.0d;
        var timeUsed = watch.Elapsed.TotalSeconds;
        return (energyUsed, timeUsed);
    }

    public void StartCpuPowerMeasurement()
    {
        watch = Stopwatch.StartNew();
        cpuStartEnergy = long.Parse(File.ReadAllText(CpuEnergyPath));
    }
    
    public (double, double) GetCpuEnergyUsed()
    {
        var currentEnergy = long.Parse(File.ReadAllText(CpuEnergyPath));
        watch.Stop();
        var energyUsed = (currentEnergy - cpuStartEnergy) / 1_000_000.0;
        var timeUsed = watch.Elapsed.TotalSeconds;
        return (energyUsed, timeUsed);
    }
    
    ~EnergyMeasurementService()
    {
        NvmlProxy.Shutdown();
    }
}