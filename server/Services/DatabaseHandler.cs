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
    }
    
    public void InsertResult(FinishedGameModel result)
    {
        _db.Insert(result);
    }
    
    public List<FinishedGameModel> GetAllResults()
    {
        return _db.Table<FinishedGameModel>().ToList();
    }
}