using Microsoft.Data.Sqlite;
using MuaythaiApp.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MuaythaiApp.Database;

public class DatabaseHelper
{
    private readonly string connectionString = BuildConnectionString();

    public static SqliteConnection CreateConnection()
    {
        return new SqliteConnection(BuildConnectionString());
    }

    private static string BuildConnectionString()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = AppPaths.GetDatabasePath(),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            DefaultTimeout = 10
        };

        return builder.ToString();
    }

    public void CreateDatabase()
    {
        PromoteLegacyDatabaseIfNeeded();

        using var connection =
            new SqliteConnection(connectionString);

        connection.Open();

        var command =
            connection.CreateCommand();

        command.CommandText =
        @"
    CREATE TABLE IF NOT EXISTS Fighters (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        FirstName TEXT,
        LastName TEXT,
        Weight REAL,
        Age INTEGER,
        ClubId INTEGER
    );

    CREATE TABLE IF NOT EXISTS Clubs (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        Name TEXT,
        Coach TEXT,
        City TEXT,
        Country TEXT
    );

    CREATE TABLE IF NOT EXISTS Categories (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        Division TEXT NOT NULL,
        Gender TEXT NOT NULL,
        AgeMin INTEGER NOT NULL,
        AgeMax INTEGER NOT NULL,
        WeightMax REAL NOT NULL,
        IsOpenWeight INTEGER NOT NULL DEFAULT 0,
        SortOrder INTEGER NOT NULL,
        CategoryName TEXT NOT NULL,
        RoundCount INTEGER NOT NULL DEFAULT 3,
        RoundDurationSeconds INTEGER NOT NULL DEFAULT 120,
        BreakDurationSeconds INTEGER NOT NULL DEFAULT 60
    );

    CREATE TABLE IF NOT EXISTS Matches (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        Fighter1Id INTEGER,
        Fighter2Id INTEGER,
        Fighter1Name TEXT,
        Fighter2Name TEXT,
        AgeCategory TEXT,
        WeightCategory TEXT,
        Gender TEXT,
        CategoryGroup TEXT,
        OrderNo INTEGER,
        JudgesCount INTEGER,
        DayNumber INTEGER NOT NULL DEFAULT 1
    );

    CREATE TABLE IF NOT EXISTS JudgeScores (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        MatchId INTEGER NOT NULL,
        RoundNo INTEGER NOT NULL,
        JudgeId INTEGER NOT NULL,
        RedPoints INTEGER NOT NULL,
        BluePoints INTEGER NOT NULL,
        RedWarning INTEGER NOT NULL DEFAULT 0,
        BlueWarning INTEGER NOT NULL DEFAULT 0
    );

    CREATE TABLE IF NOT EXISTS MatchResult (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        MatchId INTEGER NOT NULL,
        Winner TEXT,
        Method TEXT,
        Round INTEGER,
        JudgeRed INTEGER,
        JudgeBlue INTEGER
    );

    CREATE TABLE IF NOT EXISTS AthleteDailyChecks (
        FighterId INTEGER NOT NULL,
        DayNumber INTEGER NOT NULL,
        ChampionshipId INTEGER NOT NULL DEFAULT 1,
        MeasuredWeight REAL NULL,
        GenderConfirmed INTEGER NOT NULL DEFAULT 1,
        PRIMARY KEY (FighterId, DayNumber, ChampionshipId)
    );

    CREATE TABLE IF NOT EXISTS AppSettings (
        Key TEXT PRIMARY KEY,
        Value TEXT NOT NULL
    );

    CREATE TABLE IF NOT EXISTS Championships (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        Name TEXT NOT NULL,
        Address TEXT NOT NULL DEFAULT '',
        StartDate TEXT,
        EndDate TEXT,
        RingDefinitionsJson TEXT NOT NULL DEFAULT '[]',
        ActiveCategoryIds TEXT NOT NULL DEFAULT '',
        CreatedAtUtc TEXT NOT NULL DEFAULT ''
    );
    ";

        command.ExecuteNonQuery();

        EnsureFighterColumns(connection);
        EnsureClubColumns(connection);
        EnsureCategoryColumns(connection);
        EnsureMatchColumns(connection);
        EnsureAthleteDailyCheckColumns(connection);
        EnsureChampionshipSchema(connection);
        EnsureDefaultPasswords(connection);
        SeedCategories(connection);
        NormalizeCategoryGenders(connection);
        UpdateCategoryRoundRules(connection);
    }

    private void PromoteLegacyDatabaseIfNeeded()
    {
        var currentDatabasePath = AppPaths.GetDatabasePath();
        var appDataDatabasePath = AppPaths.GetAppDataDatabasePath();

        if (!string.Equals(currentDatabasePath, appDataDatabasePath, StringComparison.OrdinalIgnoreCase))
            return;

        var currentStats = ReadDatabaseStats(appDataDatabasePath);
        if (!currentStats.IsEffectivelyEmpty)
            return;

        var bestLegacyPath = AppPaths.GetExistingLegacyDatabasePaths()
            .Select(path => new { Path = path, Stats = ReadDatabaseStats(path) })
            .Where(x => x.Stats.HasUserData)
            .OrderByDescending(x => x.Stats.TotalUserRows)
            .Select(x => x.Path)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(bestLegacyPath))
            return;

        if (File.Exists(appDataDatabasePath))
        {
            var backupPath = appDataDatabasePath + ".backup";
            File.Copy(appDataDatabasePath, backupPath, overwrite: true);
        }

        File.Copy(bestLegacyPath, appDataDatabasePath, overwrite: true);
    }

    private DatabaseStats ReadDatabaseStats(string databasePath)
    {
        if (!File.Exists(databasePath))
            return DatabaseStats.Empty;

        try
        {
            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();

            return new DatabaseStats(
                ReadTableCount(connection, "Fighters"),
                ReadTableCount(connection, "Clubs"),
                ReadTableCount(connection, "Matches"));
        }
        catch
        {
            return DatabaseStats.Empty;
        }
    }

    private int ReadTableCount(SqliteConnection connection, string tableName)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {tableName}";
        return Convert.ToInt32(command.ExecuteScalar() ?? 0);
    }

    private void EnsureDefaultPasswords(SqliteConnection connection)
    {
        EnsureSetting(connection, "AdminPasswordHash", PasswordHasher.Hash(AuthService.DefaultAdminPassword));
        EnsureSetting(connection, "UserPasswordHash", PasswordHasher.Hash(AuthService.DefaultUserPassword));
    }

    private void EnsureFighterColumns(SqliteConnection connection)
    {
        var existingColumns = GetExistingColumns(connection, "Fighters");

        AddColumnIfMissing(connection, existingColumns, "BirthYear",
            "ALTER TABLE Fighters ADD COLUMN BirthYear INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, existingColumns, "Gender",
            "ALTER TABLE Fighters ADD COLUMN Gender TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, existingColumns, "AgeCategory",
            "ALTER TABLE Fighters ADD COLUMN AgeCategory TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, existingColumns, "WeightCategory",
            "ALTER TABLE Fighters ADD COLUMN WeightCategory TEXT NOT NULL DEFAULT ''");
    }

    private void EnsureClubColumns(SqliteConnection connection)
    {
        var existingColumns = GetExistingColumns(connection, "Clubs");

        AddColumnIfMissing(connection, existingColumns, "Coach",
            "ALTER TABLE Clubs ADD COLUMN Coach TEXT");
        AddColumnIfMissing(connection, existingColumns, "City",
            "ALTER TABLE Clubs ADD COLUMN City TEXT");
        AddColumnIfMissing(connection, existingColumns, "Country",
            "ALTER TABLE Clubs ADD COLUMN Country TEXT");
    }

    private void EnsureCategoryColumns(SqliteConnection connection)
    {
        var existingColumns = GetExistingColumns(connection, "Categories");

        AddColumnIfMissing(connection, existingColumns, "RoundCount",
            "ALTER TABLE Categories ADD COLUMN RoundCount INTEGER NOT NULL DEFAULT 3");
        AddColumnIfMissing(connection, existingColumns, "RoundDurationSeconds",
            "ALTER TABLE Categories ADD COLUMN RoundDurationSeconds INTEGER NOT NULL DEFAULT 120");
        AddColumnIfMissing(connection, existingColumns, "BreakDurationSeconds",
            "ALTER TABLE Categories ADD COLUMN BreakDurationSeconds INTEGER NOT NULL DEFAULT 60");
    }

    private void EnsureMatchColumns(SqliteConnection connection)
    {
        var existingColumns = GetExistingColumns(connection, "Matches");

        AddColumnIfMissing(connection, existingColumns, "ChampionshipId",
            "ALTER TABLE Matches ADD COLUMN ChampionshipId INTEGER NOT NULL DEFAULT 1");
        AddColumnIfMissing(connection, existingColumns, "DayNumber",
            "ALTER TABLE Matches ADD COLUMN DayNumber INTEGER NOT NULL DEFAULT 1");
        AddColumnIfMissing(connection, existingColumns, "RingName",
            "ALTER TABLE Matches ADD COLUMN RingName TEXT NOT NULL DEFAULT 'RING A'");

        var normalizeRingNames = connection.CreateCommand();
        normalizeRingNames.CommandText =
        @"
        UPDATE Matches
        SET RingName = 'RING A'
        WHERE RingName IS NULL OR TRIM(RingName) = ''
        ";
        normalizeRingNames.ExecuteNonQuery();
    }

    private void EnsureAthleteDailyCheckColumns(SqliteConnection connection)
    {
        var existingColumns = GetExistingColumns(connection, "AthleteDailyChecks");

        AddColumnIfMissing(connection, existingColumns, "ChampionshipId",
            "ALTER TABLE AthleteDailyChecks ADD COLUMN ChampionshipId INTEGER NOT NULL DEFAULT 1");
        AddColumnIfMissing(connection, existingColumns, "LicensePresented",
            "ALTER TABLE AthleteDailyChecks ADD COLUMN LicensePresented INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, existingColumns, "MedicalReportPresented",
            "ALTER TABLE AthleteDailyChecks ADD COLUMN MedicalReportPresented INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, existingColumns, "IdentityPresented",
            "ALTER TABLE AthleteDailyChecks ADD COLUMN IdentityPresented INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, existingColumns, "InsurancePresented",
            "ALTER TABLE AthleteDailyChecks ADD COLUMN InsurancePresented INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, existingColumns, "RegistrationFormPresented",
            "ALTER TABLE AthleteDailyChecks ADD COLUMN RegistrationFormPresented INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, existingColumns, "GuardianConsentPresented",
            "ALTER TABLE AthleteDailyChecks ADD COLUMN GuardianConsentPresented INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, existingColumns, "SeniorGuardianConsentPresented",
            "ALTER TABLE AthleteDailyChecks ADD COLUMN SeniorGuardianConsentPresented INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, existingColumns, "AmateurLicense2026Presented",
            "ALTER TABLE AthleteDailyChecks ADD COLUMN AmateurLicense2026Presented INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, existingColumns, "PolishCitizenshipConfirmed",
            "ALTER TABLE AthleteDailyChecks ADD COLUMN PolishCitizenshipConfirmed INTEGER NOT NULL DEFAULT 0");

        EnsureAthleteDailyChecksPrimaryKey(connection);
    }

    private void EnsureAthleteDailyChecksPrimaryKey(SqliteConnection connection)
    {
        var primaryKeyColumns = GetPrimaryKeyColumns(connection, "AthleteDailyChecks");
        if (primaryKeyColumns.SequenceEqual(new[] { "FighterId", "DayNumber", "ChampionshipId" }))
            return;

        using var transaction = connection.BeginTransaction();
        ExecuteNonQuery(connection, transaction, "ALTER TABLE AthleteDailyChecks RENAME TO AthleteDailyChecks_Legacy");
        ExecuteNonQuery(connection, transaction,
        @"
        CREATE TABLE AthleteDailyChecks (
            FighterId INTEGER NOT NULL,
            DayNumber INTEGER NOT NULL,
            ChampionshipId INTEGER NOT NULL DEFAULT 1,
            MeasuredWeight REAL NULL,
            GenderConfirmed INTEGER NOT NULL DEFAULT 1,
            LicensePresented INTEGER NOT NULL DEFAULT 0,
            MedicalReportPresented INTEGER NOT NULL DEFAULT 0,
            IdentityPresented INTEGER NOT NULL DEFAULT 0,
            InsurancePresented INTEGER NOT NULL DEFAULT 0,
            RegistrationFormPresented INTEGER NOT NULL DEFAULT 0,
            GuardianConsentPresented INTEGER NOT NULL DEFAULT 0,
            SeniorGuardianConsentPresented INTEGER NOT NULL DEFAULT 0,
            AmateurLicense2026Presented INTEGER NOT NULL DEFAULT 0,
            PolishCitizenshipConfirmed INTEGER NOT NULL DEFAULT 0,
            PRIMARY KEY (FighterId, DayNumber, ChampionshipId)
        )
        ");
        ExecuteNonQuery(connection, transaction,
        @"
        INSERT OR REPLACE INTO AthleteDailyChecks
        (
            FighterId,
            DayNumber,
            ChampionshipId,
            MeasuredWeight,
            GenderConfirmed,
            LicensePresented,
            MedicalReportPresented,
            IdentityPresented,
            InsurancePresented,
            RegistrationFormPresented,
            GuardianConsentPresented,
            SeniorGuardianConsentPresented,
            AmateurLicense2026Presented,
            PolishCitizenshipConfirmed
        )
        SELECT
            FighterId,
            DayNumber,
            CASE
                WHEN ChampionshipId IS NULL OR ChampionshipId <= 0 THEN 1
                ELSE ChampionshipId
            END,
            MeasuredWeight,
            COALESCE(GenderConfirmed, 1),
            COALESCE(LicensePresented, 0),
            COALESCE(MedicalReportPresented, 0),
            COALESCE(IdentityPresented, 0),
            COALESCE(InsurancePresented, 0),
            COALESCE(RegistrationFormPresented, 0),
            COALESCE(GuardianConsentPresented, 0),
            COALESCE(SeniorGuardianConsentPresented, 0),
            COALESCE(AmateurLicense2026Presented, 0),
            COALESCE(PolishCitizenshipConfirmed, 0)
        FROM AthleteDailyChecks_Legacy
        ");
        ExecuteNonQuery(connection, transaction, "DROP TABLE AthleteDailyChecks_Legacy");
        transaction.Commit();
    }

    private void EnsureChampionshipSchema(SqliteConnection connection)
    {
        EnsureSetting(connection, "CurrentChampionshipId", "1");

        var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM Championships";
        var championshipCount = Convert.ToInt32(countCommand.ExecuteScalar() ?? 0);

        if (championshipCount == 0)
        {
            var insertDefault = connection.CreateCommand();
            insertDefault.CommandText =
            @"
            INSERT INTO Championships
            (
                Id,
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
                1,
                @name,
                @address,
                @startDate,
                @endDate,
                @ringDefinitionsJson,
                @activeCategoryIds,
                @createdAtUtc
            )
            ";
            insertDefault.Parameters.AddWithValue("@name", ReadSetting(connection, "Championship.Name") ?? "Muaythai Championship");
            insertDefault.Parameters.AddWithValue("@address", ReadSetting(connection, "Championship.Address") ?? string.Empty);
            insertDefault.Parameters.AddWithValue("@startDate", ReadSetting(connection, "Championship.StartDate") as object ?? DBNull.Value);
            insertDefault.Parameters.AddWithValue("@endDate", ReadSetting(connection, "Championship.EndDate") as object ?? DBNull.Value);
            insertDefault.Parameters.AddWithValue("@ringDefinitionsJson", BuildLegacyRingDefinitionsJson(connection));
            insertDefault.Parameters.AddWithValue("@activeCategoryIds", ReadSetting(connection, "Championship.ActiveCategoryIds") ?? string.Empty);
            insertDefault.Parameters.AddWithValue("@createdAtUtc", DateTime.UtcNow.ToString("O"));
            insertDefault.ExecuteNonQuery();
        }

        var normalizeMatches = connection.CreateCommand();
        normalizeMatches.CommandText =
        @"
        UPDATE Matches
        SET ChampionshipId = 1
        WHERE ChampionshipId IS NULL OR ChampionshipId <= 0
        ";
        normalizeMatches.ExecuteNonQuery();

        var normalizeChecks = connection.CreateCommand();
        normalizeChecks.CommandText =
        @"
        UPDATE AthleteDailyChecks
        SET ChampionshipId = 1
        WHERE ChampionshipId IS NULL OR ChampionshipId <= 0
        ";
        normalizeChecks.ExecuteNonQuery();
    }

    private void AddColumnIfMissing(
        SqliteConnection connection,
        HashSet<string> existingColumns,
        string columnName,
        string sql)
    {
        if (existingColumns.Contains(columnName))
            return;

        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
        existingColumns.Add(columnName);
    }

    private readonly record struct DatabaseStats(int Fighters, int Clubs, int Matches)
    {
        public static DatabaseStats Empty => new(0, 0, 0);

        public int TotalUserRows => Fighters + Clubs + Matches;

        public bool HasUserData => TotalUserRows > 0;

        public bool IsEffectivelyEmpty => TotalUserRows == 0;
    }

    private void EnsureSetting(SqliteConnection connection, string key, string value)
    {
        var existsCommand = connection.CreateCommand();
        existsCommand.CommandText =
        @"
        SELECT COUNT(*)
        FROM AppSettings
        WHERE Key = @key
        ";
        existsCommand.Parameters.AddWithValue("@key", key);

        var exists = Convert.ToInt32(existsCommand.ExecuteScalar() ?? 0) > 0;

        if (exists)
            return;

        var insertCommand = connection.CreateCommand();
        insertCommand.CommandText =
        @"
        INSERT INTO AppSettings (Key, Value)
        VALUES (@key, @value)
        ";
        insertCommand.Parameters.AddWithValue("@key", key);
        insertCommand.Parameters.AddWithValue("@value", value);
        insertCommand.ExecuteNonQuery();
    }

    private string? ReadSetting(SqliteConnection connection, string key)
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

    private string BuildLegacyRingDefinitionsJson(SqliteConnection connection)
    {
        var json = ReadSetting(connection, "Championship.RingDefinitions");
        if (!string.IsNullOrWhiteSpace(json))
            return json;

        var ringNames = ReadSetting(connection, "Championship.RingNames") ?? "RING A";
        var definitions = ringNames
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => $"{{\"RingName\":\"{x.Replace("\"", "\\\"")}\",\"JudgesText\":\"\",\"DivisionNamesText\":\"\",\"GendersText\":\"\"}}");

        return $"[{string.Join(",", definitions)}]";
    }

    private HashSet<string> GetExistingColumns(SqliteConnection connection, string tableName)
    {
        var columns = new HashSet<string>();

        var pragma = connection.CreateCommand();
        pragma.CommandText = $"PRAGMA table_info({tableName})";

        using var reader = pragma.ExecuteReader();

        while (reader.Read())
            columns.Add(reader.GetString(1));

        return columns;
    }

    private List<string> GetPrimaryKeyColumns(SqliteConnection connection, string tableName)
    {
        var columns = new List<(int Order, string Name)>();

        var pragma = connection.CreateCommand();
        pragma.CommandText = $"PRAGMA table_info({tableName})";

        using var reader = pragma.ExecuteReader();
        while (reader.Read())
        {
            var primaryKeyOrder = reader.GetInt32(5);
            if (primaryKeyOrder > 0)
                columns.Add((primaryKeyOrder, reader.GetString(1)));
        }

        return columns
            .OrderBy(x => x.Order)
            .Select(x => x.Name)
            .ToList();
    }

    private void ExecuteNonQuery(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private void SeedCategories(SqliteConnection connection)
    {
        var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM Categories";

        var existingCount = (long)(countCommand.ExecuteScalar() ?? 0L);

        if (existingCount > 0)
            return;

        foreach (var category in BuildDefaultCategories())
        {
            var insert = connection.CreateCommand();
            insert.CommandText =
            @"
            INSERT INTO Categories
            (
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
            )
            VALUES
            (
                @division,
                @gender,
                @ageMin,
                @ageMax,
                @weightMax,
                @isOpenWeight,
                @sortOrder,
                @categoryName,
                @roundCount,
                @roundDurationSeconds,
                @breakDurationSeconds
            )
            ";

            insert.Parameters.AddWithValue("@division", category.Division);
            insert.Parameters.AddWithValue("@gender", category.Gender);
            insert.Parameters.AddWithValue("@ageMin", category.AgeMin);
            insert.Parameters.AddWithValue("@ageMax", category.AgeMax);
            insert.Parameters.AddWithValue("@weightMax", category.WeightMax);
            insert.Parameters.AddWithValue("@isOpenWeight", category.IsOpenWeight ? 1 : 0);
            insert.Parameters.AddWithValue("@sortOrder", category.SortOrder);
            insert.Parameters.AddWithValue("@categoryName", category.CategoryName);
            insert.Parameters.AddWithValue("@roundCount", category.RoundCount);
            insert.Parameters.AddWithValue("@roundDurationSeconds", category.RoundDurationSeconds);
            insert.Parameters.AddWithValue("@breakDurationSeconds", category.BreakDurationSeconds);

            insert.ExecuteNonQuery();
        }
    }

    private void UpdateCategoryRoundRules(SqliteConnection connection)
    {
        UpdateRoundRule(connection, 3, 180, 60, "U24", "Senior");
        UpdateRoundRule(connection, 3, 120, 60, "Masters", "Senior B", "U18", "U16");
        UpdateRoundRule(connection, 3, 90, 60, "U14");
        UpdateRoundRule(connection, 3, 60, 60, "U12");
    }

    private void UpdateRoundRule(
        SqliteConnection connection,
        int roundCount,
        int roundDurationSeconds,
        int breakDurationSeconds,
        params string[] divisions)
    {
        foreach (var division in divisions)
        {
            var command = connection.CreateCommand();
            command.CommandText =
            @"
            UPDATE Categories
            SET
                RoundCount = @roundCount,
                RoundDurationSeconds = @roundDurationSeconds,
                BreakDurationSeconds = @breakDurationSeconds
            WHERE Division = @division
            ";

            command.Parameters.AddWithValue("@roundCount", roundCount);
            command.Parameters.AddWithValue("@roundDurationSeconds", roundDurationSeconds);
            command.Parameters.AddWithValue("@breakDurationSeconds", breakDurationSeconds);
            command.Parameters.AddWithValue("@division", division);

            command.ExecuteNonQuery();
        }
    }

    private void NormalizeCategoryGenders(SqliteConnection connection)
    {
        var maleCommand = connection.CreateCommand();
        maleCommand.CommandText =
        @"
        UPDATE Categories
        SET Gender = 'Male'
        WHERE Gender IN ('Boys', 'Men', 'Male')
        ";
        maleCommand.ExecuteNonQuery();

        var femaleCommand = connection.CreateCommand();
        femaleCommand.CommandText =
        @"
        UPDATE Categories
        SET Gender = 'Female'
        WHERE Gender IN ('Girls', 'Women', 'Female')
        ";
        femaleCommand.ExecuteNonQuery();
    }

    private List<CategorySeed> BuildDefaultCategories()
    {
        var categories = new List<CategorySeed>();

        AddDivision(categories, "U12", 10, 11, "Male",
            30, 32, 34, 36, 38, 40, 42, 44, 46, 48, 50, 52, 54, 56, 58, 60, 63.5, 67);
        AddDivision(categories, "U12", 10, 11, "Female",
            30, 32, 34, 36, 38, 40, 42, 44, 46, 48, 50, 52, 54, 56, 58, 60);

        AddDivision(categories, "U14", 12, 13, "Male",
            32, 34, 36, 38, 40, 42, 44, 46, 48, 50, 52, 54, 56, 58, 60, 63.5, 67, 71);
        AddDivision(categories, "U14", 12, 13, "Female",
            32, 34, 36, 38, 40, 42, 44, 46, 48, 50, 52, 54, 56, 58, 60, 63.5);

        AddDivision(categories, "U16", 14, 15, "Male",
            38, 40, 42, 45, 48, 51, 54, 57, 60, 63.5, 67, 71, 75, 81);
        AddDivision(categories, "U16", 14, 15, "Female",
            36, 38, 40, 42, 45, 48, 51, 54, 57, 60, 63.5, 67, 71);

        AddDivision(categories, "U18", 16, 17, "Male",
            45, 48, 51, 54, 57, 60, 63.5, 67, 71, 75, 81, 86, 91);
        AddDivision(categories, "U18", 16, 17, "Female",
            42, 45, 48, 51, 54, 57, 60, 63.5, 67, 71, 75);

        AddDivision(categories, "U24", 18, 23, "Male",
            45, 48, 51, 54, 57, 60, 63.5, 67, 71, 75, 81, 86, 91);
        AddDivision(categories, "U24", 18, 23, "Female",
            45, 48, 51, 54, 57, 60, 63.5, 67, 71, 75);

        AddDivision(categories, "Senior", 18, 40, "Male",
            45, 48, 51, 54, 57, 60, 63.5, 67, 71, 75, 81, 86, 91);
        AddDivision(categories, "Senior", 18, 40, "Female",
            45, 48, 51, 54, 57, 60, 63.5, 67, 71, 75);

        AddDivision(categories, "Masters", 41, 55, "Male",
            45, 48, 51, 54, 57, 60, 63.5, 67, 71, 75, 81, 86, 91);
        AddDivision(categories, "Masters", 41, 55, "Female",
            45, 48, 51, 54, 57, 60, 63.5, 67, 71, 75);

        return categories;
    }

    private void AddDivision(
        List<CategorySeed> categories,
        string division,
        int ageMin,
        int ageMax,
        string gender,
        params double[] weights)
    {
        var rule = GetRoundRule(division);

        for (int i = 0; i < weights.Length; i++)
        {
            var weight = weights[i];

            categories.Add(new CategorySeed
            {
                Division = division,
                Gender = gender,
                AgeMin = ageMin,
                AgeMax = ageMax,
                WeightMax = weight,
                IsOpenWeight = false,
                SortOrder = i + 1,
                CategoryName = $"-{FormatWeight(weight)} kg",
                RoundCount = rule.RoundCount,
                RoundDurationSeconds = rule.RoundDurationSeconds,
                BreakDurationSeconds = rule.BreakDurationSeconds
            });
        }

        if (weights.Length == 0)
            return;

        var lastWeight = weights[weights.Length - 1];

        categories.Add(new CategorySeed
        {
            Division = division,
            Gender = gender,
            AgeMin = ageMin,
            AgeMax = ageMax,
            WeightMax = lastWeight,
            IsOpenWeight = true,
            SortOrder = weights.Length + 1,
            CategoryName = $"+{FormatWeight(lastWeight)} kg",
            RoundCount = rule.RoundCount,
            RoundDurationSeconds = rule.RoundDurationSeconds,
            BreakDurationSeconds = rule.BreakDurationSeconds
        });
    }

    private RoundRule GetRoundRule(string division)
    {
        return division switch
        {
            "U24" => new RoundRule(3, 180, 60),
            "Senior" => new RoundRule(3, 180, 60),
            "Masters" => new RoundRule(3, 120, 60),
            "Senior B" => new RoundRule(3, 120, 60),
            "U18" => new RoundRule(3, 120, 60),
            "U16" => new RoundRule(3, 120, 60),
            "U14" => new RoundRule(3, 90, 60),
            "U12" => new RoundRule(3, 60, 60),
            _ => new RoundRule(3, 120, 60)
        };
    }

    private string FormatWeight(double value)
    {
        return value % 1 == 0
            ? value.ToString("0")
            : value.ToString("0.##");
    }

    private class CategorySeed
    {
        public string Division { get; set; } = "";
        public string Gender { get; set; } = "";
        public int AgeMin { get; set; }
        public int AgeMax { get; set; }
        public double WeightMax { get; set; }
        public bool IsOpenWeight { get; set; }
        public int SortOrder { get; set; }
        public string CategoryName { get; set; } = "";
        public int RoundCount { get; set; }
        public int RoundDurationSeconds { get; set; }
        public int BreakDurationSeconds { get; set; }
    }

    private record RoundRule(
        int RoundCount,
        int RoundDurationSeconds,
        int BreakDurationSeconds);
}
