using SQLite;

namespace benchmark_server;

public class DatabaseHandler
{
    private const string DatabasePath = "benchmark.db";
    private readonly SQLiteConnection _db;
    
    public DatabaseHandler() {
        
        _db = new SQLiteConnection(DatabasePath);
        _db.CreateTable<ExperimentResult>();
    }
    
    public void UpsertResult(ExperimentResult result)
    {
        var existing = _db.Table<ExperimentResult>().FirstOrDefault(r => r.Engine == result.Engine && r.PowerCap == result.PowerCap);
        if (existing != null)
        {
            existing.White += result.White;
            existing.Black += result.Black;
            existing.Draws += result.Draws;
            _db.Update(existing);
            return;
        }
        _db.Insert(result);
    }
    
    public List<ExperimentResult> GetAllResults()
    {
        return _db.Table<ExperimentResult>().ToList();
    }
}