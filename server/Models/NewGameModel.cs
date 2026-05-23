namespace server.Models;

public class NewGameModel
{
    public string GameId { get;set; }
    public string Engine { get; set; }
    public int PowerCap { get; set; } // POWER CAP IS PERCENTAGE VALUE POWER = (MAX WATTS - MIN WATTS) * (POWER CAP / 100)) + MIN WATTS
    public int TimeControl { get; set; }
    public int TimeIncrement { get; set; }
    public string InitialPosition { get; set; }
    public string PowerCapColor { get; set; }
}