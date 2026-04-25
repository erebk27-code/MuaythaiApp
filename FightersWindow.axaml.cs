using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Data.Sqlite;
using MuaythaiApp.Security;
using System;
using System.Data;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MuaythaiApp.Database;

namespace MuaythaiApp;

public partial class FightersWindow : Window
{
    private readonly List<Fighter> allFighters = new();
    private readonly List<Club> allClubs = new();
    private readonly List<Category> allCategories = new();
    private DatabaseAutoRefresh? databaseAutoRefresh;
    private Fighter? selectedFighter;

    public FightersWindow()
    {
        InitializeComponent();
        StartupLogger.Log("FightersWindow constructor started");

        if (!AppSession.IsAdmin)
        {
            StartupLogger.Log("FightersWindow closing because session is not admin");
            Opened += (_, __) => Close();
            return;
        }

        LoadGender();
        LoadSortOptions();
        LocalizationService.LocalizeControlTree(this);
        Opened += async (_, __) => await LoadInitialDataAsync();
        Opened += (_, __) => StartupLogger.Log("FightersWindow opened");
        StartupLogger.Log("FightersWindow constructor completed");
    }

    private void ReloadDatabaseData()
    {
        LoadClubs();
        LoadCategories();
        LoadFighters();
    }

    private async Task LoadInitialDataAsync()
    {
        StartupLogger.Log("FightersWindow.LoadInitialDataAsync started");
        FormStatusText.Text = "Loading fighters...";

        try
        {
            var result = await Task.Run(() => new FighterWindowLoadResult(
                LoadClubsCore(),
                LoadCategoriesCore(),
                LoadFightersCore()));

            allClubs.Clear();
            allClubs.AddRange(result.Clubs);
            ClubCombo.ItemsSource = null;
            ClubCombo.ItemsSource = allClubs;

            allCategories.Clear();
            allCategories.AddRange(result.Categories);

            allFighters.Clear();
            allFighters.AddRange(result.Fighters);
            ApplyFiltersAndSorting();
            databaseAutoRefresh ??= new DatabaseAutoRefresh(this, ReloadDatabaseData);
            FormStatusText.Text = string.Empty;
            StartupLogger.Log($"FightersWindow.LoadInitialDataAsync completed with {result.Fighters.Count} fighters");
        }
        catch (Exception ex)
        {
            FormStatusText.Text = $"Fighters could not be loaded: {ex.Message}";
            StartupLogger.Log(ex, "FightersWindow.LoadInitialDataAsync failed");
        }
    }

    private void AddClick(object? sender, RoutedEventArgs e)
    {
        if (!TryBuildFormData(out var formData))
            return;

        if (RemoteApiClient.IsEnabled)
        {
            RemoteApiClient.CreateFighter(MapToFighter(formData));
            FormStatusText.Text = "Fighter added.";
            ClearFormInternal();
            LoadFighters();
            return;
        }

        using var c =
            DatabaseHelper.CreateConnection();

        c.Open();

        var cmd = c.CreateCommand();
        cmd.CommandText =
        @"
        INSERT INTO Fighters
        (
            FirstName,
            LastName,
            Weight,
            Age,
            ClubId,
            AgeCategory,
            WeightCategory,
            BirthYear,
            Gender
        )
        VALUES
        (
            @fn,
            @ln,
            @w,
            @a,
            @c,
            @ac,
            @wc,
            @by,
            @g
        )
        ";

        FillCommonParameters(cmd, formData);
        cmd.ExecuteNonQuery();

        FormStatusText.Text = "Fighter added.";
        ClearFormInternal();
        LoadFighters();
    }

    private void UpdateClick(object? sender, RoutedEventArgs e)
    {
        var fighterToUpdate = FightersGrid.SelectedItem as Fighter ?? selectedFighter;

        if (fighterToUpdate == null)
        {
            FormStatusText.Text = "Select a fighter to update.";
            return;
        }

        if (!TryBuildFormData(out var formData))
            return;

        if (RemoteApiClient.IsEnabled)
        {
            RemoteApiClient.UpdateFighter(MapToFighter(formData, fighterToUpdate.Id));
            FormStatusText.Text = "Fighter updated.";
            ClearFormInternal();
            LoadFighters();
            return;
        }

        using var c =
            DatabaseHelper.CreateConnection();

        c.Open();

        var cmd = c.CreateCommand();
        cmd.CommandText =
        @"
        UPDATE Fighters
        SET
            FirstName = @fn,
            LastName = @ln,
            Weight = @w,
            Age = @a,
            ClubId = @c,
            AgeCategory = @ac,
            WeightCategory = @wc,
            BirthYear = @by,
            Gender = @g
        WHERE Id = @id
        ";

        FillCommonParameters(cmd, formData);
        cmd.Parameters.AddWithValue("@id", fighterToUpdate.Id);
        cmd.ExecuteNonQuery();

        FormStatusText.Text = "Fighter updated.";
        ClearFormInternal();
        LoadFighters();
    }

    private void DeleteClick(object? sender, RoutedEventArgs e)
    {
        var fighter = FightersGrid.SelectedItem as Fighter ?? selectedFighter;

        if (fighter == null)
        {
            FormStatusText.Text = "Select a fighter to delete.";
            return;
        }

        if (RemoteApiClient.IsEnabled)
        {
            RemoteApiClient.DeleteFighter(fighter.Id);
            FormStatusText.Text = "Fighter deleted.";
            ClearFormInternal();
            LoadFighters();
            return;
        }

        using var c =
            DatabaseHelper.CreateConnection();

        c.Open();

        var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM Fighters WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", fighter.Id);
        cmd.ExecuteNonQuery();

        FormStatusText.Text = "Fighter deleted.";
        ClearFormInternal();
        LoadFighters();
    }

    private void ClearClick(object? sender, RoutedEventArgs e)
    {
        ClearFormInternal();
        FormStatusText.Text = "Form cleared.";
    }

    private void LoadFighters()
    {
        allFighters.Clear();
        allFighters.AddRange(LoadFightersCore());
        ApplyFiltersAndSorting();
    }

    private List<Fighter> LoadFightersCore()
    {
        var fighters = new List<Fighter>();

        if (RemoteApiClient.IsEnabled)
        {
            fighters.AddRange(RemoteApiClient.GetFighters());
            return fighters;
        }

        using var c =
            DatabaseHelper.CreateConnection();

        c.Open();

        var cmd = c.CreateCommand();

        cmd.CommandText =
        @"
        SELECT
            Fighters.Id,
            FirstName,
            LastName,
            Weight,
            Age,
            Fighters.ClubId,
            AgeCategory,
            WeightCategory,
            BirthYear,
            Gender,
            Clubs.Name as ClubName
        FROM Fighters
        LEFT JOIN Clubs
            ON Fighters.ClubId = Clubs.Id
        ";

        using var r = cmd.ExecuteReader();

        while (r.Read())
        {
            var f = new Fighter();

            f.Id =
                r["Id"] != DBNull.Value
                ? Convert.ToInt32(r["Id"])
                : 0;

            f.FirstName = r["FirstName"]?.ToString() ?? "";
            f.LastName = r["LastName"]?.ToString() ?? "";
            f.Weight =
                r["Weight"] != DBNull.Value
                ? Convert.ToDouble(r["Weight"])
                : 0;
            f.Age =
                r["Age"] != DBNull.Value
                ? Convert.ToInt32(r["Age"])
                : 0;
            f.ClubId =
                r["ClubId"] != DBNull.Value
                ? Convert.ToInt32(r["ClubId"])
                : 0;
            f.AgeCategory = r["AgeCategory"]?.ToString() ?? "";
            f.WeightCategory = r["WeightCategory"]?.ToString() ?? "";
            f.BirthYear =
                r["BirthYear"] != DBNull.Value
                ? Convert.ToInt32(r["BirthYear"])
                : 0;
            f.Gender = r["Gender"]?.ToString() ?? "";
            f.ClubName = r["ClubName"]?.ToString() ?? "";

            fighters.Add(f);
        }

        return fighters;
    }

    private void LoadGender()
    {
        GenderCombo.ItemsSource =
            new List<string>
            {
                "Male",
                "Female"
            };
    }

    private void LoadCategories()
    {
        allCategories.Clear();
        allCategories.AddRange(LoadCategoriesCore());
    }

    private List<Category> LoadCategoriesCore()
    {
        var categories = new List<Category>();

        if (RemoteApiClient.IsEnabled)
        {
            return ChampionshipSettingsService.FilterActiveCategories(RemoteApiClient.GetCategories())
                .OrderBy(x => x.AgeMin)
                .ThenBy(x => x.AgeMax)
                .ThenBy(x => x.SortOrder)
                .ToList();
        }

        using var c = DatabaseHelper.CreateConnection();
        c.Open();

        var cmd = c.CreateCommand();
        cmd.CommandText =
        @"
        SELECT
            Id,
            Division,
            Gender,
            AgeMin,
            AgeMax,
            WeightMax,
            IsOpenWeight,
            SortOrder,
            CategoryName,
            RoundCount,
            RoundDurationSeconds,
            BreakDurationSeconds
        FROM Categories
        ORDER BY AgeMin, AgeMax, SortOrder
        ";

        using var r = cmd.ExecuteReader();

        while (r.Read())
        {
            categories.Add(new Category
            {
                Id = r.GetInt32(0),
                Division = r.GetString(1),
                Gender = r.GetString(2),
                AgeMin = r.GetInt32(3),
                AgeMax = r.GetInt32(4),
                WeightMax = r.GetDouble(5),
                IsOpenWeight = r.GetInt32(6) == 1,
                SortOrder = r.GetInt32(7),
                CategoryName = r.GetString(8),
                RoundCount = r.GetInt32(9),
                RoundDurationSeconds = r.GetInt32(10),
                BreakDurationSeconds = r.GetInt32(11)
            });
        }

        return ChampionshipSettingsService.FilterActiveCategories(categories)
            .OrderBy(x => x.AgeMin)
            .ThenBy(x => x.AgeMax)
            .ThenBy(x => x.SortOrder)
            .ToList();
    }

    private void LoadSortOptions()
    {
        SortCombo.ItemsSource =
            new List<string>
            {
                "Category",
                "Name",
                "Club",
                "Gender",
                "Weight"
            };

        SortCombo.SelectedIndex = 0;
    }

    private void LoadClubs()
    {
        allClubs.Clear();
        allClubs.AddRange(LoadClubsCore());
        ClubCombo.ItemsSource = null;
        ClubCombo.ItemsSource = allClubs;
    }

    private List<Club> LoadClubsCore()
    {
        var clubs = new List<Club>();

        if (RemoteApiClient.IsEnabled)
        {
            clubs.AddRange(RemoteApiClient.GetClubs().OrderBy(x => x.Name));
            return clubs;
        }

        using var c =
            DatabaseHelper.CreateConnection();

        c.Open();

        var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT Id, Name FROM Clubs ORDER BY Name";

        using var r = cmd.ExecuteReader();

        while (r.Read())
        {
            clubs.Add(new Club
            {
                Id = r.GetInt32(0),
                Name = r.IsDBNull(1) ? string.Empty : r.GetString(1)
            });
        }

        return clubs;
    }

    private void BirthYearChanged(object? sender, TextChangedEventArgs e)
    {
        if (int.TryParse(BirthYearBox.Text, out var birthYear))
        {
            var age = DateTime.Now.Year - birthYear;
            AgeBox.Text = age >= 0 ? age.ToString() : string.Empty;
        }
        else
        {
            AgeBox.Text = string.Empty;
        }

        UpdateAutoCategory();
    }

    private void WeightChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateAutoCategory();
    }

    private void GenderChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateAutoCategory();
    }

    private void SearchChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFiltersAndSorting();
    }

    private void SortChanged(object? sender, SelectionChangedEventArgs e)
    {
        ApplyFiltersAndSorting();
    }

    private void FighterSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        selectedFighter = FightersGrid.SelectedItem as Fighter;

        if (selectedFighter == null)
            return;

        FirstNameBox.Text = selectedFighter.FirstName;
        LastNameBox.Text = selectedFighter.LastName;
        BirthYearBox.Text = selectedFighter.BirthYear > 0
            ? selectedFighter.BirthYear.ToString()
            : string.Empty;
        AgeBox.Text = selectedFighter.Age > 0
            ? selectedFighter.Age.ToString()
            : string.Empty;
        WeightBox.Text = selectedFighter.Weight.ToString("0.##", CultureInfo.InvariantCulture);
        GenderCombo.SelectedItem = selectedFighter.Gender;
        ClubCombo.SelectedItem = allClubs.FirstOrDefault(x => x.Id == selectedFighter.ClubId);
        CategoryBox.Text = selectedFighter.AgeCategory;
        WeightClassBox.Text = selectedFighter.WeightCategory;

        FormStatusText.Text = $"Selected fighter #{selectedFighter.Id}";
    }

    private void ApplyFiltersAndSorting()
    {
        IEnumerable<Fighter> query = allFighters;
        var searchText = SearchBox.Text?.Trim().ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = query.Where(x =>
                x.FullName.ToLowerInvariant().Contains(searchText) ||
                x.FirstName.ToLowerInvariant().Contains(searchText) ||
                x.LastName.ToLowerInvariant().Contains(searchText) ||
                x.ClubName.ToLowerInvariant().Contains(searchText) ||
                x.Gender.ToLowerInvariant().Contains(searchText) ||
                x.AgeCategory.ToLowerInvariant().Contains(searchText) ||
                x.WeightCategory.ToLowerInvariant().Contains(searchText) ||
                x.Weight.ToString("0.##", CultureInfo.InvariantCulture).Contains(searchText) ||
                x.Id.ToString(CultureInfo.InvariantCulture).Contains(searchText));
        }

        query = (SortCombo.SelectedItem?.ToString() ?? "Name") switch
        {
            "Club" => query.OrderBy(x => x.ClubName).ThenBy(x => x.LastName).ThenBy(x => x.FirstName),
            "Gender" => query.OrderBy(x => x.Gender).ThenBy(x => x.LastName).ThenBy(x => x.FirstName),
            "Category" => query.OrderBy(x => x.AgeCategory).ThenBy(x => x.WeightCategory).ThenBy(x => x.LastName),
            "Weight" => query.OrderBy(x => x.Weight).ThenBy(x => x.LastName).ThenBy(x => x.FirstName),
            _ => query.OrderBy(x => x.LastName).ThenBy(x => x.FirstName)
        };

        var result = query.ToList();

        FightersGrid.ItemsSource = null;
        FightersGrid.ItemsSource = result;
        ListInfoText.Text = $"{result.Count} fighters listed";
    }

    private bool TryBuildFormData(out FighterFormData formData)
    {
        formData = new FighterFormData();

        var firstName = FirstNameBox.Text?.Trim() ?? "";
        var lastName = LastNameBox.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            FormStatusText.Text = "First name and last name are required.";
            return false;
        }

        if (ClubCombo.SelectedItem is not Club club)
        {
            FormStatusText.Text = "Please select a club.";
            return false;
        }

        if (GenderCombo.SelectedItem == null)
        {
            FormStatusText.Text = "Please select a gender.";
            return false;
        }

        if (!int.TryParse(BirthYearBox.Text, out var birthYear))
        {
            FormStatusText.Text = "Birth year must be numeric.";
            return false;
        }

        var age = DateTime.Now.Year - birthYear;

        if (age < 0)
        {
            FormStatusText.Text = "Birth year is not valid.";
            return false;
        }

        if (!double.TryParse(
                WeightBox.Text,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var weight))
        {
            if (!double.TryParse(WeightBox.Text, out weight))
            {
                FormStatusText.Text = "Weight must be numeric.";
                return false;
            }
        }

        formData.FirstName = firstName;
        formData.LastName = lastName;
        formData.ClubId = club.Id;
        formData.BirthYear = birthYear;
        formData.Age = age;
        formData.Weight = weight;
        formData.Gender = GenderCombo.SelectedItem?.ToString() ?? "";

        if (!TryResolveCategory(age, weight, formData.Gender, out var matchedCategory))
        {
            FormStatusText.Text = "Category and weight class could not be calculated.";
            return false;
        }

        formData.AgeCategory = matchedCategory.Division;
        formData.WeightCategory = matchedCategory.CategoryName;
        CategoryBox.Text = formData.AgeCategory;
        WeightClassBox.Text = formData.WeightCategory;

        if (string.IsNullOrWhiteSpace(formData.AgeCategory) ||
            string.IsNullOrWhiteSpace(formData.WeightCategory))
        {
            FormStatusText.Text = "Category and weight class could not be calculated.";
            return false;
        }

        return true;
    }

    private sealed record FighterWindowLoadResult(
        List<Club> Clubs,
        List<Category> Categories,
        List<Fighter> Fighters);

    private void FillCommonParameters(SqliteCommand cmd, FighterFormData formData)
    {
        cmd.Parameters.AddWithValue("@fn", formData.FirstName);
        cmd.Parameters.AddWithValue("@ln", formData.LastName);
        cmd.Parameters.AddWithValue("@w", formData.Weight);
        cmd.Parameters.AddWithValue("@a", formData.Age);
        cmd.Parameters.AddWithValue("@c", formData.ClubId);
        cmd.Parameters.AddWithValue("@ac", formData.AgeCategory);
        cmd.Parameters.AddWithValue("@wc", formData.WeightCategory);
        cmd.Parameters.AddWithValue("@by", formData.BirthYear);
        cmd.Parameters.AddWithValue("@g", formData.Gender);
    }

    private static Fighter MapToFighter(FighterFormData formData, int id = 0)
    {
        return new Fighter
        {
            Id = id,
            FirstName = formData.FirstName,
            LastName = formData.LastName,
            Weight = formData.Weight,
            Age = formData.Age,
            BirthYear = formData.BirthYear,
            ClubId = formData.ClubId,
            Gender = formData.Gender,
            AgeCategory = formData.AgeCategory,
            WeightCategory = formData.WeightCategory
        };
    }

    private void UpdateAutoCategory()
    {
        CategoryBox.Text = string.Empty;
        WeightClassBox.Text = string.Empty;

        if (!int.TryParse(AgeBox.Text, out var age))
            return;

        if (!TryParseWeight(WeightBox.Text, out var weight))
            return;

        var selectedGender = GenderCombo.SelectedItem?.ToString();

        if (string.IsNullOrWhiteSpace(selectedGender))
            return;

        if (!TryResolveCategory(age, weight, selectedGender, out var matched))
            return;

        CategoryBox.Text = matched.Division;
        WeightClassBox.Text = matched.CategoryName;
    }

    private bool TryResolveCategory(int age, double weight, string? selectedGender, out Category matchedCategory)
    {
        matchedCategory = null!;

        if (string.IsNullOrWhiteSpace(selectedGender))
            return false;

        var candidates = allCategories
            .Where(x =>
                x.AgeMin <= age &&
                x.AgeMax >= age &&
                x.Gender.Equals(selectedGender, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 0)
            return false;

        var divisionPriority = GetDivisionPriority(age);

        var matched = candidates
            .Where(x => IsWeightAllowedInCategory(weight, x))
            .OrderBy(x => divisionPriority.IndexOf(x.Division))
            .ThenBy(x => x.SortOrder)
            .FirstOrDefault();

        if (matched == null)
            return false;

        matchedCategory = matched;
        return true;
    }

    private static bool IsWeightAllowedInCategory(double weight, Category category)
        => category.IsOpenWeight || weight <= category.WeightMax;

    private bool TryParseWeight(string? value, out double weight)
    {
        if (double.TryParse(
                value,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out weight))
            return true;

        return double.TryParse(value, out weight);
    }

    private List<string> GetDivisionPriority(int age)
    {
        if (age >= 18 && age <= 23)
            return new List<string> { "U24", "Senior", "Masters", "U18", "U16", "U14", "U12" };

        if (age >= 41)
            return new List<string> { "Masters", "Senior", "U24", "U18", "U16", "U14", "U12" };

        return new List<string> { "U12", "U14", "U16", "U18", "Senior", "U24", "Masters" };
    }

    private void ClearFormInternal()
    {
        selectedFighter = null;
        FirstNameBox.Text = string.Empty;
        LastNameBox.Text = string.Empty;
        BirthYearBox.Text = string.Empty;
        AgeBox.Text = string.Empty;
        WeightBox.Text = string.Empty;
        GenderCombo.SelectedItem = null;
        CategoryBox.Text = string.Empty;
        WeightClassBox.Text = string.Empty;
        ClubCombo.SelectedItem = null;
        FightersGrid.SelectedItem = null;
    }

    private class FighterFormData
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public int ClubId { get; set; }
        public int BirthYear { get; set; }
        public int Age { get; set; }
        public double Weight { get; set; }
        public string Gender { get; set; } = "";
        public string AgeCategory { get; set; } = "";
        public string WeightCategory { get; set; } = "";
    }
}
