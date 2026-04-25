namespace MuaythaiApp;

 public class Match
{
    public int Id { get; set; }

    public int Fighter1Id { get; set; }
    public int Fighter2Id { get; set; }

    public string Fighter1Name { get; set; } = "";
    public string Fighter2Name { get; set; } = "";

    public string AgeCategory { get; set; } = "";
    public string WeightCategory { get; set; } = "";
    public string Gender { get; set; } = "";

    public int OrderNo { get; set; }

    public string CategoryGroup { get; set; } = "";

    public int JudgesCount { get; set; }

    public int DayNumber { get; set; } = 1;
    public string RingName { get; set; } = "RING A";

    public string DayLabel => $"Day {DayNumber}";



    public override string ToString()
{
    return
        DayLabel
        + " | "
        + RingName
        + " | "
        +
        CategoryGroup
        + " | "
        + OrderNo
        + " | "
        + Fighter1Name
        + " vs "
        + Fighter2Name;
}
}
