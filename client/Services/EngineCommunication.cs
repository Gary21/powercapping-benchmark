using System.Diagnostics;
using server.Models;

namespace client.Services;

public class EngineCommunication : IDisposable
{
    private readonly Process _process;
    public readonly StreamWriter Input;
    public readonly StreamReader Output;
    private double totalEnergyUsed = 0;
    private double totalTimeUsed = 0;
    private List<int> _npsHistory = [];
    private PowerLimitState _powerState;
    

    public EngineCommunication(bool isWhite, int powerCapPercent, string powerCapColor, PowerLimitState powerState)
    {
        _powerState = powerState;
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
        SetupEngine(isWhite, powerCapPercent, powerCapColor);
    }
    
    public SubmitMoveModel MakeMove(MoveMadeModel moveMade)
    {
        string path = "/sys/class/powercap/intel-rapl/intel-rapl:0/energy_uj";
        Input.WriteLine(moveMade.CurrentMoves);
        WaitForReady();
        string nps = "";
        string depth = "";
        string nodes = "";
        string score = "";
        var moveCommand =
            $"go wtime {moveMade.WhiteTimeLeft} btime {moveMade.BlackTimeLeft} winc {moveMade.Increment} binc {moveMade.Increment}";
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var startUj = long.Parse(File.ReadAllText(path));
        Input.WriteLine(moveCommand);
        while (true)
        {
            var line = Output.ReadLine();
            Console.WriteLine($"[Stockfish]: {line}");
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
            else if (line.StartsWith("bestmove"))
            {
                try
                {
                    var finishUj = long.Parse(File.ReadAllText(path));
                    watch.Stop();
                    var energyJ = (finishUj - startUj) / 1_000_000.0;
                    var timeS = watch.ElapsedMilliseconds / 1000.0;
                    var powerW = timeS != 0 ? energyJ / timeS : 0.0d;
                    _npsHistory.Add(Int32.Parse(nps));
                    Console.WriteLine(powerW + " W");
                    if (energyJ > 0)
                    {
                        totalEnergyUsed += energyJ;
                        totalTimeUsed += timeS;
                    }

                    var avgPowerW = totalTimeUsed != 0 ? totalEnergyUsed / totalTimeUsed : 0.0d;
                    Console.WriteLine("Average power: " + avgPowerW + " W");
                    var bestMove = line.Split(' ')[1];
                    Console.Write("Best move: " + bestMove + "\n");
                    Console.Write("Depth: " + depth + ", NPS: " + nps + ", Nodes: " + nodes + ", Score:" + score +
                                  "\n");
                    var submitMove = new SubmitMoveModel
                    {
                        Move = bestMove,
                        AvgPower = avgPowerW,
                        AvgNps = _npsHistory.Average()
                    };
                    return submitMove;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }
    }
    
    private void SetupEngine(bool isWhite, int powerCapPercent, string powerCapColor)
    {
        Input.WriteLine("uci");
        while (Output.ReadLine() != "uciok") { }
        if ((isWhite && powerCapColor == "white") || (!isWhite && powerCapColor == "black"))
        {
            _powerState.TargetLimitPercent = powerCapPercent;
            Thread.Sleep(100);
        }
        if ((!isWhite && powerCapColor == "white") || (isWhite && powerCapColor == "black"))
        {
            _powerState.TargetLimitPercent = 100;
            Thread.Sleep(100);
        }
        Input.WriteLine("setoption name Clear Hash");
        Input.WriteLine("setoption name Threads value 24");
        Input.WriteLine("setoption name Hash value 2048");
        Input.WriteLine("ucinewgame");
        WaitForReady();
    }

    private void WaitForReady()
    {
        Input.WriteLine("isready");
        while (Output.ReadLine() != "readyok") { }
    }
    
    public void Dispose()
    {
        if (!_process.HasExited)
        {
            _process.Kill();
        }
        Input?.Dispose();
        Output?.Dispose();
    }
    
    ~EngineCommunication()
    {
        Dispose();
    }
}