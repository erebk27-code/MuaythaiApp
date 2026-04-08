using Microsoft.Data.Sqlite;
using MuaythaiApp.Database;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MuaythaiApp;

public static class TournamentProgressionService
{
    public static void RebuildNextDayFrom(int currentDay, int judgesCount)
    {
        if (currentDay >= 4)
            return;

        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();

        DeleteMatchesFromDay(connection, currentDay + 1);

        var advancingFighters = LoadAdvancingFighters(connection, currentDay);
        var nextDay = currentDay + 1;
        var orderNo = 1;

        foreach (var group in advancingFighters
                     .GroupBy(x => new MatchGroupKey(x.AgeCategory, x.WeightCategory, x.Gender))
                     .OrderBy(x => x.Key.AgeCategory)
                     .ThenBy(x => x.Key.WeightCategory)
                     .ThenBy(x => x.Key.Gender))
        {
            var orderedGroup = group
                .OrderBy(x => x.SourceBoutNo)
                .ThenBy(x => x.FighterName)
                .ToList();

            var pairing = BuildBestPairing(orderedGroup);

            foreach (var pair in pairing.Pairs)
            {
                InsertMatch(connection, pair.RedCorner, pair.BlueCorner, nextDay, orderNo, judgesCount);
                orderNo++;
            }
        }
    }

    private static void DeleteMatchesFromDay(SqliteConnection connection, int dayNumber)
    {
        var matchIds = new List<int>();
        var read = connection.CreateCommand();
        read.CommandText =
        @"
        SELECT Id
        FROM Matches
        WHERE DayNumber >= @dayNumber
        ";
        read.Parameters.AddWithValue("@dayNumber", dayNumber);

        using (var reader = read.ExecuteReader())
        {
            while (reader.Read())
                matchIds.Add(reader.GetInt32(0));
        }

        foreach (var matchId in matchIds)
        {
            var deleteScores = connection.CreateCommand();
            deleteScores.CommandText = "DELETE FROM JudgeScores WHERE MatchId = @matchId";
            deleteScores.Parameters.AddWithValue("@matchId", matchId);
            deleteScores.ExecuteNonQuery();

            var deleteResult = connection.CreateCommand();
            deleteResult.CommandText = "DELETE FROM MatchResult WHERE MatchId = @matchId";
            deleteResult.Parameters.AddWithValue("@matchId", matchId);
            deleteResult.ExecuteNonQuery();
        }

        var deleteMatches = connection.CreateCommand();
        deleteMatches.CommandText = "DELETE FROM Matches WHERE DayNumber >= @dayNumber";
        deleteMatches.Parameters.AddWithValue("@dayNumber", dayNumber);
        deleteMatches.ExecuteNonQuery();
    }

    private static List<AdvancingFighter> LoadAdvancingFighters(SqliteConnection connection, int currentDay)
    {
        var fighters = new List<AdvancingFighter>();

        var command = connection.CreateCommand();
        command.CommandText =
        @"
        SELECT
            m.Id,
            m.OrderNo,
            m.AgeCategory,
            m.WeightCategory,
            m.Gender,
            m.Fighter1Id,
            m.Fighter2Id,
            m.Fighter1Name,
            m.Fighter2Name,
            mr.Winner,
            fr.ClubId AS RedClubId,
            fb.ClubId AS BlueClubId,
            cr.Name AS RedClubName,
            cb.Name AS BlueClubName
        FROM Matches m
        LEFT JOIN MatchResult mr
            ON mr.MatchId = m.Id
        LEFT JOIN Fighters fr
            ON fr.Id = m.Fighter1Id
        LEFT JOIN Fighters fb
            ON fb.Id = m.Fighter2Id
        LEFT JOIN Clubs cr
            ON cr.Id = fr.ClubId
        LEFT JOIN Clubs cb
            ON cb.Id = fb.ClubId
        WHERE m.DayNumber = @dayNumber
        ORDER BY m.OrderNo, m.Id
        ";
        command.Parameters.AddWithValue("@dayNumber", currentDay);

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var winner = reader.IsDBNull(9) ? string.Empty : reader.GetString(9);
            var redFighterId = reader.IsDBNull(5) ? 0 : reader.GetInt32(5);
            var blueFighterId = reader.IsDBNull(6) ? 0 : reader.GetInt32(6);
            var redName = reader.IsDBNull(7) ? string.Empty : reader.GetString(7);
            var blueName = reader.IsDBNull(8) ? string.Empty : reader.GetString(8);
            var hasBye = blueFighterId <= 0 || string.Equals(blueName, "BYE", StringComparison.OrdinalIgnoreCase);

            if (string.Equals(winner, "Red", StringComparison.OrdinalIgnoreCase))
            {
                fighters.Add(BuildAdvancingFighter(reader, true));
                continue;
            }

            if (string.Equals(winner, "Blue", StringComparison.OrdinalIgnoreCase))
            {
                fighters.Add(BuildAdvancingFighter(reader, false));
                continue;
            }

            if (hasBye && redFighterId > 0)
                fighters.Add(BuildAdvancingFighter(reader, true));
        }

        return fighters;
    }

    private static AdvancingFighter BuildAdvancingFighter(SqliteDataReader reader, bool useRedCorner)
    {
        return new AdvancingFighter
        {
            SourceMatchId = reader.GetInt32(0),
            SourceBoutNo = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
            AgeCategory = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            WeightCategory = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            Gender = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            FighterId = useRedCorner
                ? (reader.IsDBNull(5) ? 0 : reader.GetInt32(5))
                : (reader.IsDBNull(6) ? 0 : reader.GetInt32(6)),
            FighterName = useRedCorner
                ? (reader.IsDBNull(7) ? string.Empty : reader.GetString(7))
                : (reader.IsDBNull(8) ? string.Empty : reader.GetString(8)),
            ClubId = useRedCorner
                ? (reader.IsDBNull(10) ? 0 : reader.GetInt32(10))
                : (reader.IsDBNull(11) ? 0 : reader.GetInt32(11)),
            ClubName = useRedCorner
                ? (reader.IsDBNull(12) ? string.Empty : reader.GetString(12))
                : (reader.IsDBNull(13) ? string.Empty : reader.GetString(13))
        };
    }

    private static PairingResult BuildBestPairing(List<AdvancingFighter> fighters)
    {
        if (fighters.Count < 2)
            return new PairingResult();

        var first = fighters[0];
        PairingResult? best = null;

        for (int i = 1; i < fighters.Count; i++)
        {
            var opponent = fighters[i];

            if (!CanFightEachOther(first, opponent))
                continue;

            var remaining = fighters
                .Where((_, index) => index != 0 && index != i)
                .ToList();

            var candidate = BuildBestPairing(remaining);
            candidate.Pairs.Insert(0, new MatchPair(first, opponent));

            if (best == null || candidate.Pairs.Count > best.Pairs.Count)
                best = candidate;
        }

        var skippedFirst = BuildBestPairing(fighters.Skip(1).ToList());

        if (best == null || skippedFirst.Pairs.Count > best.Pairs.Count)
            return skippedFirst;

        return best;
    }

    private static bool CanFightEachOther(AdvancingFighter first, AdvancingFighter second)
    {
        if (first.FighterId <= 0 || second.FighterId <= 0)
            return false;

        if (first.ClubId <= 0 || second.ClubId <= 0)
            return false;

        return first.ClubId != second.ClubId;
    }

    private static void InsertMatch(
        SqliteConnection connection,
        AdvancingFighter redCorner,
        AdvancingFighter blueCorner,
        int dayNumber,
        int orderNo,
        int judgesCount)
    {
        var command = connection.CreateCommand();
        command.CommandText =
        @"
        INSERT INTO Matches
        (
            Fighter1Id,
            Fighter2Id,
            Fighter1Name,
            Fighter2Name,
            AgeCategory,
            WeightCategory,
            Gender,
            CategoryGroup,
            OrderNo,
            JudgesCount,
            DayNumber
        )
        VALUES
        (
            @fighter1Id,
            @fighter2Id,
            @fighter1Name,
            @fighter2Name,
            @ageCategory,
            @weightCategory,
            @gender,
            @categoryGroup,
            @orderNo,
            @judgesCount,
            @dayNumber
        )
        ";

        command.Parameters.AddWithValue("@fighter1Id", redCorner.FighterId);
        command.Parameters.AddWithValue("@fighter2Id", blueCorner.FighterId);
        command.Parameters.AddWithValue("@fighter1Name", redCorner.FighterName);
        command.Parameters.AddWithValue("@fighter2Name", blueCorner.FighterName);
        command.Parameters.AddWithValue("@ageCategory", redCorner.AgeCategory);
        command.Parameters.AddWithValue("@weightCategory", redCorner.WeightCategory);
        command.Parameters.AddWithValue("@gender", redCorner.Gender);
        command.Parameters.AddWithValue("@categoryGroup",
            $"{redCorner.AgeCategory}_{redCorner.WeightCategory}_{redCorner.Gender}");
        command.Parameters.AddWithValue("@orderNo", orderNo);
        command.Parameters.AddWithValue("@judgesCount", judgesCount);
        command.Parameters.AddWithValue("@dayNumber", dayNumber);
        command.ExecuteNonQuery();
    }

    private sealed class AdvancingFighter
    {
        public int SourceMatchId { get; set; }
        public int SourceBoutNo { get; set; }
        public int FighterId { get; set; }
        public int ClubId { get; set; }
        public string FighterName { get; set; } = string.Empty;
        public string ClubName { get; set; } = string.Empty;
        public string AgeCategory { get; set; } = string.Empty;
        public string WeightCategory { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
    }

    private sealed record MatchGroupKey(string AgeCategory, string WeightCategory, string Gender);

    private sealed record MatchPair(AdvancingFighter RedCorner, AdvancingFighter BlueCorner);

    private sealed class PairingResult
    {
        public List<MatchPair> Pairs { get; } = new();
    }
}
