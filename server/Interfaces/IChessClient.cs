namespace server.Models;

public interface IChessClient
{
    Task Ping(string message);
    Task GameStarted(GameStateModel gameState);
}