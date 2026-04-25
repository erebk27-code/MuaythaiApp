using MuaythaiApp.Database;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MuaythaiApp;

public static class MedalTableService
{
    public static List<MedalAward> BuildAwards()
    {
        var fighters = LoadFighters();
        var matches = LoadAllMatches();
        var awards = new List<MedalAward>();

        foreach (var group in matches
                     .GroupBy(x => new MatchKey(x.AgeCategory, x.WeightCategory, x.Gender))
                     .OrderBy(x => x.Key.AgeCategory)
                     .ThenBy(x => x.Key.WeightCategory)
                     .ThenBy(x => x.Key.Gender))
        {
            var latestScheduledMatch = group
                .OrderByDescending(x => x.DayNumber)
                .ThenByDescending(x => x.OrderNo)
                .FirstOrDefault();

            if (latestScheduledMatch == null || !latestScheduledMatch.HasResult)
                continue;

            var finalMatch = latestScheduledMatch;

            var goldName = finalMatch.Winner == "Red"
                ? finalMatch.Fighter1Name
                : finalMatch.Winner == "Blue"
                    ? finalMatch.Fighter2Name
                    : string.Empty;

            var silverName = finalMatch.Winner == "Red"
                ? finalMatch.Fighter2Name
                : finalMatch.Winner == "Blue"
                    ? finalMatch.Fighter1Name
                    : string.Empty;

            if (!string.IsNullOrWhiteSpace(goldName) &&
                !string.Equals(goldName, "BYE", StringComparison.OrdinalIgnoreCase))
            {
                awards.Add(CreateAward("Gold", goldName, group.Key, fighters));
            }

            if (!string.IsNullOrWhiteSpace(silverName) &&
                !string.Equals(silverName, "BYE", StringComparison.OrdinalIgnoreCase))
            {
                awards.Add(CreateAward("Silver", silverName, group.Key, fighters));
            }

            var finalistNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(finalMatch.Fighter1Name))
                finalistNames.Add(finalMatch.Fighter1Name);
            if (!string.IsNullOrWhiteSpace(finalMatch.Fighter2Name))
                finalistNames.Add(finalMatch.Fighter2Name);

            var bronzeMatches = group
                .Where(x => x.HasResult)
                .Where(x => x.DayNumber == finalMatch.DayNumber - 1)
                .Where(x =>
                {
                    var winnerName = x.Winner == "Red" ? x.Fighter1Name : x.Winner == "Blue" ? x.Fighter2Name : "";
                    return finalistNames.Contains(winnerName);
                })
                .ToList();

            foreach (var bronzeMatch in bronzeMatches)
            {
                var bronzeName = bronzeMatch.Winner == "Red"
                    ? bronzeMatch.Fighter2Name
                    : bronzeMatch.Winner == "Blue"
                        ? bronzeMatch.Fighter1Name
                        : string.Empty;

                if (string.IsNullOrWhiteSpace(bronzeName) ||
                    string.Equals(bronzeName, "BYE", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                awards.Add(CreateAward("Bronze", bronzeName, group.Key, fighters));
            }
        }

        return awards
            .GroupBy(x => $"{x.Medal}|{x.FighterName}|{x.Category}|{x.WeightClass}|{x.Gender}")
            .Select(x => x.First())
            .ToList();
    }

    public static List<MedalStanding> BuildStandings(IEnumerable<MedalAward> awards)
    {
        return awards
            .GroupBy(x => x.ClubName)
            .Select(group => new MedalStanding
            {
                ClubName = group.Key,
                Gold = group.Count(x => x.Medal == "Gold"),
                Silver = group.Count(x => x.Medal == "Silver"),
                Bronze = group.Count(x => x.Medal == "Bronze")
            })
            .OrderByDescending(x => x.Gold)
            .ThenByDescending(x => x.Silver)
            .ThenByDescending(x => x.Bronze)
            .ThenBy(x => x.ClubName)
            .ToList();
    }

    public static string BuildCategoryLabel(string category, string weightClass, string gender)
    {
        return $"{category} | {weightClass} | {gender}".Trim();
    }

    private static Dictionary<string, Fighter> LoadFighters()
    {
        var fighters = new Dictionary<string, Fighter>(StringComparer.OrdinalIgnoreCase);

        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText =
        @"
        SELECT
            f.Id,
            f.FirstName,
            f.LastName,
            f.ClubId,
            c.Name
        FROM Fighters f
        LEFT JOIN Clubs c
            ON c.Id = f.ClubId
        ";

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var fighter = new Fighter
            {
                Id = reader.GetInt32(0),
                FirstName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                LastName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                ClubId = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                ClubName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
            };

            if (!fighters.ContainsKey(fighter.FullName))
                fighters[fighter.FullName] = fighter;

            if (!fighters.ContainsKey(fighter.FirstName))
                fighters[fighter.FirstName] = fighter;
        }

        return fighters;
    }

    private static List<CompletedMatch> LoadAllMatches()
    {
        var matches = new List<CompletedMatch>();

        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();
        var championshipId = ChampionshipSettingsService.GetOrCreateActiveChampionshipId();

        var command = connection.CreateCommand();
        command.CommandText =
        @"
        SELECT
            m.DayNumber,
            m.OrderNo,
            m.Fighter1Name,
            m.Fighter2Name,
            m.AgeCategory,
            m.WeightCategory,
            m.Gender,
            mr.Winner
        FROM Matches m
        LEFT JOIN MatchResult mr
            ON mr.MatchId = m.Id
        WHERE m.ChampionshipId = @championshipId
        ORDER BY m.DayNumber, m.OrderNo, m.Id
        ";
        command.Parameters.AddWithValue("@championshipId", championshipId);

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            matches.Add(new CompletedMatch
            {
                DayNumber = reader.IsDBNull(0) ? 1 : reader.GetInt32(0),
                OrderNo = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                Fighter1Name = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Fighter2Name = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                AgeCategory = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                WeightCategory = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                Gender = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                Winner = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                HasResult = !reader.IsDBNull(7) && !string.IsNullOrWhiteSpace(reader.GetString(7))
            });
        }

        return matches;
    }

    private static MedalAward CreateAward(
        string medal,
        string fighterName,
        MatchKey matchKey,
        Dictionary<string, Fighter> fighters)
    {
        fighters.TryGetValue(fighterName, out var fighter);

        return new MedalAward
        {
            Medal = medal,
            FighterName = fighterName,
            ClubName = fighter?.ClubName ?? "-",
            Category = matchKey.AgeCategory,
            WeightClass = matchKey.WeightCategory,
            Gender = matchKey.Gender
        };
    }

    private sealed class CompletedMatch
    {
        public int DayNumber { get; set; }
        public int OrderNo { get; set; }
        public string Fighter1Name { get; set; } = "";
        public string Fighter2Name { get; set; } = "";
        public string AgeCategory { get; set; } = "";
        public string WeightCategory { get; set; } = "";
        public string Gender { get; set; } = "";
        public string Winner { get; set; } = "";
        public bool HasResult { get; set; }
    }

    private sealed record MatchKey(string AgeCategory, string WeightCategory, string Gender);
}
