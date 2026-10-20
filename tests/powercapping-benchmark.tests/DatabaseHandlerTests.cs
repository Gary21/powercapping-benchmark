using server.Models;
using server.Services;
using SQLite;
using Xunit;

namespace powercapping_benchmark.tests;

public class DatabaseHandlerTests : IDisposable
{
    private readonly List<string> _paths = [];

    private string NewDbPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"powercapping-test-{Guid.NewGuid():N}.db");
        _paths.Add(path);
        return path;
    }

    // every handler/connection is disposed by the tests before this runs, so the files can be deleted
    public void Dispose()
    {
        foreach (var path in _paths)
            File.Delete(path);
    }

    private static GamesScheduledModel Row(string id, string mode, int? tc = null, int? ti = null, int? tpm = null, long? npm = null, int games = 1)
        => new()
        {
            Id = id, LimitMode = mode, TimeControl = tc, TimeIncrement = ti, TimePerMove = tpm, NodesPerMove = npm,
            PowerCap = 80, NumberOfGamesPerSide = games, InitialMoves = "e2e4 e7e5"
        };

    [Fact]
    public void Identical_requests_are_merged_and_different_modes_are_not()
    {
        using var db = new DatabaseHandler(NewDbPath());

        db.ScheduleGames(Row("a", LimitModes.TimePerMove, tpm: 500, games: 1));
        db.ScheduleGames(Row("b", LimitModes.TimePerMove, tpm: 500, games: 3));
        db.ScheduleGames(Row("c", LimitModes.TimePerMove, tpm: 1000));
        db.ScheduleGames(Row("d", LimitModes.NodesPerMove, npm: 500));
        db.ScheduleGames(Row("e", LimitModes.TimeControl, tc: 60000, ti: 1000));

        var rows = db.GetScheduledGames();
        Assert.Equal(4, rows.Count);
        Assert.Equal(4, rows.Single(r => r.Id == "a").NumberOfGamesPerSide);
    }

    [Fact]
    public void Removing_a_played_game_targets_the_exact_row()
    {
        using var db = new DatabaseHandler(NewDbPath());
        // same PowerCap and NULL TimeControl/TimeIncrement: only the Id tells these rows apart
        db.ScheduleGames(Row("slow", LimitModes.TimePerMove, tpm: 1000, games: 1));
        db.ScheduleGames(Row("fast", LimitModes.TimePerMove, tpm: 500, games: 2));
        db.ScheduleGames(Row("nodes", LimitModes.NodesPerMove, npm: 500, games: 2));

        db.RemoveScheduledGame(db.GetScheduledGames().Single(r => r.Id == "slow"));
        db.RemoveScheduledGame(db.GetScheduledGames().Single(r => r.Id == "nodes"));

        var rows = db.GetScheduledGames().ToDictionary(r => r.Id, r => r.NumberOfGamesPerSide);
        Assert.False(rows.ContainsKey("slow"));
        Assert.Equal(2, rows["fast"]);
        Assert.Equal(1, rows["nodes"]);
    }

    [Fact]
    public void Old_database_is_migrated_without_losing_data_and_accepts_non_clock_rows()
    {
        var path = NewDbPath();
        using (var old = new SQLiteConnection(path))
        {
            old.CreateTable<Old.FinishedGameModel>();
            old.CreateTable<Old.GamesScheduledModel>();
            old.Insert(new Old.FinishedGameModel { GameId = "g1", TimeControl = 60000, TimeIncrement = 1000, WhiteTimeLeft = 1234, BlackTimeLeft = 99, Result = "w" });
            old.Insert(new Old.GamesScheduledModel { Id = "s1", TimeControl = 60000, TimeIncrement = 1000, PowerCap = 80, NumberOfGamesPerSide = 2, InitialMoves = "e2e4 e7e5" });
        }

        using (var db = new DatabaseHandler(path))
        {
            var oldRow = Assert.Single(db.GetScheduledGames());
            Assert.Equal(LimitModes.TimeControl, oldRow.LimitMode);
            Assert.Equal(60000, oldRow.TimeControl);
            Assert.Equal(2, oldRow.NumberOfGamesPerSide);
            Assert.Null(oldRow.TimePerMove);
            Assert.Null(oldRow.NodesPerMove);

            // NULLs in the old (formerly non-nullable) columns must be accepted by the upgraded tables
            db.ScheduleGames(Row("s2", LimitModes.NodesPerMove, npm: 5_000_000_000L));
            db.InsertResult(new FinishedGameModel { GameId = "g2", LimitMode = LimitModes.NodesPerMove, NodesPerMove = 5_000_000_000L, Result = "d" });
        }

        using (var check = new SQLiteConnection(path))
        {
            var finished = check.Table<FinishedGameModel>().ToList();
            Assert.Equal(2, finished.Count);
            var g1 = finished.Single(r => r.GameId == "g1");
            Assert.Equal(LimitModes.TimeControl, g1.LimitMode);
            Assert.Equal(1234, g1.WhiteTimeLeft);
            var g2 = finished.Single(r => r.GameId == "g2");
            Assert.Null(g2.TimeControl);
            Assert.Null(g2.WhiteTimeLeft);
            Assert.Equal(5_000_000_000L, g2.NodesPerMove);
        }

        // a second start changes nothing
        using var again = new DatabaseHandler(path);
        Assert.Equal(2, again.GetScheduledGames().Count);
    }

    // the PowerCap column is INTEGER in databases created before the field became a double; SQLite keeps a REAL value in it as is
    [Fact]
    public void Fractional_power_cap_round_trips_through_an_old_integer_column()
    {
        var path = NewDbPath();
        using (var old = new SQLiteConnection(path))
        {
            old.CreateTable<Old.FinishedGameModel>();
            old.CreateTable<Old.GamesScheduledModel>();
            old.Insert(new Old.GamesScheduledModel { Id = "s1", TimeControl = 60000, TimeIncrement = 1000, PowerCap = 80, NumberOfGamesPerSide = 1, InitialMoves = "e2e4 e7e5" });
        }

        using (var db = new DatabaseHandler(path))
        {
            var half = Row("s2", LimitModes.NodesPerMove, npm: 1_000_000L);
            half.PowerCap = 0.5;
            db.ScheduleGames(half);
            var sameAgain = Row("s3", LimitModes.NodesPerMove, npm: 1_000_000L);
            sameAgain.PowerCap = 0.5;
            db.ScheduleGames(sameAgain); // identical settings are merged, so fractional caps compare equal
            db.InsertResult(new FinishedGameModel { GameId = "g1", LimitMode = LimitModes.NodesPerMove, NodesPerMove = 1_000_000L, PowerCap = 0.25, Result = "d" });
        }

        using var check = new DatabaseHandler(path);
        var rows = check.GetScheduledGames();
        Assert.Equal(2, rows.Count);
        Assert.Equal(80, rows.Single(r => r.Id == "s1").PowerCap); // an integer written by the old schema still reads back
        var merged = rows.Single(r => r.Id == "s2");
        Assert.Equal(0.5, merged.PowerCap);
        Assert.Equal(2, merged.NumberOfGamesPerSide);
    }
}
