namespace server.Models;

public interface IChessClient
{
    Task Ping(string message);
}