using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

class Client
{
    static async Task Main()
    {
        // Ustawienia serwera, z którym się łączymy
        string serverIp = "127.0.0.1";
        int port = 8888;
        
        try
        {
            // Połączenie z serwerem
            using (TcpClient client = new TcpClient())
            {
                Console.WriteLine($"Łączenie z serwerem {serverIp}:{port}...");
                await client.ConnectAsync(serverIp, port);
                Console.WriteLine("Połączono z serwerem!");
                
                using (NetworkStream stream = client.GetStream())
                {
                    while (true)
                    {
                        // Wysyłanie wiadomości do serwera
                        Console.Write("Wpisz wiadomość (lub 'exit' aby zakończyć): ");
                        string message = Console.ReadLine();
                        
                        if (string.IsNullOrEmpty(message) || message.ToLower() == "exit")
                            break;
                        
                        byte[] data = Encoding.UTF8.GetBytes(message);
                        await stream.WriteAsync(data, 0, data.Length);
                        
                        // Odbieranie odpowiedzi od serwera
                        byte[] buffer = new byte[1024];
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Console.WriteLine($"Odpowiedź serwera: {response}");
                    }
                }
                
                Console.WriteLine("Rozłączono z serwerem.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd: {ex.Message}");
        }
        
        Console.WriteLine("Naciśnij dowolny klawisz, aby zakończyć...");
        Console.ReadKey();
    }
}