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
    private const string EnginePathStockfish = "engines/stockfish/stockfish-windows.exe";
    private const string EnginePathLc0 = "engines/lc0/lc0.exe";
    static async Task Main()
    {
        string cpuBasePath = "/sys/class/powercap/intel-rapl:0/constraint_0_";
        long cpuMin = long.Parse(File.ReadAllText(cpuBasePath + "min_power_uw"));
        long cpuMax = long.Parse(File.ReadAllText(cpuBasePath + "max_power_uw"));
        
        var result = NvmlNativeMethods.nvmlInit();
        if (result != nvmlReturn.Success)
        {
            Console.WriteLine($"Błąd inicjalizacji NVML: {result}");
            return;
        }
        
        nvmlDevice device = new nvmlDevice();
        result = NvmlNativeMethods.nvmlDeviceGetHandleByIndex(0, ref device);
        if (result != nvmlReturn.Success)
        {
            Console.WriteLine($"Błąd uchwytu: {result}");
            NvmlNativeMethods.nvmlShutdown();
            return;
        }
        
        uint gpuMin = 0, gpuMax = 0;
        result = NvmlNativeMethods.nvmlDeviceGetPowerManagementLimitConstraints(device, ref gpuMin, ref gpuMax);
        if (result == nvmlReturn.Success)
        {
            Console.WriteLine($"Min: {gpuMin/1000} W, Max: {gpuMax/1000} W");
        }
        
        var stockfishCommunaction = new EngineCommunication(EnginePathStockfish);
        var lc0Communaction = new EngineCommunication(EnginePathLc0);
        
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
            if (parts.Length != 3)
            {
                Console.WriteLine("Nieprawidłowy format wiadomości eksperymentu.");
                return;
            }
            var engine = parts[0];
            var powerCap = int.Parse(parts[1]);
            var numberOfGames = int.Parse(parts[2]);
            
            Console.WriteLine($"Otrzymano eksperyment: {engine}, powerCap: {powerCap}");

            for (int i = 0; i < numberOfGames; i++)
            {
                var result = engine == "stockfish"
                    ? await PlaySingleGame(stockfishCommunaction)
                    : await PlaySingleGame(lc0Communaction);
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
    
    private static async Task<string> PlaySingleGame(EngineCommunication engineCommunication, bool isGpu)
    {
        string line;
        
        await engineCommunication.Input.WriteLineAsync("uci");
        while ((line = await engineCommunication.Output.ReadLineAsync()) != null)
        {
            if (line == "uciok") break;
        }
        
        await engineCommunication.Input.WriteLineAsync("ucinewgame");
        await engineCommunication.Input.WriteLineAsync("position startpos");
        
        await engineCommunication.Input.WriteLineAsync("isready");
        while ((line = await engineCommunication.Output.ReadLineAsync()) != null)
        {
            if (line == "readyok") break;
        }

        var board = new ChessBoard() {AutoEndgameRules = AutoEndgameRules.All};

        while (!board.IsEndGame)
        {
            await engineCommunication.Input.WriteLineAsync("go movetime 5");
            while ((line = await engineCommunication.Output.ReadLineAsync()) != null)
            {
                if (line.StartsWith("bestmove"))
                {
                    var move = line.Split(' ')[1];
                    string moveFrom = move.Substring(0, 2);
                    string moveTo = move.Substring(2, 2);
                    var bestMove = new Move(moveFrom, moveTo);
                    board.Move(bestMove);
                    break;
                }
            }

            if (isGpu)
            {
                //zmienic limit mocy GPU
                /*
                    result = NvmlNativeMethods.nvmlDeviceSetPowerManagementLimit(device, newLimit);
                    Console.WriteLine(result == nvmlReturn_t.Success
                ? $"Nowy limit ustawiony: {newLimit / 1000} W"
                : $"Błąd ustawiania: {result}");
                 */
            }
            else
            {
                //zmienic limit mocy CPU
                /*
                string path = "/sys/class/powercap/intel-rapl:0/constraint_0_power_limit_uw";
                File.WriteAllText(path, "65000000"); // np. 65W*/
            }

            var fen = board.ToFen();
            await engineCommunication.Input.WriteLineAsync($"position fen {fen}");
            //Console.WriteLine(board.ToAscii());
        }
        
        if(board.EndGame.WonSide == PieceColor.White)
        {
            return "white";
        }
        if(board.EndGame.WonSide == PieceColor.Black)
        {
            return "black";
        }
        return "draw";
    }
}