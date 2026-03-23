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
    
    [Column("nocapwins")]
    public int NocapWins { get; set; }
    
    [Column("powercapwins")]
    public int PowercapWins { get; set; }
    
    [Column("draws")]
    public int Draws { get; set; }

    public ExperimentResult()
    {
        Engine = "";
        PowerCap = 0;
        NocapWins = 0;
        PowercapWins = 0;
        Draws = 0;
    }
    
    public ExperimentResult(string engine, int powerCap)
    {
        Engine = engine;
        PowerCap = powerCap;
        NocapWins = 0;
        PowercapWins = 0;
        Draws = 0;
    }
    
    public ExperimentResult(string engine, int powerCap, int nocapWins=0, int powercapWins=0, int draws=0)
    {
        Engine = engine;
        PowerCap = powerCap;
        NocapWins = nocapWins;
        PowercapWins = powercapWins;
        Draws = draws;
    }
}