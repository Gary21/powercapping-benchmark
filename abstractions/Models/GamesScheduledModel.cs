using SQLite;

namespace server.Models;

public class GamesScheduledModel
{
    [PrimaryKey]
    public string Id { get; set; }
    public int TimeControl { get; set; }
    public int TimeIncrement { get; set; }
    public int PowerCap { get; set; }
    public int NumberOfGamesPerSide { get; set; }
}