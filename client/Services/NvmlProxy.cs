namespace client.Services;

using System;
using System.Runtime.InteropServices;

public static class NvmlProxy
{
    private const string Library = "libnvidia-ml.so.1";

    private enum NvmlReturn
    {
        Success = 0
    }

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern NvmlReturn nvmlInit();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern NvmlReturn nvmlShutdown();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern NvmlReturn nvmlDeviceGetHandleByIndex(
        uint index,
        out IntPtr device);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern NvmlReturn nvmlDeviceGetTotalEnergyConsumption(
        IntPtr device,
        out ulong energy);

    public static void Init()
    {
        Check(nvmlInit(), nameof(nvmlInit));
    }

    public static void Shutdown()
    {
        nvmlShutdown();
    }

    public static ulong GetEnergyMilliJoules(uint gpuIndex)
    {
        Check(
            nvmlDeviceGetHandleByIndex(gpuIndex, out var device),
            nameof(nvmlDeviceGetHandleByIndex));

        Check(
            nvmlDeviceGetTotalEnergyConsumption(device, out var energy),
            nameof(nvmlDeviceGetTotalEnergyConsumption));

        return energy;
    }

    private static void Check(NvmlReturn result, string operation)
    {
        if (result != NvmlReturn.Success)
            throw new InvalidOperationException(
                $"{operation} failed: NVML error {(int)result}");
    }
}