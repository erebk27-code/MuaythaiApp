using Microsoft.Data.Sqlite;
using MuaythaiApp.Database;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace MuaythaiApp;

public static class ChampionshipSettingsService
{
    private const string CurrentChampionshipIdKey = "CurrentChampionshipId";
    public static event Action? SettingsChanged;

    public static ChampionshipSettings Load()
    {
        EnsureStorage();

        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();
        return Load(connection, GetActiveChampionshipId(connection));
    }

    public static ChampionshipSettings Load(int championshipId)
    {
        EnsureStorage();

        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();
        return Load(connection, championshipId);
    }

    public static IReadOnlyList<ChampionshipListItem> GetChampionships()
    {
        EnsureStorage();

        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();
        return GetChampionships(connection);
    }

    public static int GetActiveChampionshipId()
    {
        EnsureStorage();

        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();
        return GetActiveChampionshipId(connection);
    }

    public static int GetOrCreateActiveChampionshipId()
        => GetActiveChampionshipId();

    public static void SetActiveChampionship(int championshipId)
    {
        EnsureStorage();

        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();
        SetActiveChampionship(connection, championshipId);
    }

    public static int CreateChampionship(string championshipName)
    {
        EnsureStorage();

        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText =
        @"
        INSERT INTO Championships
        (
            Name,
            Address,
            StartDate,
            EndDate,
            RingDefinitionsJson,
            ActiveCategoryIds,
            CreatedAtUtc
        )
        VALUES
        (
            @name,
            @address,
            @startDate,
            @endDate,
            @ringDefinitionsJson,
            @activeCategoryIds,
            @createdAtUtc
        );
        SELECT last_insert_rowid();
        ";
        command.Parameters.AddWithValue("@name", string.IsNullOrWhiteSpace(championshipName)
            ? "Muaythai Championship"
            : championshipName.Trim());
        command.Parameters.AddWithValue("@address", string.Empty);
        command.Parameters.AddWithValue("@startDate", DBNull.Value);
        command.Parameters.AddWithValue("@endDate", DBNull.Value);
        command.Parameters.AddWithValue("@ringDefinitionsJson", JsonSerializer.Serialize(new[]
        {
            new ChampionshipRingDefinition { RingName = "RING A" }
        }));
        command.Parameters.AddWithValue("@activeCategoryIds", string.Empty);
        command.Parameters.AddWithValue("@createdAtUtc", DateTime.UtcNow.ToString("O"));

        var newId = Convert.ToInt32(command.ExecuteScalar() ?? 0);
        SetActiveChampionship(connection, newId);
        return newId;
    }

    public static void SaveActiveCategoryIds(IEnumerable<int> categoryIds)
    {
        var settings = Load();
        settings.ActiveCategoryIds = categoryIds
            .Where(x => x > 0)
            .ToHashSet();
        Save(settings);
    }

    public static void Save(ChampionshipSettings settings)
    {
        EnsureStorage();

        var sanitized = Sanitize(settings);

        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();
        EnsureMatchRingColumn(connection);

        var championshipId = sanitized.Id > 0
            ? sanitized.Id
            : GetActiveChampionshipId(connection);

        var command = connection.CreateCommand();
        command.CommandText =
        @"
        UPDATE Championships
        SET Name = @name,
            Address = @address,
            StartDate = @startDate,
            EndDate = @endDate,
            RingDefinitionsJson = @ringDefinitionsJson,
            ActiveCategoryIds = @activeCategoryIds
        WHERE Id = @id
        ";
        command.Parameters.AddWithValue("@id", championshipId);
        command.Parameters.AddWithValue("@name", sanitized.ChampionshipName);
        command.Parameters.AddWithValue("@address", sanitized.ChampionshipAddress);
        command.Parameters.AddWithValue("@startDate", sanitized.StartDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) as object ?? DBNull.Value);
        command.Parameters.AddWithValue("@endDate", sanitized.EndDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) as object ?? DBNull.Value);
        command.Parameters.AddWithValue("@ringDefinitionsJson", JsonSerializer.Serialize(sanitized.RingDefinitions));
        command.Parameters.AddWithValue("@activeCategoryIds", string.Join(",", sanitized.ActiveCategoryIds.OrderBy(x => x)));
        command.ExecuteNonQuery();

        NormalizeExistingMatchRings(connection, championshipId, sanitized.RingNames[0]);
        SettingsChanged?.Invoke();
    }

    public static string GetChampionshipName()
        => Load().ChampionshipName;

    public static IReadOnlyList<string> GetRingNames()
        => Load().RingNames;

    public static IReadOnlyList<ChampionshipRingDefinition> GetRingDefinitions()
        => Load().RingDefinitions;

    public static string ResolveRingName(string division, string weightCategory, string gender, int fallbackIndex)
    {
        var settings = Load();
        var definitions = settings.RingDefinitions
            .Where(x => !string.IsNullOrWhiteSpace(x.RingName))
            .ToList();

        if (definitions.Count == 0)
            definitions = settings.RingNames.Select(x => new ChampionshipRingDefinition { RingName = x }).ToList();

        if (definitions.Count == 0)
            return "RING A";

        var matchingDefinitions = definitions
            .Where(x => x.Supports(division, weightCategory, gender))
            .ToList();

        var source = matchingDefinitions.Count > 0 ? matchingDefinitions : definitions;
        var index = Math.Abs(fallbackIndex) % source.Count;
        return source[index].RingName;
    }

    public static int GetDayCount()
    {
        var settings = Load();

        if (!settings.StartDate.HasValue || !settings.EndDate.HasValue)
            return 4;

        var days = (settings.EndDate.Value.Date - settings.StartDate.Value.Date).Days + 1;
        return Math.Max(days, 1);
    }

    public static string GetDateLabelForDay(int dayNumber)
    {
        var settings = Load();
        if (!settings.StartDate.HasValue)
            return DateTime.Now.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

        var dayOffset = Math.Max(dayNumber - 1, 0);
        return settings.StartDate.Value.AddDays(dayOffset).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
    }

    public static List<Category> FilterActiveCategories(IEnumerable<Category> categories)
    {
        var settings = Load();
        if (settings.ActiveCategoryIds.Count == 0)
            return categories.ToList();

        return categories
            .Where(x => settings.ActiveCategoryIds.Contains(x.Id))
            .ToList();
    }

    public static bool IsCategoryAllowed(string division, string weightCategory, string gender, IEnumerable<Category> categories)
    {
        var activeCategories = FilterActiveCategories(categories);
        return activeCategories.Any(x =>
            string.Equals(x.Division, division, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.CategoryName, weightCategory, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Gender, gender, StringComparison.OrdinalIgnoreCase));
    }

    private static void EnsureStorage()
    {
        var databaseHelper = new DatabaseHelper();
        databaseHelper.CreateDatabase();
    }

    private static ChampionshipSettings Load(SqliteConnection connection, int championshipId)
    {
        var command = connection.CreateCommand();
        command.CommandText =
        @"
        SELECT
            Id,
            Name,
            Address,
            StartDate,
            EndDate,
            RingDefinitionsJson,
            ActiveCategoryIds
        FROM Championships
        WHERE Id = @id
        LIMIT 1
        ";
        command.Parameters.AddWithValue("@id", championshipId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            SetActiveChampionship(connection, 1);
            return new ChampionshipSettings
            {
                Id = 1
            };
        }

        var ringDefinitions = ParseRingDefinitions(reader.IsDBNull(5) ? string.Empty : reader.GetString(5));

        var settings = new ChampionshipSettings
        {
            Id = reader.GetInt32(0),
            ChampionshipName = reader.IsDBNull(1) ? "Muaythai Championship" : reader.GetString(1),
            ChampionshipAddress = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            StartDate = ParseDate(reader.IsDBNull(3) ? null : reader.GetString(3)),
            EndDate = ParseDate(reader.IsDBNull(4) ? null : reader.GetString(4)),
            RingDefinitions = ringDefinitions,
            RingNames = ringDefinitions.Select(x => x.RingName).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ActiveCategoryIds = ParseCategoryIds(reader.IsDBNull(6) ? string.Empty : reader.GetString(6))
        };

        if (settings.RingNames.Count == 0)
            settings.RingNames.Add("RING A");

        if (settings.EndDate.HasValue && settings.StartDate.HasValue && settings.EndDate < settings.StartDate)
            settings.EndDate = settings.StartDate;

        return settings;
    }

    private static IReadOnlyList<ChampionshipListItem> GetChampionships(SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText =
        @"
        SELECT
            Id,
            Name,
            StartDate,
            EndDate
        FROM Championships
        ORDER BY
            CASE
                WHEN StartDate IS NULL OR TRIM(StartDate) = '' THEN 1
                ELSE 0
            END,
            StartDate DESC,
            Id DESC
        ";

        var items = new List<ChampionshipListItem>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new ChampionshipListItem
            {
                Id = reader.GetInt32(0),
                ChampionshipName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                StartDate = ParseDate(reader.IsDBNull(2) ? null : reader.GetString(2)),
                EndDate = ParseDate(reader.IsDBNull(3) ? null : reader.GetString(3))
            });
        }

        return items;
    }

    private static ChampionshipSettings Sanitize(ChampionshipSettings settings)
    {
        var sanitizedRingDefinitions = settings.RingDefinitions
            .Select(SanitizeRingDefinition)
            .Where(x => !string.IsNullOrWhiteSpace(x.RingName))
            .GroupBy(x => x.RingName, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();

        if (sanitizedRingDefinitions.Count == 0)
        {
            sanitizedRingDefinitions.AddRange(settings.RingNames
                .Select(NormalizeRingName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(x => new ChampionshipRingDefinition { RingName = x }));
        }

        if (sanitizedRingDefinitions.Count == 0)
            sanitizedRingDefinitions.Add(new ChampionshipRingDefinition { RingName = "RING A" });

        var sanitizedRings = sanitizedRingDefinitions
            .Select(x => x.RingName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (sanitizedRings.Count == 0)
            sanitizedRings.Add("RING A");

        var startDate = settings.StartDate?.Date;
        var endDate = settings.EndDate?.Date;

        if (startDate.HasValue && endDate.HasValue && endDate < startDate)
            endDate = startDate;

        return new ChampionshipSettings
        {
            Id = settings.Id,
            ChampionshipName = string.IsNullOrWhiteSpace(settings.ChampionshipName)
                ? "Muaythai Championship"
                : settings.ChampionshipName.Trim(),
            ChampionshipAddress = settings.ChampionshipAddress?.Trim() ?? string.Empty,
            StartDate = startDate,
            EndDate = endDate,
            RingNames = sanitizedRings,
            RingDefinitions = sanitizedRingDefinitions,
            ActiveCategoryIds = new HashSet<int>(settings.ActiveCategoryIds)
        };
    }

    private static int GetActiveChampionshipId(SqliteConnection connection)
    {
        var currentId = ReadSetting(connection, CurrentChampionshipIdKey);
        if (int.TryParse(currentId, out var championshipId) && ChampionshipExists(connection, championshipId))
            return championshipId;

        var firstIdCommand = connection.CreateCommand();
        firstIdCommand.CommandText = "SELECT Id FROM Championships ORDER BY Id LIMIT 1";
        championshipId = Convert.ToInt32(firstIdCommand.ExecuteScalar() ?? 1);
        SetActiveChampionship(connection, championshipId);
        return championshipId;
    }

    private static void SetActiveChampionship(SqliteConnection connection, int championshipId)
    {
        if (!ChampionshipExists(connection, championshipId))
            return;

        var command = connection.CreateCommand();
        command.CommandText =
        @"
        INSERT INTO AppSettings (Key, Value)
        VALUES (@key, @value)
        ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value
        ";
        command.Parameters.AddWithValue("@key", CurrentChampionshipIdKey);
        command.Parameters.AddWithValue("@value", championshipId.ToString(CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    private static bool ChampionshipExists(SqliteConnection connection, int championshipId)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Championships WHERE Id = @id";
        command.Parameters.AddWithValue("@id", championshipId);
        return Convert.ToInt32(command.ExecuteScalar() ?? 0) > 0;
    }

    private static void EnsureMatchRingColumn(SqliteConnection connection)
    {
        if (!TableExists(connection, "Matches"))
            return;

        var existingColumns = GetExistingColumns(connection, "Matches");
        if (existingColumns.Contains("RingName"))
            return;

        var command = connection.CreateCommand();
        command.CommandText = "ALTER TABLE Matches ADD COLUMN RingName TEXT NOT NULL DEFAULT 'RING A'";
        command.ExecuteNonQuery();
    }

    private static string? ReadSetting(SqliteConnection connection, string key)
    {
        var command = connection.CreateCommand();
        command.CommandText =
        @"
        SELECT Value
        FROM AppSettings
        WHERE Key = @key
        LIMIT 1
        ";
        command.Parameters.AddWithValue("@key", key);
        return command.ExecuteScalar()?.ToString();
    }

    private static DateTime? ParseDate(string? value)
    {
        return DateTime.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? date
            : null;
    }

    private static List<ChampionshipRingDefinition> ParseRingDefinitions(string? definitionsJson)
    {
        if (!string.IsNullOrWhiteSpace(definitionsJson))
        {
            try
            {
                var definitions = JsonSerializer.Deserialize<List<ChampionshipRingDefinition>>(definitionsJson);
                if (definitions != null)
                {
                    var sanitized = definitions
                        .Select(SanitizeRingDefinition)
                        .Where(x => !string.IsNullOrWhiteSpace(x.RingName))
                        .ToList();
                    if (sanitized.Count > 0)
                        return sanitized;
                }
            }
            catch
            {
            }
        }

        return new List<ChampionshipRingDefinition>
        {
            new ChampionshipRingDefinition { RingName = "RING A" }
        };
    }

    private static HashSet<int> ParseCategoryIds(string? value)
    {
        return (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToHashSet();
    }

    private static string NormalizeRingName(string value)
        => value.Trim().ToUpperInvariant();

    private static ChampionshipRingDefinition SanitizeRingDefinition(ChampionshipRingDefinition? definition)
    {
        return new ChampionshipRingDefinition
        {
            RingName = NormalizeRingName(definition?.RingName ?? string.Empty),
            JudgesText = (definition?.JudgesText ?? string.Empty).Trim(),
            DivisionNamesText = string.Join(", ", ChampionshipRingDefinition.SplitValues(definition?.DivisionNamesText)),
            GendersText = string.Join(", ", ChampionshipRingDefinition.SplitValues(definition?.GendersText))
        };
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        var command = connection.CreateCommand();
        command.CommandText =
        @"
        SELECT COUNT(*)
        FROM sqlite_master
        WHERE type = 'table' AND name = @tableName
        ";
        command.Parameters.AddWithValue("@tableName", tableName);
        return Convert.ToInt32(command.ExecuteScalar() ?? 0) > 0;
    }

    private static HashSet<string> GetExistingColumns(SqliteConnection connection, string tableName)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pragma = connection.CreateCommand();
        pragma.CommandText = $"PRAGMA table_info({tableName})";

        using var reader = pragma.ExecuteReader();
        while (reader.Read())
        {
            if (!reader.IsDBNull(1))
                columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private static void NormalizeExistingMatchRings(SqliteConnection connection, int championshipId, string defaultRingName)
    {
        if (!TableExists(connection, "Matches"))
            return;

        var existingColumns = GetExistingColumns(connection, "Matches");
        if (!existingColumns.Contains("RingName"))
            return;

        var command = connection.CreateCommand();
        command.CommandText =
        @"
        UPDATE Matches
        SET RingName = @ringName
        WHERE ChampionshipId = @championshipId
          AND (RingName IS NULL OR TRIM(RingName) = '')
        ";
        command.Parameters.AddWithValue("@ringName", defaultRingName);
        command.Parameters.AddWithValue("@championshipId", championshipId);
        command.ExecuteNonQuery();
    }
}
