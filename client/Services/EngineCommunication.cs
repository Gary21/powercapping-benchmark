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
    private EnergyMeasurementService _energyMeasurementService;
    // the limit is fixed for the whole game, so it is taken once from GameStarted and not from every MoveMade
    private readonly string _limitMode;
    private readonly int? _timePerMove;
    private readonly long? _nodesPerMove;


    public EngineCommunication(bool isWhite, NewGameModel game, PowerLimitState powerState, EnergyMeasurementService energyMeasurementService)
    {
        // checked before the engine process is started, so a bad game doesn't leave a process behind
        // time_control is not range-checked here: the server validates it and the client must keep accepting what an old server sends
        _limitMode = LimitModes.Normalize(game.LimitMode);
        if (_limitMode != LimitModes.TimeControl &&
            !LimitModes.HasValidLimits(_limitMode, game.TimeControl, game.TimeIncrement, game.TimePerMove, game.NodesPerMove))
            throw new ArgumentException($"Missing or invalid limit for mode '{_limitMode}' (game {game.GameId}).", nameof(game));
        _timePerMove = game.TimePerMove;
        _nodesPerMove = game.NodesPerMove;
        var powerCapPercent = game.PowerCap;
        var powerCapColor = game.PowerCapColor;
        var isGpu = game.IsGpu;
        _energyMeasurementService = energyMeasurementService;
        _powerState = powerState;
        _powerState.isGpu = isGpu;
        var enginePath = isGpu ? "engines/lc0" : "engines/stockfish";
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
        SetupEngine(isWhite, powerCapPercent, powerCapColor);
    }
    
    public SubmitMoveModel MakeMove(MoveMadeModel moveMade)
    {
        Input.WriteLine(moveMade.CurrentMoves);
        WaitForReady();
        string nps = "";
        string depth = "";
        string nodes = "";
        string score = "";
        var moveCommand = BuildGoCommand(_limitMode, _timePerMove, _nodesPerMove, moveMade);
        if (_powerState.isGpu)
        {
            _energyMeasurementService.StartGpuPowerMeasurement();
        }
        else
        {
            _energyMeasurementService.StartCpuPowerMeasurement();
        }
        Input.WriteLine(moveCommand);
        while (true)
        {
            var line = Output.ReadLine();
            Console.WriteLine($"[Engine]: {line}");
            if(line.StartsWith("info"))
            {
                var values =  line.Split(' ');
                if (values.Length > 13)
                {
                    (depth, nps, nodes, score) = ParseInfoLine(values);
                }
            }
            else if (line.StartsWith("bestmove"))
            {
                try
                {
                    var (energyJ, timeS) = _powerState.isGpu ? _energyMeasurementService.GetGpuEnergyUsed() : _energyMeasurementService.GetCpuEnergyUsed();
                    var powerW = timeS != 0 ? energyJ / timeS : 0.0d;
                    var npsInt = 0;
                    Int32.TryParse(nps, out npsInt);
                    if(npsInt > 0 )
                        _npsHistory.Add(npsInt);
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
                        AvgNps = _npsHistory.Count > 0 ? _npsHistory.Average() : 0.0d
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
    
    // clocks in moveMade are only used (and only meaningful) in time_control mode
    public static string BuildGoCommand(string limitMode, int? timePerMove, long? nodesPerMove, MoveMadeModel moveMade)
        => limitMode switch
        {
            LimitModes.TimeControl =>
                $"go wtime {moveMade.WhiteTimeLeft} btime {moveMade.BlackTimeLeft} winc {moveMade.Increment} binc {moveMade.Increment}",
            LimitModes.TimePerMove => $"go movetime {timePerMove}",
            LimitModes.NodesPerMove => $"go nodes {nodesPerMove}",
            _ => throw new ArgumentException($"Unknown limit mode: '{limitMode}'", nameof(limitMode))
        };

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
        
        if (!_powerState.isGpu)
        {
            Input.WriteLine("setoption name Clear Hash");
            Input.WriteLine("setoption name Threads value 12");
            Input.WriteLine("setoption name Hash value 2048");
        }
        else
        {
            Input.WriteLine("setoption name Contempt value 0.5");
        }
        //Input.WriteLine("setoption name MultiPV value 3");
        Input.WriteLine("ucinewgame");
        WaitForReady();
    }

    private void WaitForReady()
    {
        Input.WriteLine("isready");
        while (Output.ReadLine() != "readyok") { }
    }
    private (string,string,string,string) ParseInfoLine(string[] values)
    {
        string nps = "";
        string depth = "";
        string nodes = "";
        string score = "";
        if (!_powerState.isGpu)
        {
            depth = values[2];
            nps = values[13];
            nodes = values[11];
            score = values[9];
        }
        else
        {
            depth = values[2];
            nps = values[13];
            nodes = values[8];
            score = values[11];
        }
        return (depth, nps, nodes, score);
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