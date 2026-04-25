using System.Collections.Generic;

namespace MuaythaiApp;

public class MatchScheduleReportDefinition
{
    public string Name { get; set; } = "";
    public string Summary { get; set; } = "";
    public int DayNumber { get; set; }
    public string ReportDate { get; set; } = "";
    public string RingName { get; set; } = "";
    public List<MatchScheduleRow> Rows { get; set; } = new();
}

public class MatchScheduleRow
{
    public int OrderNumber { get; set; }
    public string Stage { get; set; } = "";
    public string AgeCategory { get; set; } = "";
    public string WeightCategory { get; set; } = "";
    public string RedName { get; set; } = "";
    public string RedClub { get; set; } = "";
    public string BlueName { get; set; } = "";
    public string BlueClub { get; set; } = "";
    public string Result { get; set; } = "";
}
