using System.Diagnostics;

namespace client.Services;

public class EngineCommunication
{
    private readonly Process _process;
    public readonly StreamWriter Input;
    public readonly StreamReader Output;
    private static long cpuMax;
    const string cpuBasePath = "/sys/class/powercap/intel-rapl:0/constraint_0_";

    public EngineCommunication()
    {
        cpuMax = long.Parse(File.ReadAllText(cpuBasePath + "max_power_uw"));
        Console.WriteLine($"Max cpu: {cpuMax/1000000} W");
        var psi = new ProcessStartInfo
        {
            FileName = "engines/stockfish",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        _process = Process.Start(psi);
        Input = _process.StandardInput;
        Output = _process.StandardOutput;
        SetupEngine();
    }
    
    public string MakeMove(string currentFen)
    {
        Input.WriteLine($"position fen {currentFen}");
        WaitForReady();
        Input.WriteLine("go movetime 10000");
        string bestMove = "";
        string nps = "";
        string depth = "";
        string nodes = "";
        string score = "";
        while (true)
        {
            var line = Output.ReadLine();
            Console.WriteLine($"[Stockfish]: {line}");
            if (line.StartsWith("bestmove"))
            {
                bestMove = line.Split(' ')[1];
                Console.Write("Best move: " + bestMove + "\n");
                Console.Write("Depth: " + depth + ", NPS: " + nps + ", Nodes: " + nodes + ", Score:" + score + "\n");
                return bestMove;
            }
            if(line.StartsWith("info"))
            {
                var values =  line.Split(' ');
                if (values.Length > 13)
                {
                    depth = values[2];
                    nps = values[13];
                    nodes = values[11];
                    score = values[9];
                }
            }
        }
    }
    
    private void SetupEngine()
    {
        Input.WriteLine("uci");
        while (Output.ReadLine() != "uciok") { }
        Input.WriteLine("ucinewgame");
        WaitForReady();
    }

    private void WaitForReady()
    {
        Input.WriteLine("isready");
        while (Output.ReadLine() != "readyok") { }
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