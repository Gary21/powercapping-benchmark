using server.Models;
using SQLite;

namespace server.Services;

public class DatabaseHandler
{
    private const string DatabasePath = "benchmark.db";
    private readonly SQLiteConnection _db;
    
    public DatabaseHandler() {
        
        _db = new SQLiteConnection(DatabasePath);
        _db.CreateTable<FinishedGameModel>();
        _db.CreateTable<GamesScheduledModel>();
    }
    
    public void InsertResult(FinishedGameModel result)
    {
        _db.Insert(result);
    }
    
    public void ScheduleGames(GamesScheduledModel newScheduledGames)
    {
        var existing = _db.Table<GamesScheduledModel>().FirstOrDefault(r =>
            r.TimeControl == newScheduledGames.TimeControl &&
            r.TimeIncrement == newScheduledGames.TimeIncrement &&
            r.PowerCap == newScheduledGames.PowerCap);
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
        var existing = _db.Table<GamesScheduledModel>().FirstOrDefault(r =>
            r.TimeControl == game.TimeControl &&
            r.TimeIncrement == game.TimeIncrement &&
            r.PowerCap == game.PowerCap);
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