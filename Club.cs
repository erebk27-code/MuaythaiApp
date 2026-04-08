namespace MuaythaiApp;

public class Club
{
    public int Id { get; set; }

    public string? Name { get; set; }
    public string? Coach { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }

    public override string ToString()
    {
        return Name!;
    }
}
