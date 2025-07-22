using System.Collections.Concurrent;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace benchmark_server;

class Server
{
    private static readonly ConcurrentDictionary<string, (int White, int Black, int Draws)> Stats = new();
    private const string InitQueue = "init_queue";
    private const string ResultQueue = "result_queue";

    static async Task Main()
    {
        var factory = new ConnectionFactory() { HostName = "localhost" };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(queue: InitQueue, durable: false, exclusive: false, autoDelete: false, arguments: null);
        await channel.QueueDeclareAsync(queue: ResultQueue, durable: false, exclusive: false, autoDelete: false, arguments: null);

        for (int i = 0; i < 5; i++)
        {
            string experimentId = Guid.NewGuid().ToString();
            var body = Encoding.UTF8.GetBytes(experimentId);
            await channel.BasicPublishAsync(exchange: "", routingKey: InitQueue, body: body);
            Console.WriteLine($"Wysłano eksperyment ID: {experimentId}");
            Stats.TryAdd(experimentId, (0, 0, 0));
        }
        
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (model, ea) =>
        {
            var message = Encoding.UTF8.GetString(ea.Body.ToArray());
            var parts = message.Split(';');
            if (parts.Length == 2)
            {
                var id = parts[0];
                var result = parts[1];
                if (Stats.TryGetValue(id, out var old))
                {
                    var updated = result switch
                    {
                        "white" => (old.White + 1, old.Black, old.Draws),
                        "black" => (old.White, old.Black + 1, old.Draws),
                        "draw" => (old.White, old.Black, old.Draws + 1),
                        _ => old
                    };
                    Stats[id] = updated;
                }
                Console.WriteLine($"ID: {id} | Biały: {Stats[id].White}, Czarny: {Stats[id].Black}, Remisy: {Stats[id].Draws}");
            }
            return Task.CompletedTask;
        };
        await channel.BasicConsumeAsync(queue: ResultQueue, autoAck: true, consumer: consumer);

        Console.WriteLine("Naciśnij Enter, aby zakończyć...");
        Console.ReadLine();
    }
}