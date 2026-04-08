using System.Collections.Generic;

namespace MuaythaiApp;

public class ReportDefinition
{
    public string Name { get; set; } = "";
    public string Summary { get; set; } = "";
    public List<string> Columns { get; set; } = new();
    public List<ReportRow> Rows { get; set; } = new();
}
