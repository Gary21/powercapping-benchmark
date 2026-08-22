using System.Diagnostics;

namespace client.Services;

public class EnergyMeasurementService
{
    private const string CpuBasePath = "/sys/devices/virtual/powercap/intel-rapl/intel-rapl:0/";
    private const string CpuEnergyPath = CpuBasePath + "energy_uj";
    private Stopwatch watch = null;
    private ulong gpuStartEnergy = 0;
    private ulong cpuStartEnergy = 0;
    
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
        var energyUsed = (currentEnergy - gpuStartEnergy) / 1000.0d; // uJ is consistent with cpu
        var timeUsed = watch.Elapsed.TotalSeconds;
        return (energyUsed, timeUsed);
    }

    public void StartCpuPowerMeasurement()
    {
        watch = Stopwatch.StartNew();
        cpuStartEnergy = ulong.Parse(File.ReadAllText(CpuEnergyPath));
    }
    
    public (double, double) GetCpuEnergyUsed()
    {
        var currentEnergy = ulong.Parse(File.ReadAllText(CpuEnergyPath));
        watch.Stop();
        var energyUsed = (currentEnergy - cpuStartEnergy) * 1.0d;
        var timeUsed = watch.Elapsed.TotalSeconds;
        return (energyUsed, timeUsed);
    }
    
    ~EnergyMeasurementService()
    {
        NvmlProxy.Shutdown();
    }
}