namespace server.Models;

public class FinishedGameModel
{
    public string GameId { get;set; }
    public string Engine { get; set; }
    public int PowerCap { get; set; }
    public int TimeControl { get; set; }
    public int TimeIncrement { get; set; }
    public string InitialPosition { get; set; }
    public string PowerCapColor { get; set; }
    public string Result { get; set; }
}