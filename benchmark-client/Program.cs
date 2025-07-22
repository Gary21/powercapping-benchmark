using System.Text;
using Chess;
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
            var experimentId = Encoding.UTF8.GetString(ea.Body.ToArray());
            Console.WriteLine($"Otrzymano eksperyment ID: {experimentId}");

            for (int i = 0; i < 10; i++)
            {
                var result = await PlaySingleGame(lc0Communaction);
                var resultMsg = $"{experimentId};{result}";
                var resultBody = Encoding.UTF8.GetBytes(resultMsg);
                await channel.BasicPublishAsync(exchange: "", routingKey: ResultQueue, body: resultBody);
                Console.WriteLine($"Wysłano wynik partii: {resultMsg}");
            }
        };
        await channel.BasicConsumeAsync(queue: InitQueue, autoAck: true, consumer: experimentConsumer);

        Console.WriteLine("Naciśnij Enter, aby zakończyć...");
        Console.ReadLine();
    }
    
    private static async Task<string> PlaySingleGame(EngineCommunication engineCommunication)
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