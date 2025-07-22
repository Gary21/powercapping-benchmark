using System.Diagnostics;

namespace benchmark_client;

public class EngineCommunication
{
    private readonly Process _process;
    public readonly StreamWriter Input;
    public readonly StreamReader Output;

    public EngineCommunication(string enginePath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = enginePath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        _process = Process.Start(psi);
        Input = _process.StandardInput;
        Output = _process.StandardOutput;
    }
    
    ~EngineCommunication()
    {
        if (!_process.HasExited)
        {
            _process.Kill();
        }
        Input?.Dispose();
        Output?.Dispose();
    }
}