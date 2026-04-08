namespace MuaythaiApp;

public class FightResult
{
    public int MatchId { get; set; }
    public int BoutNo { get; set; }
    public int DayNumber { get; set; } = 1;
    public string RedName { get; set; } = "";
    public string BlueName { get; set; } = "";
    public string WinnerSide { get; set; } = "";
    public string WinnerName { get; set; } = "";
    public string Category { get; set; } = "";
    public string WeightClass { get; set; } = "";
    public string Method { get; set; } = "";
    public int Round { get; set; }
}
