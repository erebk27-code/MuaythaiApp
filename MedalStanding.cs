namespace MuaythaiApp;

public class MedalStanding
{
    public string ClubName { get; set; } = "";
    public int Gold { get; set; }
    public int Silver { get; set; }
    public int Bronze { get; set; }
    public int Total => Gold + Silver + Bronze;
}
