using System.Text;
using Chess;
using ManagedCuda.Nvml;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace benchmark_client;

class Client
{
    private const string InitQueue = "init_queue";
    private const string ResultQueue = "result_queue";
    private const string EnginePathStockfish = "engines/stockfish/stockfish-linux";
    private const string EnginePathLc0 = "engines/lc0/lc0";
    private static long cpuMax;
    private static uint gpuMin;
    private static uint gpuMax;
    private static nvmlDevice device;
    static async Task Main()
    {
        string cpuBasePath = "/sys/class/powercap/intel-rapl:0/constraint_0_";
        cpuMax = long.Parse(File.ReadAllText(cpuBasePath + "max_power_uw"));
        Console.WriteLine($"Max cpu: {cpuMax/1000000} W");
        
        var result = NvmlNativeMethods.nvmlInit();
        if (result != nvmlReturn.Success)
        {
            Console.WriteLine($"Błąd inicjalizacji NVML: {result}");
            return;
        }
        
        device = new nvmlDevice();
        result = NvmlNativeMethods.nvmlDeviceGetHandleByIndex(0, ref device);
        if (result != nvmlReturn.Success)
        {
            Console.WriteLine($"Błąd uchwytu: {result}");
            NvmlNativeMethods.nvmlShutdown();
            return;
        }
        
        result = NvmlNativeMethods.nvmlDeviceGetPowerManagementLimitConstraints(device, ref gpuMin, ref gpuMax);
        if (result == nvmlReturn.Success)
        {
            Console.WriteLine($"Min: {gpuMin/1000} W, Max: {gpuMax/1000} W");
        }
        
        var stockfishCommunaction1 = new EngineCommunication(EnginePathStockfish);
        var lc0Communaction1 = new EngineCommunication(EnginePathLc0);
        var stockfishCommunaction2 = new EngineCommunication(EnginePathStockfish + "2");
        var lc0Communaction2 = new EngineCommunication(EnginePathLc0);
        
        var factory = new ConnectionFactory() { HostName = "localhost" };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(queue: InitQueue, durable: false, exclusive: false, autoDelete: false, arguments: null);
        await channel.QueueDeclareAsync(queue: ResultQueue, durable: false, exclusive: false, autoDelete: false, arguments: null);

        // Nasłuchiwanie na experiment_queue
        var experimentConsumer = new AsyncEventingBasicConsumer(channel);
        experimentConsumer.ReceivedAsync += async (model, ea) =>
        {
            var message = Encoding.UTF8.GetString(ea.Body.ToArray());
            var parts = message.Split(';');
            if (parts.Length != 4)
            {
                Console.WriteLine("Nieprawidłowy format wiadomości eksperymentu.");
                return;
            }
            var engine = parts[0];
            var powerCap = int.Parse(parts[1]);
            var numberOfGames = int.Parse(parts[2]);
            var powercapOnWhite = int.Parse(parts[3]) == 1;
            
            Console.WriteLine($"Otrzymano eksperyment: {engine}, powerCap: {powerCap}");

            for (int i = 0; i < numberOfGames; i++)
            {
                var result = engine == "stockfish"
                    ? await PlaySingleGame(stockfishCommunaction1, stockfishCommunaction2, false, powerCap, powercapOnWhite)
                    : await PlaySingleGame(lc0Communaction1, lc0Communaction2, true, powerCap, powercapOnWhite);
                var resultMsg = $"{engine};{powerCap};{result}";
                var resultBody = Encoding.UTF8.GetBytes(resultMsg);
                await channel.BasicPublishAsync(exchange: "", routingKey: ResultQueue, body: resultBody);
                Console.WriteLine($"Wysłano wynik partii: {resultMsg}");
            }
        };
        await channel.BasicConsumeAsync(queue: InitQueue, autoAck: true, consumer: experimentConsumer);

        Console.WriteLine("Naciśnij Enter, aby zakończyć...");
        Console.ReadLine();
    }
    
    private static async Task<string> PlaySingleGame(EngineCommunication engineCommunication1, EngineCommunication engineCommunication2, bool isGpu, int powerCap, bool powercapOnWhite)
    {
        var startingFen = GenerateChess960StartFEN();
        await PrepareEngine(engineCommunication1, isGpu, startingFen);
        await PrepareEngine(engineCommunication2, isGpu, startingFen);
        var board = ChessBoard.LoadFromFen(startingFen, AutoEndgameRules.All);
        

        int isWhite = 1;
        long wtime = 120000;
        long btime = 120000;
        
        while (!board.IsEndGame)
        {
            ChangePowerCap(powerCap, isWhite, isGpu, powercapOnWhite);
            Console.WriteLine("dupa");
            var (newMove, timeElapsed) = await MakeMove(isWhite == 1 ? engineCommunication1 : engineCommunication2, wtime, btime);
            if (isWhite == 1)
            {
                wtime -= timeElapsed;
            }
            else
            {
                btime -= timeElapsed;
            }
            Console.Write("White time: " + wtime/1000 + "s, Black time: " + btime/1000 +"s\n");
            board.Move(newMove);

            Console.WriteLine(board.ToAscii());
            //Console.WriteLine(wtime/1000);
            //Console.WriteLine(btime/1000);
            var fen = board.ToFen();
            await engineCommunication1.Input.WriteLineAsync($"position fen {fen}");
            await engineCommunication2.Input.WriteLineAsync($"position fen {fen}");
            isWhite *= -1;
            //Console.WriteLine(board.ToAscii());
        }
        
        if(board.EndGame.WonSide == PieceColor.White)
        {
            if (powercapOnWhite)
            {
                return "powercap";
            }
            else
            {
                return "nocap";
            }
        }
        if(board.EndGame.WonSide == PieceColor.Black)
        {
            if (powercapOnWhite)
            {
                return "nocap";
            }
            else
            {
                return "powercap";
            }
        }
        return "draw";
    }

    private static async Task<(Move,long)> MakeMove(EngineCommunication engineCommunication, long wtime, long btime)
    {
        string line;
        
        await engineCommunication.Input.WriteLineAsync("go wtime " + wtime + " btime " + btime);
        //await engineCommunication.Input.WriteLineAsync("go movetime 30000");
        string path = "/sys/class/powercap/intel-rapl/intel-rapl:0/energy_uj";
        string nps = "";
        string depth = "";
        string nodes = "";
        string score = "";
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var startUj = long.Parse(File.ReadAllText(path));
        while ((line = await engineCommunication.Output.ReadLineAsync()) != null)
        {
            //Console.WriteLine(line);
            if (line.StartsWith("bestmove"))
            {
                var finishUj = long.Parse(File.ReadAllText(path));
                watch.Stop();
                Console.WriteLine((finishUj - startUj)/watch.ElapsedMilliseconds);
                var move = line.Split(' ')[1];
                string moveFrom = move.Substring(0, 2);
                string moveTo = move.Substring(2, 2);
                var bestMove = new Move(moveFrom, moveTo);
                Console.Write("Depth: " + depth + ", NPS: " + nps + ", Nodes: " + nodes + ", Score:" + score + "\n");
                return (bestMove, watch.ElapsedMilliseconds);
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
        
        return (null, 0);
    }

    private static void ChangePowerCap(int powerCap, int isWhite, bool isGpu, bool powercapOnWhite)
    {
        if (isGpu)
        { 
            if ((isWhite == 1 && powercapOnWhite) ||  (isWhite == -1 && !powercapOnWhite))
            {
                ChangeGpuPowerCap(100);
            }
            else
            {
                ChangeGpuPowerCap((uint)powerCap);
            }
        }
        else
        {
            if ((isWhite == 1 && powercapOnWhite) ||  (isWhite == -1 && !powercapOnWhite))
            {
                ChangeCpuPowerCap(100);
            }
            else
            {
                ChangeCpuPowerCap(powerCap);
            }
        }
    }

    private static void ChangeCpuPowerCap(int powerLimit)
    {
        string path = "/sys/class/powercap/intel-rapl/intel-rapl:0";
        var newLimit = cpuMax * 0.01 * powerLimit;
        Console.WriteLine(newLimit);
        //var newLimit = powerLimit == 100 ? 50000000 : 7000000;
        File.WriteAllText(path + "/constraint_0_power_limit_uw", newLimit.ToString());
        //File.WriteAllText(path + "/constraint_1_power_limit_uw", newLimit.ToString());
        //File.WriteAllText(path + "/constraint_2_power_limit_uw", newLimit.ToString());
        File.WriteAllText(path + "/constraint_0_time_window_us", 798387.ToString());
        //File.WriteAllText(path + "/constraint_1_time_window_us", 100000.ToString());
        Thread.Sleep(100);
    }
    
    private static void ChangeGpuPowerCap(uint powerLimit)
    {
        var newLimit = (uint) (gpuMin + ((gpuMax - gpuMin) * 0.01 * powerLimit));
        //Console.WriteLine(newLimit);
        var result = NvmlNativeMethods.nvmlDeviceSetPowerManagementLimit(device, newLimit);
        //Console.WriteLine(result == nvmlReturn.Success
        //? $"Nowy limit ustawiony: {newLimit / 1000} W"
        //: $"Błąd ustawiania: {result}");
        Thread.Sleep(100);
    }

    private static async Task PrepareEngine(EngineCommunication engineCommunication, bool isGpu, string startingFen)
    {
        string line;
        await engineCommunication.Input.WriteLineAsync("uci");
        while ((line = await engineCommunication.Output.ReadLineAsync()) != null)
        {
            if (line == "uciok") break;
        }
        
        await engineCommunication.Input.WriteLineAsync("ucinewgame");
        //await engineCommunication.Input.WriteLineAsync("position startpos");
        await engineCommunication.Input.WriteLineAsync($"position fen {startingFen}");
        await engineCommunication.Input.WriteLineAsync("setoption name Ponder value false");
        await engineCommunication.Input.WriteLineAsync("setoption name UCI_Chess960 value true");
        
        
        //await engineCommunication.Input.WriteLineAsync("setoption name Contempt value 100");
        //await engineCommunication.Input.WriteLineAsync("setoption name UCI_Elo value 3190");
        //await engineCommunication.Input.WriteLineAsync("setoption name Hash value 1");

        if (!isGpu)
        {
            await engineCommunication.Input.WriteLineAsync("setoption name Threads value 12");
            //await engineCommunication.Input.WriteLineAsync(" setoption name Use NNUE value false");
        }
            
        
       
        
        await engineCommunication.Input.WriteLineAsync("isready");
        while ((line = await engineCommunication.Output.ReadLineAsync()) != null)
        {
            if (line == "readyok") break;
        }
    }
    
    private static readonly Random rng = new();

    public static string GenerateChess960StartFEN()
    {
        char[] backRank = new char[8];
        List<int> emptySquares = new();

        for (int i = 0; i < 8; i++)
            emptySquares.Add(i);

        // 1. Umieść gońce na polach o różnych kolorach
        int bishop1 = GetRandomIndex(emptySquares, i => i % 2 == 0); // białe pola
        backRank[bishop1] = 'B';
        emptySquares.Remove(bishop1);

        int bishop2 = GetRandomIndex(emptySquares, i => i % 2 == 1); // czarne pola
        backRank[bishop2] = 'B';
        emptySquares.Remove(bishop2);

        // 2. Umieść królową
        int queen = GetRandomIndex(emptySquares);
        backRank[queen] = 'Q';
        emptySquares.Remove(queen);

        // 3. Umieść skoczki
        int knight1 = GetRandomIndex(emptySquares);
        backRank[knight1] = 'N';
        emptySquares.Remove(knight1);

        int knight2 = GetRandomIndex(emptySquares);
        backRank[knight2] = 'N';
        emptySquares.Remove(knight2);

        // 4. Umieść wieże i króla: R-K-R (król między wieżami)
        // Sortujemy pozostałe 3 pozycje i umieszczamy R, K, R
        emptySquares.Sort();
        backRank[emptySquares[0]] = 'R';
        backRank[emptySquares[1]] = 'K';
        backRank[emptySquares[2]] = 'R';

        // 5. Zbuduj FEN: np. "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"
        string whiteBackRank = new string(backRank).ToUpper();
        string blackBackRank = whiteBackRank.ToLower();

        string fen = $"{blackBackRank}/pppppppp/8/8/8/8/PPPPPPPP/{whiteBackRank} w KQkq - 0 1";
        return fen;
    }

    private static int GetRandomIndex(List<int> list, Predicate<int>? predicate = null)
    {
        var filtered = predicate == null ? list : list.FindAll(predicate);
        if (filtered.Count == 0)
            throw new InvalidOperationException("No valid squares available for placement.");

        return filtered[rng.Next(filtered.Count)];
    }
}

// watch -n 1 nvidia-smi
// mpstat -P ALL 1
// sudo turbostat --interval 0.3
// sudo powercap-info -p intel-rapl
// sudo powercap-set intel-rapl -z 0 -c 2 -l 65000000
// stress-ng --cpu 8 --cpu-method matrixprod