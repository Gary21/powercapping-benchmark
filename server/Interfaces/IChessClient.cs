namespace server.Models;

public interface IChessClient
{
    Task Ping(string message);
    Task GameStarted(NewGameModel newGameState, bool isWhite);
    Task MoveMade(MoveMadeModel moveMadeModel);
    Task GameFinished();
}