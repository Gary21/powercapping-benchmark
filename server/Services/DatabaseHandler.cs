using server.Models;
using SQLite;

namespace server.Services;

public class DatabaseHandler : IDisposable
{
    private const string DatabasePath = "benchmark.db";
    private readonly SQLiteConnection _db;

    public DatabaseHandler() : this(DatabasePath) { }

    public DatabaseHandler(string databasePath) {

        _db = new SQLiteConnection(databasePath);
        _db.CreateTable<FinishedGameModel>();
        _db.CreateTable<GamesScheduledModel>();
        // CreateTable adds missing columns as NULL, so rows written before limit modes existed need the mode set explicitly
        _db.Execute("UPDATE FinishedGameModel SET LimitMode = ? WHERE LimitMode IS NULL", LimitModes.TimeControl);
        _db.Execute("UPDATE GamesScheduledModel SET LimitMode = ? WHERE LimitMode IS NULL", LimitModes.TimeControl);
    }

    public void Dispose() => _db.Dispose();

    public void InsertResult(FinishedGameModel result)
    {
        _db.Insert(result);
    }

    public void ScheduleGames(GamesScheduledModel newScheduledGames)
    {
        // compared in memory: the table is tiny and `==` on nullable columns is unambiguous (NULL only equals NULL)
        var existing = _db.Table<GamesScheduledModel>().ToList().FirstOrDefault(r =>
            (r.LimitMode ?? LimitModes.TimeControl) == newScheduledGames.LimitMode &&
            r.TimeControl == newScheduledGames.TimeControl &&
            r.TimeIncrement == newScheduledGames.TimeIncrement &&
            r.TimePerMove == newScheduledGames.TimePerMove &&
            r.NodesPerMove == newScheduledGames.NodesPerMove &&
            r.PowerCap == newScheduledGames.PowerCap &&
            r.IsGpu == newScheduledGames.IsGpu &&
            r.InitialMoves == newScheduledGames.InitialMoves);
        if (existing != null)
        {
            existing.NumberOfGamesPerSide += newScheduledGames.NumberOfGamesPerSide;
            _db.Update(existing);
            return;
        }

        _db.Insert(newScheduledGames);
    }

    public List<GamesScheduledModel> GetScheduledGames()
    {
        return _db.Table<GamesScheduledModel>().ToList();
    }

    public void RemoveScheduledGame(GamesScheduledModel game)
    {
        var existing = _db.Find<GamesScheduledModel>(game.Id);
        if (existing == null) return;
        if (existing.NumberOfGamesPerSide > 1)
        {
            existing.NumberOfGamesPerSide -= 1;
            _db.Update(existing);
        }
        else
        {
            _db.Delete(existing);
        }
    }
}
