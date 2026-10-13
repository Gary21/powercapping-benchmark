namespace server.Models;

public class NewGameModel
{
    public string GameId { get;set; }
    public string Engine { get; set; }
    public int PowerCap { get; set; } // POWER CAP IS PERCENTAGE VALUE POWER = (MAX WATTS - MIN WATTS) * (POWER CAP / 100)) + MIN WATTS
    public string LimitMode { get; set; } = LimitModes.TimeControl;
    public long TimeControl { get; set; } // MS, 0 when the mode has no clock
    public long TimeIncrement { get; set; } // MS, 0 when the mode has no clock
    public int? TimePerMove { get; set; } // MS
    public long? NodesPerMove { get; set; }
    public string InitialMoves { get; set; }
    public string PowerCapColor { get; set; }
    public bool IsGpu { get; set; }
}