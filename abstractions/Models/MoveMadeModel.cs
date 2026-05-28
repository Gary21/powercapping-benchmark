namespace server.Models;

public class MoveMadeModel
{
    public string GameId { get; set; }
    public string CurrentFen { get; set; }
    public long WhiteTimeLeft { get; set; }
    public long BlackTimeLeft { get; set; }
    public long Increment { get; set; }
    
    public MoveMadeModel() { }
    public MoveMadeModel(NewGameModel gameState)
    {
        GameId = gameState.GameId;
        CurrentFen = gameState.InitialPosition;
        WhiteTimeLeft = gameState.TimeControl;
        BlackTimeLeft = gameState.TimeControl;
        Increment = gameState.TimeIncrement;
    }
}