namespace MuaythaiApp;

public class Fighter
{
    public int Id { get; set; }

    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";

    public int Age { get; set; }
    public int BirthYear { get; set; }

    public double Weight { get; set; }

    public string Gender { get; set; } = "";
    
    public int ClubId { get; set; }
    
    public string ClubName { get; set; } = "";

    public string AgeCategory { get; set; } = "";
    public string WeightCategory { get; set; } = "";

    public string FullName =>
        $"{FirstName} {LastName}".Trim();

    public string CategoryDisplay =>
        string.IsNullOrWhiteSpace(AgeCategory) && string.IsNullOrWhiteSpace(WeightCategory)
            ? "-"
            : $"{AgeCategory} {WeightCategory}".Trim();

    public string WeightClassDisplay =>
        string.IsNullOrWhiteSpace(WeightCategory)
            ? "-"
            : WeightCategory;


    public override string ToString()
    {
        return (FirstName ?? "")
            + " "
            + (LastName ?? "")
            + " - "
            + (ClubName ?? "");
    }
}
