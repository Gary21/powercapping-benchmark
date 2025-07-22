using System.Runtime.InteropServices;
using SQLite;

namespace benchmark_server;

[Table("ExperimentResults")]
public class ExperimentResult
{
    [PrimaryKey, AutoIncrement]
    [Column("id")]		
    public int Id { get; set; }	
    
    [Column("engine"), Indexed]
    public string Engine { get; set; }
    
    [Column("powercap"), Indexed]
    public int PowerCap { get; set; }
    
    [Column("white")]
    public int White { get; set; }
    
    [Column("black")]
    public int Black { get; set; }
    
    [Column("draws")]
    public int Draws { get; set; }

    public ExperimentResult()
    {
        Engine = "";
        PowerCap = 0;
        White = 0;
        Black = 0;
        Draws = 0;
    }
    
    public ExperimentResult(string engine, int powerCap)
    {
        Engine = engine;
        PowerCap = powerCap;
        White = 0;
        Black = 0;
        Draws = 0;
    }
    
    public ExperimentResult(string engine, int powerCap, int white=0, int black=0, int draws=0)
    {
        Engine = engine;
        PowerCap = powerCap;
        White = white;
        Black = black;
        Draws = draws;
    }
}