namespace server.Models;

public class NewGameModel
{
    public string GameId { get;set; }
    public string Engine { get; set; }
    public int PowerCap { get; set; } // POWER CAP IS PERCENTAGE VALUE POWER = (MAX WATTS - MIN WATTS) * (POWER CAP / 100)) + MIN WATTS
    public long TimeControl { get; set; } // MS
    public long TimeIncrement { get; set; } // MS
    public string InitialMoves { get; set; }
    public string PowerCapColor { get; set; }
}