using System.Collections.Concurrent;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace benchmark_server;
class Server
{
    private const string InitQueue = "init_queue";
    private const string ResultQueue = "result_queue";

    static async Task Main()
    {
        var database = new DatabaseHandler();
        
        var factory = new ConnectionFactory() { HostName = "localhost" };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(queue: InitQueue, durable: false, exclusive: false, autoDelete: false, arguments: null);
        await channel.QueueDeclareAsync(queue: ResultQueue, durable: false, exclusive: false, autoDelete: false, arguments: null);
        
        /*ExperimentResult newResult = new ExperimentResult("lc0", 10);
        var message = $"{newResult.Engine};{newResult.PowerCap};50";
        var body = Encoding.UTF8.GetBytes(message);
        await channel.BasicPublishAsync(exchange: "", routingKey: InitQueue, body: body);
        Console.WriteLine($"Wysłano eksperyment: {message}");
        database.UpsertResult(newResult);*/

        for (int i = 10; i >= 10; i -= 10)
        {
            ExperimentResult newResult = new ExperimentResult("stockfish", i);
            var message = $"{newResult.Engine};{newResult.PowerCap};200;1";
            var body = Encoding.UTF8.GetBytes(message);
            await channel.BasicPublishAsync(exchange: "", routingKey: InitQueue, body: body);
            Console.WriteLine($"Wysłano eksperyment: {message}");
            database.UpsertResult(newResult);
            
            ExperimentResult newResult1 = new ExperimentResult("stockfish", i);
            var message1 = $"{newResult1.Engine};{newResult1.PowerCap};200;2";
            var body1 = Encoding.UTF8.GetBytes(message1);
            await channel.BasicPublishAsync(exchange: "", routingKey: InitQueue, body: body1);
            Console.WriteLine($"Wysłano eksperyment: {message1}");
            database.UpsertResult(newResult1);
        }
        

        
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (model, ea) =>
        {
            var message = Encoding.UTF8.GetString(ea.Body.ToArray());
            var parts = message.Split(';');
            if (parts.Length == 3)
            {
                var engine = parts[0];
                var powercap = parts[1];
                var matchResult = parts[2];
                switch (matchResult)
                {
                    case "white":
                        database.UpsertResult(new ExperimentResult(engine, int.Parse(powercap), nocapWins: 1));
                        break;
                    case "black":
                        database.UpsertResult(new ExperimentResult(engine, int.Parse(powercap), powercapWins: 1));
                        break;
                    case "draw":
                        database.UpsertResult(new ExperimentResult(engine, int.Parse(powercap), draws: 1));
                        break;
                }

                foreach (var result in database.GetAllResults())
                {
                    Console.WriteLine(
                        $"Engine: {result.Engine};Powercap: {result.PowerCap};\n Nocap wins: {result.NocapWins}; Powercap wins: {result.PowercapWins};Draws: {result.Draws}");

                }
                Console.WriteLine(database.GetAllResults());
            }
            return Task.CompletedTask;
        };
        await channel.BasicConsumeAsync(queue: ResultQueue, autoAck: true, consumer: consumer);

        Console.WriteLine("Naciśnij Enter, aby zakończyć...");
        Console.ReadLine();
    }
}