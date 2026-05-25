namespace server.Models;

public class MoveMadeModel
{
    public string GameId { get; set; }
    public string Move { get; set; }
    public float WhiteTimeLeft { get; set; }
    public float BlackTimeLeft { get; set; }
}