using System.Collections.Generic;

namespace MuaythaiApp;

public class BracketReportDefinition
{
    public string Name { get; set; } = "";
    public string Summary { get; set; } = "";
    public List<BracketCategoryReport> Categories { get; set; } = new();
}

public class BracketCategoryReport
{
    public string Title { get; set; } = "";
    public int FighterCount { get; set; }
    public int LeafCount { get; set; }
    public List<BracketRoundReport> Rounds { get; set; } = new();
}

public class BracketRoundReport
{
    public string Title { get; set; } = "";
    public List<BracketMatchReport> Matches { get; set; } = new();
}

public class BracketMatchReport
{
    public int MatchNumber { get; set; }
    public string TopPrimaryText { get; set; } = "";
    public string TopSecondaryText { get; set; } = "";
    public string BottomPrimaryText { get; set; } = "";
    public string BottomSecondaryText { get; set; } = "";
    public string TopSourceLabel { get; set; } = "";
    public string BottomSourceLabel { get; set; } = "";
    public int StartSlot { get; set; }
    public int Span { get; set; }
    public int TopStartSlot { get; set; }
    public int TopSpan { get; set; }
    public int BottomStartSlot { get; set; }
    public int BottomSpan { get; set; }
}
