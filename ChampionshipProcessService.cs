using Microsoft.Data.Sqlite;
using MuaythaiApp.Database;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace MuaythaiApp;

public static class ChampionshipProcessService
{
    public static List<ChampionshipProcessEntry> LoadEntries(
        int dayNumber,
        bool useMatchRosterForDay = false,
        bool includeDocuments = true,
        bool includeGender = true)
    {
        EnsureLocalControlStorage();
        var championshipId = ChampionshipSettingsService.GetOrCreateActiveChampionshipId();

        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();

        var entries = useMatchRosterForDay && dayNumber > 1
            ? LoadScheduledAthletes(connection, championshipId, dayNumber)
            : LoadRegisteredAthletes(connection, dayNumber);
        var checks = LoadDailyChecks(connection, championshipId, dayNumber);
        var firstDayChecks = dayNumber > 1
            ? LoadDailyChecks(connection, championshipId, 1)
            : checks;

        foreach (var entry in entries)
        {
            entry.DayNumber = dayNumber;
            if (checks.TryGetValue(entry.FighterId, out var check))
            {
                entry.MeasuredWeightText = check.MeasuredWeight.HasValue
                    ? check.MeasuredWeight.Value.ToString("0.##", CultureInfo.InvariantCulture)
                    : string.Empty;
                if (dayNumber == 1)
                    ApplyDocumentChecks(entry, check);
            }

            if (dayNumber > 1 && firstDayChecks.TryGetValue(entry.FighterId, out var firstDayCheck))
                ApplyDocumentChecks(entry, firstDayCheck);

            entry.RequiresPolishCitizenship = RequiresPolishCitizenship();

            EvaluateEntry(entry, includeDocuments, includeGender);
        }

        return entries
            .OrderBy(x => x.AgeCategory)
            .ThenBy(x => x.WeightCategory)
            .ThenBy(x => x.Gender)
            .ThenBy(x => x.FighterName)
            .ToList();
    }

    public static void SaveEntries(int dayNumber, IEnumerable<ChampionshipProcessEntry> entries)
    {
        EnsureLocalControlStorage();
        var championshipId = ChampionshipSettingsService.GetOrCreateActiveChampionshipId();

        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();

        foreach (var entry in entries)
        {
            entry.RequiresPolishCitizenship = RequiresPolishCitizenship();
            EvaluateEntry(entry);

            var command = connection.CreateCommand();
            command.CommandText =
            @"
            INSERT INTO AthleteDailyChecks
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
            VALUES
            (
                @fighterId,
                @dayNumber,
                @championshipId,
                @measuredWeight,
                @genderConfirmed,
                @licensePresented,
                @medicalReportPresented,
                @identityPresented,
                @insurancePresented,
                @registrationFormPresented,
                @guardianConsentPresented,
                @seniorGuardianConsentPresented,
                @amateurLicense2026Presented,
                @polishCitizenshipConfirmed
            )
            ON CONFLICT(FighterId, DayNumber, ChampionshipId) DO UPDATE SET
                MeasuredWeight = excluded.MeasuredWeight,
                GenderConfirmed = excluded.GenderConfirmed,
                LicensePresented = excluded.LicensePresented,
                MedicalReportPresented = excluded.MedicalReportPresented,
                IdentityPresented = excluded.IdentityPresented,
                InsurancePresented = excluded.InsurancePresented,
                RegistrationFormPresented = excluded.RegistrationFormPresented,
                GuardianConsentPresented = excluded.GuardianConsentPresented,
                SeniorGuardianConsentPresented = excluded.SeniorGuardianConsentPresented,
                AmateurLicense2026Presented = excluded.AmateurLicense2026Presented,
                PolishCitizenshipConfirmed = excluded.PolishCitizenshipConfirmed
            ";
            command.Parameters.AddWithValue("@fighterId", entry.FighterId);
            command.Parameters.AddWithValue("@dayNumber", dayNumber);
            command.Parameters.AddWithValue("@championshipId", championshipId);
            command.Parameters.AddWithValue("@measuredWeight", ParseMeasuredWeight(entry.MeasuredWeightText) as object ?? DBNull.Value);
            command.Parameters.AddWithValue("@genderConfirmed", entry.GenderConfirmed ? 1 : 0);
            command.Parameters.AddWithValue("@licensePresented", entry.LicensePresented ? 1 : 0);
            command.Parameters.AddWithValue("@medicalReportPresented", entry.MedicalReportPresented ? 1 : 0);
            command.Parameters.AddWithValue("@identityPresented", entry.IdentityPresented ? 1 : 0);
            command.Parameters.AddWithValue("@insurancePresented", entry.InsurancePresented ? 1 : 0);
            command.Parameters.AddWithValue("@registrationFormPresented", entry.RegistrationFormPresented ? 1 : 0);
            command.Parameters.AddWithValue("@guardianConsentPresented", entry.GuardianConsentPresented ? 1 : 0);
            command.Parameters.AddWithValue("@seniorGuardianConsentPresented", entry.SeniorGuardianConsentPresented ? 1 : 0);
            command.Parameters.AddWithValue("@amateurLicense2026Presented", entry.AmateurLicense2026Presented ? 1 : 0);
            command.Parameters.AddWithValue("@polishCitizenshipConfirmed", entry.PolishCitizenshipConfirmed ? 1 : 0);
            command.ExecuteNonQuery();
        }
    }

    public static string ApplyDayControls(int dayNumber, bool updateMatches)
    {
        EnsureLocalControlStorage();
        var championshipId = ChampionshipSettingsService.GetOrCreateActiveChampionshipId();
        var shouldRebuildNextDay = false;
        var judgesCountForRebuild = 5;
        var affectedMatchCount = 0;
        var processedFighterCount = 0;

        using (var connection = DatabaseHelper.CreateConnection())
        {
            connection.Open();

            var entries = LoadEntries(
                dayNumber,
                useMatchRosterForDay: updateMatches && dayNumber > 1,
                includeDocuments: !updateMatches,
                includeGender: !updateMatches);
            var dqMap = entries.ToDictionary(
                x => x.FighterId,
                x => updateMatches ? IsScaleDisqualified(x) : x.IsDisqualified);
            processedFighterCount = dqMap.Count(x => x.Value);

            if (!updateMatches)
                return $"{processedFighterCount} athlete(s) flagged | 0 match(es) updated";

            var readMatches = connection.CreateCommand();
            readMatches.CommandText =
            @"
            SELECT
                Id,
                Fighter1Id,
                Fighter2Id,
                Fighter1Name,
                Fighter2Name,
                JudgesCount
            FROM Matches
            WHERE ChampionshipId = @championshipId
              AND DayNumber = @dayNumber
            ORDER BY OrderNo, Id
            ";
            readMatches.Parameters.AddWithValue("@championshipId", championshipId);
            readMatches.Parameters.AddWithValue("@dayNumber", dayNumber);

            using var reader = readMatches.ExecuteReader();
            var matches = new List<(int MatchId, int Fighter1Id, int Fighter2Id, string Fighter1Name, string Fighter2Name, int JudgesCount)>();
            while (reader.Read())
            {
                matches.Add((
                    reader.GetInt32(0),
                    reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    reader.IsDBNull(5) ? 5 : reader.GetInt32(5)));
            }

            foreach (var match in matches)
            {
                judgesCountForRebuild = match.JudgesCount is 3 or 5 ? match.JudgesCount : judgesCountForRebuild;

                var redDq = match.Fighter1Id > 0 && dqMap.TryGetValue(match.Fighter1Id, out var redDisqualified) && redDisqualified;
                var blueDq = match.Fighter2Id > 0 && dqMap.TryGetValue(match.Fighter2Id, out var blueDisqualified) && blueDisqualified;

                if (!redDq && !blueDq)
                    continue;

                var deleteScores = connection.CreateCommand();
                deleteScores.CommandText = "DELETE FROM JudgeScores WHERE MatchId = @matchId";
                deleteScores.Parameters.AddWithValue("@matchId", match.MatchId);
                deleteScores.ExecuteNonQuery();

                var deleteResult = connection.CreateCommand();
                deleteResult.CommandText = "DELETE FROM MatchResult WHERE MatchId = @matchId";
                deleteResult.Parameters.AddWithValue("@matchId", match.MatchId);
                deleteResult.ExecuteNonQuery();

                var method = redDq && blueDq ? "No Contest" : "Disq";
                var winner = "Red";

                if (redDq && blueDq)
                    winner = "Tie";
                else if (redDq)
                    winner = "Blue";
                else
                    winner = "Red";

                var insertResult = connection.CreateCommand();
                insertResult.CommandText =
                @"
                INSERT INTO MatchResult
                (
                    MatchId,
                    Winner,
                    Method,
                    Round,
                    JudgeRed,
                    JudgeBlue
                )
                VALUES
                (
                    @matchId,
                    @winner,
                    @method,
                    @round,
                    @judgeRed,
                    @judgeBlue
                )
                ";
                insertResult.Parameters.AddWithValue("@matchId", match.MatchId);
                insertResult.Parameters.AddWithValue("@winner", winner);
                insertResult.Parameters.AddWithValue("@method", method);
                insertResult.Parameters.AddWithValue("@round", 1);
                insertResult.Parameters.AddWithValue("@judgeRed", 0);
                insertResult.Parameters.AddWithValue("@judgeBlue", 0);
                insertResult.ExecuteNonQuery();

                affectedMatchCount++;
            }

            shouldRebuildNextDay = affectedMatchCount > 0;
        }

        if (shouldRebuildNextDay)
            TournamentProgressionService.RebuildNextDayFrom(dayNumber, judgesCountForRebuild);

        return $"{processedFighterCount} athlete(s) flagged | {affectedMatchCount} match(es) updated";
    }

    private static void EnsureLocalControlStorage()
    {
        var databaseHelper = new DatabaseHelper();
        databaseHelper.CreateDatabase();
    }

    private static bool IsScaleDisqualified(ChampionshipProcessEntry entry)
    {
        var measuredWeight = ParseMeasuredWeight(entry.MeasuredWeightText);
        if (!measuredWeight.HasValue)
            return false;

        if (entry.IsOpenWeight || !entry.AllowedWeightMax.HasValue)
            return false;

        return measuredWeight.Value > entry.AllowedWeightMax.Value;
    }

    private static void ApplyDocumentChecks(
        ChampionshipProcessEntry entry,
        (double? MeasuredWeight, bool GenderConfirmed, bool LicensePresented, bool MedicalReportPresented, bool IdentityPresented, bool InsurancePresented, bool RegistrationFormPresented, bool GuardianConsentPresented, bool SeniorGuardianConsentPresented, bool AmateurLicense2026Presented, bool PolishCitizenshipConfirmed) check)
    {
        entry.GenderConfirmed = check.GenderConfirmed;
        entry.LicensePresented = check.LicensePresented;
        entry.MedicalReportPresented = check.MedicalReportPresented;
        entry.IdentityPresented = check.IdentityPresented;
        entry.InsurancePresented = check.InsurancePresented;
        entry.RegistrationFormPresented = check.RegistrationFormPresented;
        entry.GuardianConsentPresented = check.GuardianConsentPresented;
        entry.SeniorGuardianConsentPresented = check.SeniorGuardianConsentPresented;
        entry.AmateurLicense2026Presented = check.AmateurLicense2026Presented;
        entry.PolishCitizenshipConfirmed = check.PolishCitizenshipConfirmed;
    }

    public static void EvaluateEntry(ChampionshipProcessEntry entry)
        => EvaluateEntry(entry, includeDocuments: true, includeGender: true);

    public static void EvaluateEntry(ChampionshipProcessEntry entry, bool includeDocuments, bool includeGender)
    {
        entry.IsDisqualified = false;
        entry.ScaleStatus = LocalizationService.CurrentLanguage == AppLanguage.Polish ? "Dopuszczony" : "Eligible";
        entry.ControlNote = LocalizationService.CurrentLanguage == AppLanguage.Polish ? "Komplet dokumentow" : "Passed";

        var missingDocuments = new List<string>();

        if (includeDocuments && entry.DayNumber <= 1 && !entry.MedicalReportPresented)
            missingDocuments.Add(LocalizationService.CurrentLanguage == AppLanguage.Polish
                ? "ksiazeczka lub zaswiadczenie sportowe"
                : "medical fitness");

        if (includeDocuments && entry.DayNumber <= 1 && !entry.InsurancePresented)
            missingDocuments.Add(LocalizationService.CurrentLanguage == AppLanguage.Polish
                ? "ubezpieczenie NNW"
                : "high-risk insurance");

        if (includeDocuments && entry.DayNumber <= 1 && !entry.RegistrationFormPresented)
            missingDocuments.Add(LocalizationService.CurrentLanguage == AppLanguage.Polish
                ? "formularz zgloszeniowy"
                : "entry form");

        if (includeDocuments && entry.DayNumber <= 1 && !entry.AmateurLicense2026Presented)
            missingDocuments.Add(LocalizationService.CurrentLanguage == AppLanguage.Polish
                ? "licencja amatorska 2026"
                : "amateur license 2026");

        if (includeDocuments && entry.DayNumber <= 1 && entry.RequiresGuardianConsent && !entry.GuardianConsentPresented)
            missingDocuments.Add(LocalizationService.CurrentLanguage == AppLanguage.Polish
                ? "zgoda rodzica lub opiekuna"
                : "guardian consent");

        if (includeDocuments && entry.DayNumber <= 1 && entry.RequiresSeniorGuardianConsent && !entry.SeniorGuardianConsentPresented)
            missingDocuments.Add(LocalizationService.CurrentLanguage == AppLanguage.Polish
                ? "zgoda na start w kategorii Senior"
                : "senior category consent");

        if (includeDocuments && entry.DayNumber <= 1 && entry.RequiresPolishCitizenship && !entry.PolishCitizenshipConfirmed)
            missingDocuments.Add(LocalizationService.CurrentLanguage == AppLanguage.Polish
                ? "obywatelstwo polskie"
                : "Polish citizenship");

        if (missingDocuments.Count > 0)
        {
            entry.IsDisqualified = true;
            entry.ScaleStatus = LocalizationService.CurrentLanguage == AppLanguage.Polish ? "Brak dokumentow" : "Documents Missing";
            entry.ControlNote = $"{(LocalizationService.CurrentLanguage == AppLanguage.Polish ? "Brakuje" : "Missing")}: {string.Join(", ", missingDocuments)}";
            return;
        }

        if (includeGender && entry.DayNumber <= 1 && !entry.GenderConfirmed)
        {
            entry.IsDisqualified = true;
            entry.ScaleStatus = LocalizationService.CurrentLanguage == AppLanguage.Polish ? "Zdyskwalifikowany" : "Disqualified";
            entry.ControlNote = LocalizationService.CurrentLanguage == AppLanguage.Polish
                ? "Kontrola plci nie zostala potwierdzona"
                : "Gender control not confirmed";
            return;
        }

        var measuredWeight = ParseMeasuredWeight(entry.MeasuredWeightText);
        if (!measuredWeight.HasValue)
        {
            entry.ScaleStatus = LocalizationService.CurrentLanguage == AppLanguage.Polish ? "Oczekuje" : "Pending";
            entry.ControlNote = LocalizationService.CurrentLanguage == AppLanguage.Polish
                ? "Oczekiwanie na wazenie zawodnika"
                : "Waiting for athlete scale";
            return;
        }

        if (entry.IsOpenWeight || !entry.AllowedWeightMax.HasValue)
        {
            entry.ScaleStatus = LocalizationService.CurrentLanguage == AppLanguage.Polish ? "Dopuszczony" : "Eligible";
            entry.ControlNote = LocalizationService.CurrentLanguage == AppLanguage.Polish
                ? "Kategoria open"
                : "Open weight category";
            return;
        }

        if (measuredWeight.Value > entry.AllowedWeightMax.Value)
        {
            entry.IsDisqualified = true;
            entry.ScaleStatus = LocalizationService.CurrentLanguage == AppLanguage.Polish ? "Zdyskwalifikowany" : "Disqualified";
            entry.ControlNote = LocalizationService.CurrentLanguage == AppLanguage.Polish
                ? $"Wazenie niezaliczone: {measuredWeight.Value:0.##} kg > {entry.AllowedWeightMax.Value:0.##} kg"
                : $"Scale control failed: {measuredWeight.Value:0.##} kg > {entry.AllowedWeightMax.Value:0.##} kg";
            return;
        }

        entry.ScaleStatus = LocalizationService.CurrentLanguage == AppLanguage.Polish ? "Dopuszczony" : "Eligible";
        entry.ControlNote = LocalizationService.CurrentLanguage == AppLanguage.Polish
            ? $"Kontrola zakonczona pozytywnie: {measuredWeight.Value:0.##} kg"
            : $"Scale control passed: {measuredWeight.Value:0.##} kg";
    }

    private static List<ChampionshipProcessEntry> LoadRegisteredAthletes(SqliteConnection connection, int dayNumber)
    {
        if (RemoteApiClient.IsEnabled)
        {
            return RemoteApiClient.GetFighters()
                .Select(fighter =>
                {
                    var weightCategory = fighter.WeightCategory ?? string.Empty;
                    return new ChampionshipProcessEntry
                    {
                        FighterId = fighter.Id,
                        DayNumber = dayNumber,
                        FighterName = fighter.FullName,
                        ClubName = fighter.ClubName,
                        Age = fighter.Age,
                        Gender = fighter.Gender,
                        AgeCategory = fighter.AgeCategory,
                        WeightCategory = weightCategory,
                        AllowedWeightText = weightCategory,
                        AllowedWeightMax = ParseAllowedWeightMax(weightCategory),
                        IsOpenWeight = weightCategory.TrimStart().StartsWith("+", StringComparison.OrdinalIgnoreCase)
                    };
                })
                .OrderBy(x => x.AgeCategory)
                .ThenBy(x => x.WeightCategory)
                .ThenBy(x => x.Gender)
                .ThenBy(x => x.FighterName)
                .ToList();
        }

        var command = connection.CreateCommand();
        command.CommandText =
        @"
        SELECT
            f.Id,
            TRIM(IFNULL(f.FirstName, '') || ' ' || IFNULL(f.LastName, '')),
            IFNULL(c.Name, ''),
            IFNULL(f.Age, 0),
            IFNULL(f.Gender, ''),
            IFNULL(f.AgeCategory, ''),
            IFNULL(f.WeightCategory, '')
        FROM Fighters f
        LEFT JOIN Clubs c
            ON c.Id = f.ClubId
        WHERE IFNULL(f.FirstName, '') <> '' OR IFNULL(f.LastName, '') <> ''
        ORDER BY f.AgeCategory, f.WeightCategory, f.Gender, f.LastName, f.FirstName
        ";
        command.Parameters.AddWithValue("@dayNumber", dayNumber);

        var entries = new List<ChampionshipProcessEntry>();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var weightCategory = reader.IsDBNull(6) ? string.Empty : reader.GetString(6);
            entries.Add(new ChampionshipProcessEntry
            {
                FighterId = reader.GetInt32(0),
                DayNumber = dayNumber,
                FighterName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                ClubName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Age = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                Gender = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                AgeCategory = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                WeightCategory = weightCategory,
                AllowedWeightText = weightCategory,
                AllowedWeightMax = ParseAllowedWeightMax(weightCategory),
                IsOpenWeight = weightCategory.TrimStart().StartsWith("+", StringComparison.OrdinalIgnoreCase)
            });
        }

        return entries;
    }

    private static List<ChampionshipProcessEntry> LoadScheduledAthletes(SqliteConnection connection, int championshipId, int dayNumber)
    {
        var command = connection.CreateCommand();
        command.CommandText =
        @"
        SELECT
            scheduled.FighterId,
            TRIM(IFNULL(f.FirstName, '') || ' ' || IFNULL(f.LastName, '')),
            scheduled.FighterName,
            IFNULL(c.Name, ''),
            IFNULL(f.Age, 0),
            scheduled.Gender,
            scheduled.AgeCategory,
            scheduled.WeightCategory,
            scheduled.OrderNo,
            scheduled.CornerOrder
        FROM (
            SELECT
                Fighter1Id AS FighterId,
                Fighter1Name AS FighterName,
                AgeCategory,
                WeightCategory,
                Gender,
                OrderNo,
                1 AS CornerOrder
            FROM Matches
            WHERE ChampionshipId = @championshipId
              AND DayNumber = @dayNumber
              AND Fighter1Id IS NOT NULL
              AND Fighter1Id > 0

            UNION ALL

            SELECT
                Fighter2Id AS FighterId,
                Fighter2Name AS FighterName,
                AgeCategory,
                WeightCategory,
                Gender,
                OrderNo,
                2 AS CornerOrder
            FROM Matches
            WHERE ChampionshipId = @championshipId
              AND DayNumber = @dayNumber
              AND Fighter2Id IS NOT NULL
              AND Fighter2Id > 0
              AND IFNULL(Fighter2Name, '') <> 'BYE'
        ) scheduled
        LEFT JOIN Fighters f
            ON f.Id = scheduled.FighterId
        LEFT JOIN Clubs c
            ON c.Id = f.ClubId
        ORDER BY scheduled.AgeCategory, scheduled.WeightCategory, scheduled.Gender, scheduled.OrderNo, scheduled.CornerOrder
        ";
        command.Parameters.AddWithValue("@championshipId", championshipId);
        command.Parameters.AddWithValue("@dayNumber", dayNumber);

        var entries = new List<ChampionshipProcessEntry>();
        var addedFighterIds = new HashSet<int>();

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var fighterId = reader.GetInt32(0);
            if (!addedFighterIds.Add(fighterId))
                continue;

            var databaseName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var matchName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            var weightCategory = reader.IsDBNull(7) ? string.Empty : reader.GetString(7);
            entries.Add(new ChampionshipProcessEntry
            {
                FighterId = fighterId,
                DayNumber = dayNumber,
                FighterName = string.IsNullOrWhiteSpace(databaseName) ? matchName : databaseName,
                ClubName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Age = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                Gender = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                AgeCategory = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                WeightCategory = weightCategory,
                AllowedWeightText = weightCategory,
                AllowedWeightMax = ParseAllowedWeightMax(weightCategory),
                IsOpenWeight = weightCategory.TrimStart().StartsWith("+", StringComparison.OrdinalIgnoreCase)
            });
        }

        return entries;
    }

    private static Dictionary<int, (double? MeasuredWeight, bool GenderConfirmed, bool LicensePresented, bool MedicalReportPresented, bool IdentityPresented, bool InsurancePresented, bool RegistrationFormPresented, bool GuardianConsentPresented, bool SeniorGuardianConsentPresented, bool AmateurLicense2026Presented, bool PolishCitizenshipConfirmed)> LoadDailyChecks(SqliteConnection connection, int championshipId, int dayNumber)
    {
        var command = connection.CreateCommand();
        command.CommandText =
        @"
        SELECT
            FighterId,
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
        FROM AthleteDailyChecks
        WHERE ChampionshipId = @championshipId
          AND DayNumber = @dayNumber
        ";
        command.Parameters.AddWithValue("@championshipId", championshipId);
        command.Parameters.AddWithValue("@dayNumber", dayNumber);

        var result = new Dictionary<int, (double? MeasuredWeight, bool GenderConfirmed, bool LicensePresented, bool MedicalReportPresented, bool IdentityPresented, bool InsurancePresented, bool RegistrationFormPresented, bool GuardianConsentPresented, bool SeniorGuardianConsentPresented, bool AmateurLicense2026Presented, bool PolishCitizenshipConfirmed)>();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            result[reader.GetInt32(0)] = (
                reader.IsDBNull(1) ? null : reader.GetDouble(1),
                reader.IsDBNull(2) || reader.GetInt32(2) == 1,
                !reader.IsDBNull(3) && reader.GetInt32(3) == 1,
                !reader.IsDBNull(4) && reader.GetInt32(4) == 1,
                !reader.IsDBNull(5) && reader.GetInt32(5) == 1,
                !reader.IsDBNull(6) && reader.GetInt32(6) == 1,
                !reader.IsDBNull(7) && reader.GetInt32(7) == 1,
                !reader.IsDBNull(8) && reader.GetInt32(8) == 1,
                !reader.IsDBNull(9) && reader.GetInt32(9) == 1,
                !reader.IsDBNull(10) && reader.GetInt32(10) == 1,
                !reader.IsDBNull(11) && reader.GetInt32(11) == 1);
        }

        return result;
    }

    private static bool RequiresPolishCitizenship()
    {
        var name = ChampionshipSettingsService.GetChampionshipName();
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var normalized = name.Trim().ToLowerInvariant();
        return normalized.Contains("mistrzostw polski") ||
               normalized.Contains("mistrzostwa polski") ||
               normalized.Contains("polish championship");
    }

    private static double? ParseMeasuredWeight(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant))
            return invariant;

        if (double.TryParse(value.Trim(), out var local))
            return local;

        return null;
    }

    private static double? ParseAllowedWeightMax(string? weightCategory)
    {
        if (string.IsNullOrWhiteSpace(weightCategory))
            return null;

        var match = Regex.Match(weightCategory, @"(\d+(?:[.,]\d+)?)");
        if (!match.Success)
            return null;

        var text = match.Groups[1].Value.Replace(',', '.');
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}
