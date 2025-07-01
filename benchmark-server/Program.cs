using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

class Server
{
    static async Task Main()
    {
        // Ustawienia serwera
        IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
        int port = 8888;
        
        // Utworzenie i konfiguracja gniazda TCP
        TcpListener server = new TcpListener(ipAddress, port);
        
        try
        {
            // Uruchomienie nasłuchiwania
            server.Start();
            Console.WriteLine($"Serwer uruchomiony na {ipAddress}:{port}");
            Console.WriteLine("Oczekiwanie na połączenia...");
            
            while (true)
            {
                // Akceptowanie połączenia klienta
                TcpClient client = await server.AcceptTcpClientAsync();
                Console.WriteLine("Klient połączony!");
                
                // Obsługa klienta w osobnym zadaniu
                _ = HandleClientAsync(client);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd: {ex.Message}");
        }
        finally
        {
            server.Stop();
        }
    }
    
    static async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];
            
            try
            {
                while (true)
                {
                    // Odbieranie danych od klienta
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break; // Klient się rozłączył
                    
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"Odebrano: {message}");
                    
                    // Odesłanie odpowiedzi
                    string response = $"Serwer otrzymał: {message}";
                    byte[] responseData = Encoding.UTF8.GetBytes(response);
                    await stream.WriteAsync(responseData, 0, responseData.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas obsługi klienta: {ex.Message}");
            }
            
            Console.WriteLine("Klient rozłączony");
        }
    }
}