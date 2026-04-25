using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaLine = Avalonia.Controls.Shapes.Line;
using AvaloniaRectangle = Avalonia.Controls.Shapes.Rectangle;
using MuaythaiApp.Database;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace MuaythaiApp;

public partial class ReportsWindow : Window
{
    private const string AllRingsLabel = "All";

    private readonly List<string> reportTypes = new()
    {
        "Fighter List",
        "Category List",
        "Match List",
        "Medal Table",
        "Match Draw"
    };

    private ReportDefinition? currentReport;
    private BracketReportDefinition? currentBracketReport;
    private MatchScheduleReportDefinition? currentScheduleReport;
    private readonly string generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    private readonly DatabaseAutoRefresh databaseAutoRefresh;

    public ReportsWindow()
    {
        InitializeComponent();
        databaseAutoRefresh = new DatabaseAutoRefresh(this, LoadSelectedReport);
        TournamentTitleBox.Text = ChampionshipSettingsService.GetChampionshipName();
        ReportDayCombo.ItemsSource = Enumerable.Range(1, ChampionshipSettingsService.GetDayCount()).Select(day => $"Day {day}").ToList();
        ReportDayCombo.SelectedIndex = 0;
        ReportDateBox.Text = ChampionshipSettingsService.GetDateLabelForDay(1);
        RingCombo.ItemsSource = GetRingFilterItems();
        RingCombo.SelectedIndex = 0;
        ReportTypeCombo.ItemsSource = reportTypes;
        ReportTypeCombo.SelectedIndex = 0;
        Opened += (_, __) => LoadSelectedReport();
        Activated += (_, __) => LoadSelectedReport();
        LocalizationService.LanguageChanged += ApplyLocalization;
        Closed += (_, __) => LocalizationService.LanguageChanged -= ApplyLocalization;
        ApplyLocalization();
    }

    private void ReportChanged(object? sender, SelectionChangedEventArgs e)
    {
        ReportDateBox.Text = ChampionshipSettingsService.GetDateLabelForDay(ParseSelectedReportDayNumber());
        LoadSelectedReport();
    }

    private void RefreshClick(object? sender, RoutedEventArgs e)
    {
        LoadSelectedReport();
    }

    private void TournamentTitleChanged(object? sender, TextChangedEventArgs e)
    {
        LoadSelectedReport();
    }

    private void ExportCsvClick(object? sender, RoutedEventArgs e)
    {
        if (currentBracketReport != null)
        {
            SummaryText.Text = "CSV export is not available for Match Draw. Use PDF or Print.";
            return;
        }

        if (currentScheduleReport != null)
        {
            var scheduleCsvPath = System.IO.Path.Combine(GetReportsDirectory(), $"{BuildReportFilePrefix(currentScheduleReport.Name)}.csv");
            var scheduleLines = new List<string>
            {
                EscapeCsv(GetTournamentTitle()),
                EscapeCsv(currentScheduleReport.ReportDate),
                EscapeCsv(currentScheduleReport.RingName),
                EscapeCsv($"Day {currentScheduleReport.DayNumber}"),
                string.Empty,
                "\"Lp.\",\"Stage\",\"Age Category\",\"Weight Category\",\"Red Name\",\"Red Club\",\"Blue Name\",\"Blue Club\",\"Result\""
            };

            scheduleLines.AddRange(currentScheduleReport.Rows.Select(row => string.Join(",",
                EscapeCsv(row.OrderNumber.ToString(CultureInfo.InvariantCulture)),
                EscapeCsv(row.Stage),
                EscapeCsv(row.AgeCategory),
                EscapeCsv(row.WeightCategory),
                EscapeCsv(row.RedName),
                EscapeCsv(row.RedClub),
                EscapeCsv(row.BlueName),
                EscapeCsv(row.BlueClub),
                EscapeCsv(row.Result))));
            File.WriteAllLines(scheduleCsvPath, scheduleLines, Encoding.UTF8);
            SummaryText.Text = $"{currentScheduleReport.Summary} | CSV saved: {scheduleCsvPath}";
            return;
        }

        if (currentReport == null)
            return;

        var path = System.IO.Path.Combine(GetReportsDirectory(), $"{BuildReportFilePrefix(currentReport.Name)}.csv");
        var lines = new List<string>
        {
            EscapeCsv($"Generated At: {generatedAt}"),
            EscapeCsv($"Tournament: {GetTournamentTitle()}"),
            string.Empty,
            string.Join(",", currentReport.Columns.Select(EscapeCsv))
        };
        lines.AddRange(currentReport.Rows.Select(row => string.Join(",", row.Values.Select(EscapeCsv))));
        File.WriteAllLines(path, lines, Encoding.UTF8);
        SummaryText.Text = $"{currentReport.Summary} | CSV saved: {path}";
    }

    private void ExportPdfClick(object? sender, RoutedEventArgs e)
    {
        if (currentBracketReport != null)
        {
            var bracketPdfPath = System.IO.Path.Combine(GetReportsDirectory(), $"{BuildReportFilePrefix(currentBracketReport.Name)}.pdf");
            SimplePdfExporter.WriteBracketPdf(
                bracketPdfPath,
                currentBracketReport.Name,
                currentBracketReport.Categories,
                GetTournamentTitle(),
                generatedAt);
            SummaryText.Text = $"{currentBracketReport.Summary} | PDF saved: {bracketPdfPath}";
            return;
        }

        if (currentScheduleReport != null)
        {
            var schedulePdfPath = System.IO.Path.Combine(GetReportsDirectory(), $"{BuildReportFilePrefix(currentScheduleReport.Name)}.pdf");
            SimplePdfExporter.WriteTablePdf(
                schedulePdfPath,
                currentScheduleReport.Name,
                new[]
                {
                    "Lp.", "Stage", "Age Category", "Weight", "Red Corner", "Blue Corner", "Result"
                },
                currentScheduleReport.Rows.Select(row => new ReportRow
                {
                    Values = new[]
                    {
                        row.OrderNumber.ToString(CultureInfo.InvariantCulture),
                        row.Stage,
                        row.AgeCategory,
                        row.WeightCategory,
                        BuildPdfFighterCell(row.RedName, row.RedClub),
                        BuildPdfFighterCell(row.BlueName, row.BlueClub),
                        row.Result
                    }
                }).ToList(),
                $"{GetTournamentTitle()} | {currentScheduleReport.ReportDate} | {currentScheduleReport.RingName} | Day {currentScheduleReport.DayNumber}",
                generatedAt);
            SummaryText.Text = $"{currentScheduleReport.Summary} | PDF saved: {schedulePdfPath}";
            return;
        }

        if (currentReport == null)
            return;

        var path = System.IO.Path.Combine(GetReportsDirectory(), $"{BuildReportFilePrefix(currentReport.Name)}.pdf");
        SimplePdfExporter.WriteTablePdf(
            path,
            currentReport.Name,
            currentReport.Columns,
            currentReport.Rows,
            GetTournamentTitle(),
            generatedAt);
        SummaryText.Text = $"{currentReport.Summary} | PDF saved: {path}";
    }

    private void PrintClick(object? sender, RoutedEventArgs e)
    {
        if (currentBracketReport == null && currentScheduleReport == null && currentReport == null)
            return;

        var reportName = currentBracketReport?.Name ?? currentScheduleReport?.Name ?? currentReport?.Name ?? "Report";
        var path = System.IO.Path.Combine(GetReportsDirectory(), $"{BuildReportFilePrefix(reportName)}_print.html");
        var html = currentBracketReport != null
            ? BuildBracketHtml(currentBracketReport, autoPrint: true)
            : currentScheduleReport != null
                ? BuildScheduleHtml(currentScheduleReport, autoPrint: true)
                : BuildHtml(currentReport!, autoPrint: true);
        File.WriteAllText(path, html, Encoding.UTF8);

        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        };

        Process.Start(startInfo);
        var summary = currentBracketReport?.Summary ?? currentScheduleReport?.Summary ?? currentReport?.Summary ?? "Report opened";
        SummaryText.Text = $"{summary} | Print file opened: {path}";
    }

    private static string BuildPdfFighterCell(string fighterName, string clubName)
    {
        if (string.IsNullOrWhiteSpace(clubName))
            return fighterName;

        if (string.IsNullOrWhiteSpace(fighterName))
            return clubName;

        return $"{fighterName} / {clubName}";
    }

    private void LoadSelectedReport()
    {
        var selectedReportType = ReportTypeCombo.SelectedItem?.ToString() ?? reportTypes[0];

        if (selectedReportType == "Match Draw")
        {
            currentReport = null;
            currentScheduleReport = null;
            currentBracketReport = BuildBracketReport();
            PreviewTextBox.IsVisible = false;
            BracketScrollViewer.IsVisible = true;
            ScheduleScrollViewer.IsVisible = false;
            ExportCsvButton.IsEnabled = false;
            RenderBracketPreview(currentBracketReport);
            SummaryText.Text = currentBracketReport.Summary;
            UpdateFilterVisibility(selectedReportType);
            return;
        }

        if (selectedReportType == "Match List")
        {
            currentReport = null;
            currentBracketReport = null;
            currentScheduleReport = BuildMatchScheduleReport();
            PreviewTextBox.IsVisible = false;
            BracketScrollViewer.IsVisible = false;
            ScheduleScrollViewer.IsVisible = true;
            ExportCsvButton.IsEnabled = true;
            RenderSchedulePreview(currentScheduleReport);
            SummaryText.Text = currentScheduleReport.Summary;
            UpdateFilterVisibility(selectedReportType);
            return;
        }

        currentBracketReport = null;
        currentScheduleReport = null;
        currentReport = BuildReport(selectedReportType);
        PreviewTextBox.IsVisible = true;
        BracketScrollViewer.IsVisible = false;
        ScheduleScrollViewer.IsVisible = false;
        ExportCsvButton.IsEnabled = true;
        PreviewTextBox.Text = BuildPreview(currentReport);
        SummaryText.Text = currentReport.Summary;
        UpdateFilterVisibility(selectedReportType);
    }

    private void ApplyLocalization()
    {
        Title = LocalizationService.T("ReportsTitle");
        ReportsTitleText.Text = LocalizationService.T("ReportsTitle");
        TournamentTitleLabelText.Text = LocalizationService.T("TournamentTitle");
        ReportTypeLabelText.Text = LocalizationService.T("ReportType");
        ReportDayLabelText.Text = LocalizationService.T("Day");
        ReportDateLabelText.Text = LocalizationService.T("Date");
        RingLabelText.Text = LocalizationService.T("Ring");
        RefreshButton.Content = LocalizationService.T("Refresh");
        ExportCsvButton.Content = LocalizationService.T("ExportCsv");
        ExportPdfButton.Content = LocalizationService.T("ExportPdf");
        PrintButton.Content = LocalizationService.T("Print");
        ReportDayCombo.ItemsSource = Enumerable.Range(1, ChampionshipSettingsService.GetDayCount()).Select(day => $"{LocalizationService.T("Day")} {day}").ToList();
        LocalizationService.LocalizeControlTree(this);
        if (ReportDayCombo.SelectedIndex < 0)
            ReportDayCombo.SelectedIndex = 0;
        var selectedRing = RingCombo.SelectedItem?.ToString();
        RingCombo.ItemsSource = GetRingFilterItems();
        if (!string.IsNullOrWhiteSpace(selectedRing) &&
            GetRingFilterItems().Contains(selectedRing))
        {
            RingCombo.SelectedItem = selectedRing;
        }
        if (RingCombo.SelectedIndex < 0)
            RingCombo.SelectedIndex = 0;
    }

    private ReportDefinition BuildReport(string reportType)
    {
        return reportType switch
        {
            "Category List" => BuildCategoryReport(),
            "Match List" => BuildMatchReport(),
            "Medal Table" => BuildMedalReport(),
            _ => BuildFighterReport()
        };
    }

    private BracketReportDefinition BuildBracketReport()
    {
        var report = new BracketReportDefinition
        {
            Name = "Match Draw",
            Summary = "No categories available for bracket draw"
        };

        var fightersByName = LoadFightersByName();
        var matchCategories = LoadBracketCategoriesFromMatches(fightersByName);

        if (matchCategories.Count > 0)
        {
            report.Categories.AddRange(matchCategories);
            report.Summary = $"{report.Categories.Count} bracket categories generated from matches";
            return report;
        }

        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText =
        @"
        SELECT
            f.FirstName,
            f.LastName,
            c.Name,
            f.AgeCategory,
            f.WeightCategory,
            f.Gender
        FROM Fighters f
        LEFT JOIN Clubs c
            ON c.Id = f.ClubId
        ORDER BY f.AgeCategory, f.WeightCategory, f.Gender, f.FirstName, f.LastName
        ";

        var categoryEntries = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var firstName = ReadString(reader, 0);
            var lastName = ReadString(reader, 1);
            var clubName = ReadString(reader, 2);
            var ageCategory = ReadString(reader, 3);
            var weightCategory = ReadString(reader, 4);
            var gender = ReadString(reader, 5);
            var categoryTitle = BuildBracketCategoryTitle(ageCategory, weightCategory, gender);
            var athleteLabel = BuildBracketAthleteEntry(firstName, lastName, clubName);

            if (!categoryEntries.TryGetValue(categoryTitle, out var athletes))
            {
                athletes = new List<string>();
                categoryEntries[categoryTitle] = athletes;
            }

            athletes.Add(athleteLabel);
        }

        foreach (var pair in categoryEntries.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            report.Categories.Add(BuildBracketCategory(pair.Key, pair.Value));
        }

        if (report.Categories.Count > 0)
            report.Summary = $"{report.Categories.Count} bracket categories generated";

        return report;
    }

    private ReportDefinition BuildFighterReport()
    {
        var report = new ReportDefinition
        {
            Name = "Fighter List",
            Columns = new List<string>
            {
                "First Name", "Last Name", "Club", "Birth Year", "Age", "Weight", "Gender", "Category", "Weight Class"
            }
        };

        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText =
        @"
        SELECT
            f.FirstName,
            f.LastName,
            c.Name,
            f.BirthYear,
            f.Age,
            f.Weight,
            f.Gender,
            f.AgeCategory,
            f.WeightCategory
        FROM Fighters f
        LEFT JOIN Clubs c
            ON c.Id = f.ClubId
        ORDER BY f.FirstName, f.LastName
        ";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            report.Rows.Add(new ReportRow
            {
                Values = new[]
                {
                    ReadString(reader, 0),
                    ReadString(reader, 1),
                    ReadString(reader, 2),
                    ReadInt(reader, 3),
                    ReadInt(reader, 4),
                    ReadDouble(reader, 5),
                    ReadString(reader, 6),
                    ReadString(reader, 7),
                    ReadString(reader, 8)
                }
            });
        }

        report.Summary = $"{report.Rows.Count} fighters listed";
        return report;
    }

    private ReportDefinition BuildCategoryReport()
    {
        var report = new ReportDefinition
        {
            Name = "Category List",
            Columns = new List<string>
            {
                "Division", "Gender", "Age Range", "Weight", "Rounds", "Break"
            }
        };

        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText =
        @"
        SELECT
            Division,
            Gender,
            AgeMin,
            AgeMax,
            WeightMax,
            IsOpenWeight,
            RoundCount,
            RoundDurationSeconds,
            BreakDurationSeconds
        FROM Categories
        ORDER BY AgeMin, AgeMax, Division, Gender, SortOrder
        ";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var weightMax = reader.IsDBNull(4) ? 0 : reader.GetDouble(4);
            var isOpenWeight = !reader.IsDBNull(5) && reader.GetInt32(5) == 1;
            var roundCount = reader.IsDBNull(6) ? 0 : reader.GetInt32(6);
            var roundSeconds = reader.IsDBNull(7) ? 0 : reader.GetInt32(7);
            var breakSeconds = reader.IsDBNull(8) ? 0 : reader.GetInt32(8);

            report.Rows.Add(new ReportRow
            {
                Values = new[]
                {
                    ReadString(reader, 0),
                    ReadString(reader, 1),
                    $"{ReadInt(reader, 2)}-{ReadInt(reader, 3)}",
                    isOpenWeight ? $"+{FormatWeight(weightMax)} kg" : $"-{FormatWeight(weightMax)} kg",
                    $"{roundCount} x {FormatDuration(roundSeconds)}",
                    FormatDuration(breakSeconds)
                }
            });
        }

        report.Summary = $"{report.Rows.Count} category rows listed";
        return report;
    }

    private ReportDefinition BuildMatchReport()
    {
        var report = new ReportDefinition
        {
            Name = "Match List",
            Columns = new List<string>
            {
                "Day", "Ring", "Bout", "Red", "Blue", "Category", "Weight", "Gender", "Winner", "Method"
            }
        };

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
            m.RingName,
            mr.Winner,
            mr.Method
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
            var winnerSide = ReadString(reader, 8);
            var redName = ReadString(reader, 2);
            var blueName = ReadString(reader, 3);

            report.Rows.Add(new ReportRow
            {
                Values = new[]
                {
                    $"Day {ReadInt(reader, 0)}",
                    ReadString(reader, 7),
                    ReadInt(reader, 1),
                    redName,
                    blueName,
                    ReadString(reader, 4),
                    ReadString(reader, 5),
                    ReadString(reader, 6),
                    ResolveWinnerName(winnerSide, redName, blueName),
                    ReadString(reader, 9)
                }
            });
        }

        report.Summary = $"{report.Rows.Count} matches listed";
        return report;
    }

    private MatchScheduleReportDefinition BuildMatchScheduleReport()
    {
        var dayNumber = ParseSelectedReportDayNumber();
        var fightersByName = LoadFightersByName();
        var report = new MatchScheduleReportDefinition
        {
            Name = $"Match List Day {dayNumber}",
            DayNumber = dayNumber,
            ReportDate = GetReportDate(dayNumber),
            RingName = GetRingName(),
            Summary = $"No matches listed for Day {dayNumber} | {GetRingName()}"
        };

        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();
        var championshipId = ChampionshipSettingsService.GetOrCreateActiveChampionshipId();
        var allRingsSelected = IsAllRingsSelected();

        var command = connection.CreateCommand();
        command.CommandText =
        @"
        SELECT
            m.Id,
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
          AND m.DayNumber = @dayNumber
          AND (@allRings = 1 OR m.RingName = @ringName)
        ORDER BY m.AgeCategory, m.WeightCategory, m.Gender, m.OrderNo, m.Id
        ";
        command.Parameters.AddWithValue("@championshipId", championshipId);
        command.Parameters.AddWithValue("@dayNumber", dayNumber);
        command.Parameters.AddWithValue("@ringName", report.RingName);
        command.Parameters.AddWithValue("@allRings", allRingsSelected ? 1 : 0);

        var matches = new List<BracketMatchData>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            matches.Add(new BracketMatchData
            {
                Id = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                OrderNo = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                Fighter1Name = ReadString(reader, 2),
                Fighter2Name = ReadString(reader, 3),
                AgeCategory = ReadString(reader, 4),
                WeightCategory = ReadString(reader, 5),
                Gender = ReadString(reader, 6),
                Winner = ReadString(reader, 7),
                DayNumber = dayNumber
            });
        }

        var orderedRows = new List<MatchScheduleRow>();

        foreach (var group in matches
                     .GroupBy(x => new { x.AgeCategory, x.WeightCategory, x.Gender })
                     .OrderBy(x => x.Key.AgeCategory)
                     .ThenBy(x => x.Key.WeightCategory)
                     .ThenBy(x => x.Key.Gender))
        {
            var stage = ResolveStageLabel(group.Count());
            foreach (var match in group.OrderBy(x => x.OrderNo).ThenBy(x => x.Id))
            {
                var red = ResolveBracketFighterInfo(match.Fighter1Name, fightersByName);
                var blue = ResolveBracketFighterInfo(match.Fighter2Name, fightersByName);
                orderedRows.Add(new MatchScheduleRow
                {
                    OrderNumber = match.OrderNo,
                    Stage = stage,
                    AgeCategory = BuildShortAgeCategory(group.Key.AgeCategory, group.Key.Gender),
                    WeightCategory = group.Key.WeightCategory,
                    RedName = red.PrimaryText,
                    RedClub = red.SecondaryText,
                    BlueName = blue.PrimaryText,
                    BlueClub = blue.SecondaryText,
                    Result = ResolveResultLabel(match, red.PrimaryText, blue.PrimaryText)
                });
            }
        }

        report.Rows = orderedRows.OrderBy(x => x.OrderNumber).ToList();
        report.Summary = $"{report.Rows.Count} matches listed for Day {dayNumber} | {report.RingName}";
        return report;
    }

    private ReportDefinition BuildMedalReport()
    {
        var awards = MedalTableService.BuildAwards();
        var standings = MedalTableService.BuildStandings(awards);

        var report = new ReportDefinition
        {
            Name = "Medal Table",
            Columns = new List<string>
            {
                "Club", "Gold", "Silver", "Bronze", "Total"
            }
        };

        foreach (var standing in standings)
        {
            report.Rows.Add(new ReportRow
            {
                Values = new[]
                {
                    standing.ClubName,
                    standing.Gold.ToString(CultureInfo.InvariantCulture),
                    standing.Silver.ToString(CultureInfo.InvariantCulture),
                    standing.Bronze.ToString(CultureInfo.InvariantCulture),
                    standing.Total.ToString(CultureInfo.InvariantCulture)
                }
            });
        }

        report.Summary = $"{standings.Count} clubs listed | {awards.Count} medals awarded";
        return report;
    }

    private string BuildPreview(ReportDefinition report)
    {
        var widths = new int[report.Columns.Count];

        for (int i = 0; i < report.Columns.Count; i++)
            widths[i] = report.Columns[i].Length;

        foreach (var row in report.Rows)
        {
            for (int i = 0; i < report.Columns.Count; i++)
                widths[i] = Math.Max(widths[i], row.Values.ElementAtOrDefault(i)?.Length ?? 0);
        }

        var builder = new StringBuilder();
        builder.AppendLine(report.Name);
        builder.AppendLine(GetTournamentTitle());
        builder.AppendLine($"Generated At: {generatedAt}");
        builder.AppendLine(report.Summary);
        builder.AppendLine();
        builder.AppendLine(BuildFixedWidthLine(report.Columns.ToArray(), widths));
        builder.AppendLine(BuildSeparator(widths));

        foreach (var row in report.Rows)
            builder.AppendLine(BuildFixedWidthLine(row.Values, widths));

        return builder.ToString();
    }

    private string BuildFixedWidthLine(string[] values, int[] widths)
    {
        return string.Join(" | ",
            values.Select((value, index) => (value ?? string.Empty).PadRight(widths[index])));
    }

    private string BuildSeparator(int[] widths)
    {
        return string.Join("-+-", widths.Select(width => new string('-', width)));
    }

    private string BuildHtml(ReportDefinition report, bool autoPrint = false)
    {
        var builder = new StringBuilder();
        builder.Append("""
<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<title>Report</title>
<style>
body { font-family: Arial, sans-serif; margin: 24px; }
h1 { margin-bottom: 8px; }
p { color: #444; }
table { border-collapse: collapse; width: 100%; }
th, td { border: 1px solid #ccc; padding: 8px; text-align: left; }
th { background: #f2f2f2; }
</style>
</head>
<body>
""");
        if (autoPrint)
            builder.AppendLine("<script>window.onload = () => window.print();</script>");
        builder.AppendLine($"<h1>{EscapeHtml(report.Name)}</h1>");
        builder.AppendLine($"<p><strong>{EscapeHtml(GetTournamentTitle())}</strong></p>");
        builder.AppendLine($"<p>Generated At: {EscapeHtml(generatedAt)}</p>");
        builder.AppendLine($"<p>{EscapeHtml(report.Summary)}</p>");
        builder.AppendLine("<table><thead><tr>");

        foreach (var column in report.Columns)
            builder.AppendLine($"<th>{EscapeHtml(column)}</th>");

        builder.AppendLine("</tr></thead><tbody>");

        foreach (var row in report.Rows)
        {
            builder.AppendLine("<tr>");
            foreach (var value in row.Values)
                builder.AppendLine($"<td>{EscapeHtml(value)}</td>");
            builder.AppendLine("</tr>");
        }

        builder.AppendLine("</tbody></table></body></html>");
        return builder.ToString();
    }

    private string BuildBracketHtml(BracketReportDefinition report, bool autoPrint = false)
    {
        var builder = new StringBuilder();
        builder.Append("""
<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<title>Match Draw</title>
<style>
body { font-family: Arial, sans-serif; margin: 20px; color: #111; }
h1 { margin: 0 0 8px 0; }
h2 { margin: 24px 0 10px 0; font-size: 20px; }
p { color: #444; margin: 6px 0; }
.category { page-break-inside: avoid; margin-bottom: 28px; border: 1px solid #d9d9d9; border-radius: 12px; padding: 16px; }
.round-title { font-size: 12px; fill: #1d4f91; font-weight: bold; }
.fighter { font-size: 11px; fill: #111; }
.winner { font-size: 12px; fill: #111; font-weight: bold; }
.line { stroke: #222; stroke-width: 1.5; }
</style>
</head>
<body>
""");

        if (autoPrint)
            builder.AppendLine("<script>window.onload = () => window.print();</script>");

        builder.AppendLine($"<h1>{EscapeHtml(report.Name)}</h1>");
        builder.AppendLine($"<p><strong>{EscapeHtml(GetTournamentTitle())}</strong></p>");
        builder.AppendLine($"<p>Generated At: {EscapeHtml(generatedAt)}</p>");
        builder.AppendLine($"<p>{EscapeHtml(report.Summary)}</p>");

        foreach (var category in report.Categories)
        {
            builder.AppendLine("<section class=\"category\">");
            builder.AppendLine($"<h2>{EscapeHtml(category.Title)} ({category.FighterCount} athletes)</h2>");

            if (category.Rounds.Count == 0)
            {
                builder.AppendLine("<p>Not enough athletes to generate a draw.</p>");
                builder.AppendLine("</section>");
                continue;
            }

            var layout = BuildBracketLayout(category, 18, 150, 70, 34, 20, 44);
            builder.AppendLine($"<svg width=\"{layout.Width}\" height=\"{layout.Height}\" viewBox=\"0 0 {layout.Width} {layout.Height}\" xmlns=\"http://www.w3.org/2000/svg\">");

            foreach (var title in layout.RoundTitles)
                builder.AppendLine($"<text class=\"round-title\" x=\"{FormatSvg(title.X)}\" y=\"{FormatSvg(title.Y)}\">{EscapeHtml(title.Text)}</text>");

            foreach (var match in layout.Matches)
            {
                builder.AppendLine($"<rect x=\"{FormatSvg(match.BoxX)}\" y=\"{FormatSvg(match.BoxY)}\" width=\"{FormatSvg(match.BoxWidth)}\" height=\"{FormatSvg(match.BoxHeight)}\" fill=\"white\" stroke=\"#222\" stroke-width=\"1.2\" />");
                builder.AppendLine($"<line class=\"line\" x1=\"{FormatSvg(match.BoxX)}\" y1=\"{FormatSvg(match.RowDividerY)}\" x2=\"{FormatSvg(match.BoxRightX)}\" y2=\"{FormatSvg(match.RowDividerY)}\" />");
                builder.AppendLine($"<rect x=\"{FormatSvg(match.BoxX)}\" y=\"{FormatSvg(match.BoxY)}\" width=\"{FormatSvg(match.CornerBarWidth)}\" height=\"{FormatSvg(match.RowHeight)}\" fill=\"#D61518\" />");
                builder.AppendLine($"<rect x=\"{FormatSvg(match.BoxX)}\" y=\"{FormatSvg(match.BottomRowY)}\" width=\"{FormatSvg(match.CornerBarWidth)}\" height=\"{FormatSvg(match.RowHeight)}\" fill=\"#1947D1\" />");
                AppendSvgParticipantText(builder, match.TopPrimaryText, match.TopSecondaryText, match.TextX, match.TopPrimaryTextY + 10, match.TopSecondaryTextY + 9);
                AppendSvgParticipantText(builder, match.BottomPrimaryText, match.BottomSecondaryText, match.TextX, match.BottomPrimaryTextY + 10, match.BottomSecondaryTextY + 9);
                builder.AppendLine($"<line class=\"line\" x1=\"{FormatSvg(match.BoxRightX)}\" y1=\"{FormatSvg(match.TopY)}\" x2=\"{FormatSvg(match.StemX)}\" y2=\"{FormatSvg(match.TopY)}\" />");
                builder.AppendLine($"<line class=\"line\" x1=\"{FormatSvg(match.BoxRightX)}\" y1=\"{FormatSvg(match.BottomY)}\" x2=\"{FormatSvg(match.StemX)}\" y2=\"{FormatSvg(match.BottomY)}\" />");
                builder.AppendLine($"<line class=\"line\" x1=\"{FormatSvg(match.StemX)}\" y1=\"{FormatSvg(match.TopY)}\" x2=\"{FormatSvg(match.StemX)}\" y2=\"{FormatSvg(match.BottomY)}\" />");
                builder.AppendLine($"<line class=\"line\" x1=\"{FormatSvg(match.StemX)}\" y1=\"{FormatSvg(match.CenterY)}\" x2=\"{FormatSvg(match.OutputX)}\" y2=\"{FormatSvg(match.CenterY)}\" />");
            }

            if (layout.Winner != null)
            {
                builder.AppendLine($"<line class=\"line\" x1=\"{FormatSvg(layout.Winner.StartX)}\" y1=\"{FormatSvg(layout.Winner.Y)}\" x2=\"{FormatSvg(layout.Winner.EndX)}\" y2=\"{FormatSvg(layout.Winner.Y)}\" />");
                builder.AppendLine($"<text class=\"winner\" x=\"{FormatSvg(layout.Winner.TextX)}\" y=\"{FormatSvg(layout.Winner.TextY)}\">Winner</text>");
            }

            builder.AppendLine("</svg>");
            builder.AppendLine("</section>");
        }

        builder.AppendLine("</body></html>");
        return builder.ToString();
    }

    private string BuildScheduleHtml(MatchScheduleReportDefinition report, bool autoPrint = false)
    {
        var builder = new StringBuilder();
        builder.Append("""
<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<title>Match List</title>
<style>
body { font-family: Arial, sans-serif; margin: 18px; color: #111; }
.sheet { max-width: 1200px; margin: 0 auto; }
.title { text-align: center; font-weight: bold; font-size: 24px; margin-bottom: 4px; }
.subtitle { text-align: center; font-weight: bold; margin-bottom: 2px; }
.meta { text-align: center; font-weight: bold; margin-bottom: 16px; }
table { border-collapse: collapse; width: 100%; font-size: 12px; }
th, td { border: 1px solid #222; padding: 6px 8px; vertical-align: top; }
th { background: #f7f7f7; }
.red { background: #e21b23; color: #fff; text-align: center; }
.blue { background: #4a7de0; color: #fff; text-align: center; }
.fighter-name { font-weight: bold; }
.fighter-club { font-size: 11px; color: #333; }
</style>
</head>
<body>
""");
        if (autoPrint)
            builder.AppendLine("<script>window.onload = () => window.print();</script>");

        builder.AppendLine("<div class=\"sheet\">");
        builder.AppendLine($"<div class=\"title\">{EscapeHtml(GetTournamentTitle())}</div>");
        builder.AppendLine($"<div class=\"subtitle\">Day {report.DayNumber}</div>");
        builder.AppendLine($"<div class=\"subtitle\">{EscapeHtml(report.ReportDate)}</div>");
        builder.AppendLine($"<div class=\"meta\">{EscapeHtml(report.RingName)}</div>");
        builder.AppendLine("<table>");
        builder.AppendLine("<thead><tr>");
        builder.AppendLine("<th>Lp.</th>");
        builder.AppendLine("<th>Stage</th>");
        builder.AppendLine("<th>Age Category</th>");
        builder.AppendLine("<th>Weight</th>");
        builder.AppendLine("<th class=\"red\">Red Corner</th>");
        builder.AppendLine("<th class=\"blue\">Blue Corner</th>");
        builder.AppendLine("<th>Result</th>");
        builder.AppendLine("</tr></thead><tbody>");

        foreach (var row in report.Rows)
        {
            builder.AppendLine("<tr>");
            builder.AppendLine($"<td>{row.OrderNumber}</td>");
            builder.AppendLine($"<td>{EscapeHtml(row.Stage)}</td>");
            builder.AppendLine($"<td>{EscapeHtml(row.AgeCategory)}</td>");
            builder.AppendLine($"<td>{EscapeHtml(row.WeightCategory)}</td>");
            builder.AppendLine($"<td><div class=\"fighter-name\">{EscapeHtml(row.RedName)}</div><div class=\"fighter-club\">{EscapeHtml(row.RedClub)}</div></td>");
            builder.AppendLine($"<td><div class=\"fighter-name\">{EscapeHtml(row.BlueName)}</div><div class=\"fighter-club\">{EscapeHtml(row.BlueClub)}</div></td>");
            builder.AppendLine($"<td>{EscapeHtml(row.Result)}</td>");
            builder.AppendLine("</tr>");
        }

        builder.AppendLine("</tbody></table></div></body></html>");
        return builder.ToString();
    }

    private string GetReportsDirectory()
    {
        return AppPaths.GetReportsDirectory();
    }

    private string BuildSafeFileName(string value)
    {
        var invalidChars = System.IO.Path.GetInvalidFileNameChars();
        var safe = new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return safe.Replace(' ', '_');
    }

    private string BuildReportFilePrefix(string reportName)
    {
        var tournament = BuildSafeFileName(GetTournamentTitle());
        var report = BuildSafeFileName(reportName);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        return $"{tournament}_{report}_{stamp}";
    }

    private string GetTournamentTitle()
    {
        var text = TournamentTitleBox.Text?.Trim();
        return string.IsNullOrWhiteSpace(text)
            ? "Muaythai Championship"
            : text;
    }

    private int ParseSelectedReportDayNumber()
    {
        var text = ReportDayCombo.SelectedItem?.ToString();
        if (!string.IsNullOrWhiteSpace(text))
        {
            var digits = new string(text.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var day))
                return day;
        }

        return 1;
    }

    private string GetReportDate(int dayNumber)
    {
        var text = ReportDateBox.Text?.Trim();
        return string.IsNullOrWhiteSpace(text)
            ? ChampionshipSettingsService.GetDateLabelForDay(dayNumber)
            : text;
    }

    private string GetRingName()
    {
        var text = RingCombo.SelectedItem?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(text) || string.Equals(text, LocalizationService.T("All"), StringComparison.OrdinalIgnoreCase))
            return LocalizationService.T("All");

        return text.ToUpperInvariant();
    }

    private List<string> GetRingFilterItems()
    {
        var rings = new List<string> { LocalizationService.T("All") };
        rings.AddRange(ChampionshipSettingsService.GetRingNames());
        return rings;
    }

    private bool IsAllRingsSelected()
    {
        var text = RingCombo.SelectedItem?.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ||
               string.Equals(text, AllRingsLabel, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(text, LocalizationService.T("All"), StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateFilterVisibility(string reportType)
    {
        var isMatchList = reportType == "Match List";
        ReportDayPanel.IsVisible = isMatchList;
        ReportDatePanel.IsVisible = isMatchList;
        RingPanel.IsVisible = isMatchList;
    }

    private string ResolveStageLabel(int matchCount)
    {
        var stage = ResolveRoundTitle(matchCount * 2, false);
        return stage.ToUpperInvariant();
    }

    private string ResolveResultLabel(BracketMatchData match, string redName, string blueName)
    {
        return ResolveWinnerName(match.Winner, redName, blueName, string.Empty);
    }

    private static string ResolveWinnerName(
        string winnerSide,
        string redName,
        string blueName,
        string noWinnerLabel = "-")
    {
        if (string.Equals(winnerSide, "Red", StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(redName) ? "BYE" : redName;

        if (string.Equals(winnerSide, "Blue", StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(blueName) ? "BYE" : blueName;

        return noWinnerLabel;
    }

    private string BuildShortAgeCategory(string ageCategory, string gender)
    {
        var age = ageCategory.Trim().ToUpperInvariant();
        var genderCode = string.Equals(gender, "Female", StringComparison.OrdinalIgnoreCase) ? "SF" : "SM";

        return age switch
        {
            "SENIOR" => genderCode,
            "MASTERS" => $"M {genderCode}",
            "U18" => $"U18 {genderCode}",
            "U24" => $"U24 {genderCode}",
            _ => string.IsNullOrWhiteSpace(age) ? genderCode : $"{age} {genderCode}"
        };
    }

    private string EscapeCsv(string value)
    {
        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private static string EscapeHtml(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    private string ReadString(Microsoft.Data.Sqlite.SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    }

    private string ReadInt(Microsoft.Data.Sqlite.SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal)
            ? string.Empty
            : reader.GetInt32(ordinal).ToString(CultureInfo.InvariantCulture);
    }

    private string ReadDouble(Microsoft.Data.Sqlite.SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal)
            ? string.Empty
            : FormatWeight(reader.GetDouble(ordinal));
    }

    private string FormatWeight(double value)
    {
        return value % 1 == 0
            ? value.ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private string FormatDuration(int seconds)
    {
        if (seconds <= 0)
            return "-";

        if (seconds % 60 == 0)
            return $"{seconds / 60} min";

        return $"{seconds / 60.0:0.#} min";
    }

    private void RenderBracketPreview(BracketReportDefinition report)
    {
        BracketHostPanel.Children.Clear();

        foreach (var category in report.Categories)
        {
            var card = new Border
            {
                BorderBrush = new SolidColorBrush(Color.Parse("#D9D9D9")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16),
                Background = Brushes.White,
                Child = BuildBracketCategoryPanel(category)
            };

            BracketHostPanel.Children.Add(card);
        }
    }

    private void RenderSchedulePreview(MatchScheduleReportDefinition report)
    {
        ScheduleHostPanel.Children.Clear();

        var sheet = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#222222")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16)
        };

        var stack = new StackPanel
        {
            Spacing = 10
        };

        stack.Children.Add(new TextBlock
        {
            Text = GetTournamentTitle(),
            FontSize = 24,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        });
        stack.Children.Add(new TextBlock
        {
            Text = $"Day {report.DayNumber}",
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        });
        stack.Children.Add(new TextBlock
        {
            Text = report.ReportDate,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        });
        stack.Children.Add(new TextBlock
        {
            Text = report.RingName,
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        });

        var table = new Grid
        {
            RowDefinitions = new RowDefinitions(),
            ColumnDefinitions = new ColumnDefinitions("54,110,110,110,260,260,90")
        };
        table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        AddScheduleHeaderCell(table, 0, 0, "Lp.", Brushes.White, Brushes.Black);
        AddScheduleHeaderCell(table, 0, 1, "Stage", Brushes.White, Brushes.Black);
        AddScheduleHeaderCell(table, 0, 2, "Age Category", Brushes.White, Brushes.Black);
        AddScheduleHeaderCell(table, 0, 3, "Weight", Brushes.White, Brushes.Black);
        AddScheduleHeaderCell(table, 0, 4, "Red Corner", new SolidColorBrush(Color.Parse("#E21B23")), Brushes.White);
        AddScheduleHeaderCell(table, 0, 5, "Blue Corner", new SolidColorBrush(Color.Parse("#4A7DE0")), Brushes.White);
        AddScheduleHeaderCell(table, 0, 6, "Result", Brushes.White, Brushes.Black);

        var rowIndex = 1;
        foreach (var row in report.Rows)
        {
            table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AddScheduleBodyCell(table, rowIndex, 0, row.OrderNumber.ToString(CultureInfo.InvariantCulture), isCenter: true);
            AddScheduleBodyCell(table, rowIndex, 1, row.Stage, isCenter: true);
            AddScheduleBodyCell(table, rowIndex, 2, row.AgeCategory, isCenter: true);
            AddScheduleBodyCell(table, rowIndex, 3, row.WeightCategory, isCenter: true);
            AddScheduleFighterCell(table, rowIndex, 4, row.RedName, row.RedClub);
            AddScheduleFighterCell(table, rowIndex, 5, row.BlueName, row.BlueClub);
            AddScheduleBodyCell(table, rowIndex, 6, row.Result, isCenter: true);
            rowIndex++;
        }

        stack.Children.Add(table);
        sheet.Child = stack;
        ScheduleHostPanel.Children.Add(sheet);
    }

    private Control BuildBracketCategoryPanel(BracketCategoryReport category)
    {
        var stack = new StackPanel
        {
            Spacing = 12
        };

        stack.Children.Add(new TextBlock
        {
            Text = $"{category.Title} ({category.FighterCount} athletes)",
            FontSize = 18,
            FontWeight = FontWeight.Bold
        });

        if (category.Rounds.Count == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "Not enough athletes to generate a draw.",
                Foreground = new SolidColorBrush(Color.Parse("#666666"))
            });
            return stack;
        }

        stack.Children.Add(BuildBracketCanvas(category));
        return stack;
    }

    private void AddScheduleHeaderCell(Grid table, int row, int column, string text, IBrush background, IBrush foreground)
    {
        var border = new Border
        {
            Background = background,
            BorderBrush = new SolidColorBrush(Color.Parse("#222222")),
            BorderThickness = new Thickness(0.5),
            Padding = new Thickness(6)
        };

        border.Child = new TextBlock
        {
            Text = text,
            FontWeight = FontWeight.Bold,
            Foreground = foreground,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };

        Grid.SetRow(border, row);
        Grid.SetColumn(border, column);
        table.Children.Add(border);
    }

    private void AddScheduleBodyCell(Grid table, int row, int column, string text, bool isCenter = false)
    {
        var border = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#222222")),
            BorderThickness = new Thickness(0.5),
            Padding = new Thickness(6)
        };

        border.Child = new TextBlock
        {
            Text = text,
            TextAlignment = isCenter ? TextAlignment.Center : TextAlignment.Left,
            HorizontalAlignment = isCenter ? Avalonia.Layout.HorizontalAlignment.Center : Avalonia.Layout.HorizontalAlignment.Stretch
        };

        Grid.SetRow(border, row);
        Grid.SetColumn(border, column);
        table.Children.Add(border);
    }

    private void AddScheduleFighterCell(Grid table, int row, int column, string fighterName, string clubName)
    {
        var border = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#222222")),
            BorderThickness = new Thickness(0.5),
            Padding = new Thickness(6)
        };

        var stack = new StackPanel
        {
            Spacing = 2
        };

        stack.Children.Add(new TextBlock
        {
            Text = fighterName,
            FontWeight = FontWeight.SemiBold
        });

        if (!string.IsNullOrWhiteSpace(clubName))
        {
            stack.Children.Add(new TextBlock
            {
                Text = clubName,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse("#444444"))
            });
        }

        border.Child = stack;
        Grid.SetRow(border, row);
        Grid.SetColumn(border, column);
        table.Children.Add(border);
    }

    private Control BuildBracketCanvas(BracketCategoryReport category)
    {
        var layout = BuildBracketLayout(category, 34, 180, 86, 38, 24, 54);
        var canvas = new Canvas
        {
            Width = layout.Width,
            Height = layout.Height
        };

        foreach (var title in layout.RoundTitles)
        {
            var titleBlock = new TextBlock
            {
                Text = title.Text,
                FontSize = 13,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.Parse("#1D4F91"))
            };
            Canvas.SetLeft(titleBlock, title.X);
            Canvas.SetTop(titleBlock, title.Y - 14);
            canvas.Children.Add(titleBlock);
        }

        foreach (var match in layout.Matches)
        {
            AddMatchBox(canvas, match);
            AddLine(canvas, match.BoxRightX, match.TopY, match.StemX, match.TopY);
            AddLine(canvas, match.BoxRightX, match.BottomY, match.StemX, match.BottomY);
            AddLine(canvas, match.StemX, match.TopY, match.StemX, match.BottomY);
            AddLine(canvas, match.StemX, match.CenterY, match.OutputX, match.CenterY);
        }

        if (layout.Winner != null)
        {
            AddLine(canvas, layout.Winner.StartX, layout.Winner.Y, layout.Winner.EndX, layout.Winner.Y, 2);

            var winnerText = new TextBlock
            {
                Text = "Winner",
                FontSize = 15,
                FontWeight = FontWeight.Bold
            };
            Canvas.SetLeft(winnerText, layout.Winner.TextX);
            Canvas.SetTop(winnerText, layout.Winner.TextY - 14);
            canvas.Children.Add(winnerText);
        }

        return canvas;
    }

    private void AddParticipantLabel(Canvas canvas, string primary, string secondary, double x, double primaryY, double secondaryY)
    {
        if (!string.IsNullOrWhiteSpace(primary))
        {
            var primaryLabel = new TextBlock
            {
                Text = primary,
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                Width = 152
            };

            Canvas.SetLeft(primaryLabel, x);
            Canvas.SetTop(primaryLabel, primaryY);
            canvas.Children.Add(primaryLabel);
        }

        if (!string.IsNullOrWhiteSpace(secondary))
        {
            var secondaryLabel = new TextBlock
            {
                Text = secondary,
                FontSize = 9.5,
                Width = 152,
                Foreground = new SolidColorBrush(Color.Parse("#333333"))
            };

            Canvas.SetLeft(secondaryLabel, x);
            Canvas.SetTop(secondaryLabel, secondaryY);
            canvas.Children.Add(secondaryLabel);
        }
    }

    private void AddLine(Canvas canvas, double x1, double y1, double x2, double y2, double thickness = 1.5)
    {
        canvas.Children.Add(new AvaloniaLine
        {
            StartPoint = new Point(x1, y1),
            EndPoint = new Point(x2, y2),
            Stroke = new SolidColorBrush(Color.Parse("#222222")),
            StrokeThickness = thickness
        });
    }

    private void AddMatchBox(Canvas canvas, BracketCanvasMatch match)
    {
        var border = new AvaloniaRectangle
        {
            Width = match.BoxWidth,
            Height = match.BoxHeight,
            Stroke = new SolidColorBrush(Color.Parse("#222222")),
            StrokeThickness = 1.2,
            Fill = Brushes.White
        };
        Canvas.SetLeft(border, match.BoxX);
        Canvas.SetTop(border, match.BoxY);
        canvas.Children.Add(border);

        AddLine(canvas, match.BoxX, match.RowDividerY, match.BoxRightX, match.RowDividerY, 1.2);

        var topBar = new AvaloniaRectangle
        {
            Width = match.CornerBarWidth,
            Height = match.RowHeight,
            Fill = new SolidColorBrush(Color.Parse("#D61518"))
        };
        Canvas.SetLeft(topBar, match.BoxX);
        Canvas.SetTop(topBar, match.BoxY);
        canvas.Children.Add(topBar);

        var bottomBar = new AvaloniaRectangle
        {
            Width = match.CornerBarWidth,
            Height = match.RowHeight,
            Fill = new SolidColorBrush(Color.Parse("#1947D1"))
        };
        Canvas.SetLeft(bottomBar, match.BoxX);
        Canvas.SetTop(bottomBar, match.BottomRowY);
        canvas.Children.Add(bottomBar);

        AddParticipantLabel(canvas, match.TopPrimaryText, match.TopSecondaryText, match.TextX, match.TopPrimaryTextY, match.TopSecondaryTextY);
        AddParticipantLabel(canvas, match.BottomPrimaryText, match.BottomSecondaryText, match.TextX, match.BottomPrimaryTextY, match.BottomSecondaryTextY);
    }

    private BracketCategoryReport BuildBracketCategory(string title, List<string> athletes)
    {
        var category = new BracketCategoryReport
        {
            Title = title,
            FighterCount = athletes.Count
        };

        if (athletes.Count < 2)
        {
            category.LeafCount = Math.Max(1, athletes.Count);
            return category;
        }

        var matchCounter = 1;
        var rounds = new List<BracketRoundReport>();

        if (IsPowerOfTwo(athletes.Count))
        {
            var entries = athletes
                .Select(ParseBracketSeedEntry)
                .Cast<BracketSeedEntryBase>()
                .ToList();

            AssignStartSlots(entries);
            category.LeafCount = entries.Sum(x => x.Span);
            rounds.AddRange(BuildBracketRounds(entries, ref matchCounter, hasPlayInRound: false));
            category.Rounds = rounds;
            return category;
        }

        var baseSize = HighestPowerOfTwoLessThan(athletes.Count);
        var byeCount = (baseSize * 2) - athletes.Count;
        var byes = athletes.Take(byeCount)
            .Select(ParseBracketSeedEntry)
            .Select(entry => new BracketSeedEntry(entry.PrimaryText, entry.SecondaryText, 2))
            .Cast<BracketSeedEntryBase>()
            .ToList();

        var playInAthletes = athletes.Skip(byeCount)
            .Select(ParseBracketSeedEntry)
            .ToList();

        var playInMatches = new List<BracketMatchSeed>();
        for (int i = 0; i < playInAthletes.Count; i += 2)
        {
            playInMatches.Add(new BracketMatchSeed(
                playInAthletes[i],
                playInAthletes[i + 1],
                matchCounter++));
        }

        var secondRoundEntries = new List<BracketSeedEntryBase>();
        secondRoundEntries.AddRange(byes);
        secondRoundEntries.AddRange(playInMatches);
        AssignStartSlots(secondRoundEntries);

        foreach (var playInMatch in playInMatches)
            playInMatch.SyncWithChildren();

        category.LeafCount = secondRoundEntries.Sum(x => x.Span);

        rounds.Add(new BracketRoundReport
        {
            Title = "Opening Round",
            Matches = playInMatches.Select(ToReportMatch).ToList()
        });

        rounds.AddRange(BuildBracketRounds(secondRoundEntries, ref matchCounter, hasPlayInRound: true));
        category.Rounds = rounds;
        return category;
    }

    private List<BracketRoundReport> BuildBracketRounds(
        List<BracketSeedEntryBase> entries,
        ref int matchCounter,
        bool hasPlayInRound)
    {
        var rounds = new List<BracketRoundReport>();
        var currentEntries = entries;
        var isFirstGeneratedRound = true;

        while (currentEntries.Count > 1)
        {
            var matches = new List<BracketMatchSeed>();

            for (int i = 0; i < currentEntries.Count; i += 2)
                matches.Add(new BracketMatchSeed(currentEntries[i], currentEntries[i + 1], matchCounter++));

            rounds.Add(new BracketRoundReport
            {
                Title = ResolveRoundTitle(matches.Count * 2, hasPlayInRound && isFirstGeneratedRound),
                Matches = matches.Select(ToReportMatch).ToList()
            });

            currentEntries = matches.Cast<BracketSeedEntryBase>().ToList();
            AssignStartSlots(currentEntries);
            foreach (var match in matches)
                match.SyncWithChildren();

            isFirstGeneratedRound = false;
        }

        return rounds;
    }

    private BracketMatchReport ToReportMatch(BracketMatchSeed match)
    {
        return new BracketMatchReport
        {
            MatchNumber = match.MatchNumber,
            TopPrimaryText = match.Top.PrimaryText,
            TopSecondaryText = match.Top.SecondaryText,
            BottomPrimaryText = match.Bottom.PrimaryText,
            BottomSecondaryText = match.Bottom.SecondaryText,
            TopSourceLabel = match.Top.SourceLabel,
            BottomSourceLabel = match.Bottom.SourceLabel,
            StartSlot = match.StartSlot,
            Span = match.Span,
            TopStartSlot = match.Top.StartSlot,
            TopSpan = match.Top.Span,
            BottomStartSlot = match.Bottom.StartSlot,
            BottomSpan = match.Bottom.Span
        };
    }

    private void AssignStartSlots(List<BracketSeedEntryBase> entries)
    {
        var offset = 0;
        foreach (var entry in entries)
        {
            entry.StartSlot = offset;
            offset += entry.Span;
        }
    }

    private string ResolveRoundTitle(int competitorCount, bool isMainRoundAfterPlayIn)
    {
        if (!isMainRoundAfterPlayIn)
        {
            return competitorCount switch
            {
                2 => "Final",
                4 => "Semi Finals",
                8 => "Quarter Finals",
                16 => "Preliminary 2",
                32 => "Preliminary 1",
                _ => $"Round of {competitorCount}"
            };
        }

        return competitorCount switch
        {
            4 => "Semi Finals",
            8 => "Quarter Finals",
            16 => "Preliminary 2",
            32 => "Preliminary 1",
            _ => $"Round of {competitorCount}"
        };
    }

    private string BuildBracketCategoryTitle(string ageCategory, string weightCategory, string gender)
    {
        var parts = new[]
        {
            ageCategory,
            weightCategory,
            gender
        }.Where(x => !string.IsNullOrWhiteSpace(x));

        return string.Join(" | ", parts);
    }

    private Dictionary<string, BracketFighterInfo> LoadFightersByName()
    {
        var fighters = new Dictionary<string, BracketFighterInfo>(StringComparer.OrdinalIgnoreCase);

        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText =
        @"
        SELECT
            f.FirstName,
            f.LastName,
            c.Name
        FROM Fighters f
        LEFT JOIN Clubs c
            ON c.Id = f.ClubId
        ";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var firstName = ReadString(reader, 0);
            var lastName = ReadString(reader, 1);
            var clubName = ReadString(reader, 2);
            var fullName = string.Join(" ", new[] { firstName, lastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
            var info = new BracketFighterInfo
            {
                PrimaryText = fullName,
                SecondaryText = clubName
            };

            AddFighterLookup(fighters, fullName, info);
            AddFighterLookup(fighters, firstName, info);
        }

        return fighters;
    }

    private void AddFighterLookup(Dictionary<string, BracketFighterInfo> fighters, string key, BracketFighterInfo info)
    {
        if (string.IsNullOrWhiteSpace(key) || fighters.ContainsKey(key))
            return;

        fighters[key] = info;
    }

    private List<BracketCategoryReport> LoadBracketCategoriesFromMatches(Dictionary<string, BracketFighterInfo> fightersByName)
    {
        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();
        var championshipId = ChampionshipSettingsService.GetOrCreateActiveChampionshipId();

        var command = connection.CreateCommand();
        command.CommandText =
        @"
        SELECT
            m.Id,
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
        ORDER BY m.AgeCategory, m.WeightCategory, m.Gender, m.DayNumber, m.OrderNo, m.Id
        ";
        command.Parameters.AddWithValue("@championshipId", championshipId);

        var matches = new List<BracketMatchData>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            matches.Add(new BracketMatchData
            {
                Id = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                DayNumber = reader.IsDBNull(1) ? 1 : reader.GetInt32(1),
                OrderNo = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                Fighter1Name = ReadString(reader, 3),
                Fighter2Name = ReadString(reader, 4),
                AgeCategory = ReadString(reader, 5),
                WeightCategory = ReadString(reader, 6),
                Gender = ReadString(reader, 7),
                Winner = ReadString(reader, 8)
            });
        }

        return matches
            .GroupBy(x => new { x.AgeCategory, x.WeightCategory, x.Gender })
            .OrderBy(x => x.Key.AgeCategory)
            .ThenBy(x => x.Key.WeightCategory)
            .ThenBy(x => x.Key.Gender)
            .Select(group => BuildBracketCategoryFromMatches(
                BuildBracketCategoryTitle(group.Key.AgeCategory, group.Key.WeightCategory, group.Key.Gender),
                group.OrderBy(x => x.DayNumber).ThenBy(x => x.OrderNo).ThenBy(x => x.Id).ToList(),
                fightersByName))
            .Where(x => x != null)
            .Cast<BracketCategoryReport>()
            .ToList();
    }

    private BracketCategoryReport? BuildBracketCategoryFromMatches(
        string title,
        List<BracketMatchData> matches,
        Dictionary<string, BracketFighterInfo> fightersByName)
    {
        if (matches.Count == 0)
            return null;

        var roundsByDay = matches
            .GroupBy(x => x.DayNumber)
            .OrderBy(x => x.Key)
            .ToList();

        var firstRound = roundsByDay.First().OrderBy(x => x.OrderNo).ThenBy(x => x.Id).ToList();
        var leafCount = firstRound.Count * 2;
        if (leafCount <= 0)
            return null;

        var category = new BracketCategoryReport
        {
            Title = title,
            FighterCount = CountDistinctScheduledFighters(matches),
            LeafCount = leafCount
        };

        var previousRoundMatches = new List<BracketMatchReport>();

        for (int roundIndex = 0; roundIndex < roundsByDay.Count; roundIndex++)
        {
            var roundMatches = roundsByDay[roundIndex]
                .OrderBy(x => x.OrderNo)
                .ThenBy(x => x.Id)
                .ToList();

            var reportRound = new BracketRoundReport
            {
                Title = ResolveRoundTitle(leafCount / (int)Math.Pow(2, roundIndex), false)
            };

            for (int matchIndex = 0; matchIndex < roundMatches.Count; matchIndex++)
            {
                var match = roundMatches[matchIndex];
                var startSlot = roundIndex == 0
                    ? matchIndex * 2
                    : previousRoundMatches.Skip(matchIndex * 2).Take(2).Select(x => x.StartSlot).DefaultIfEmpty(matchIndex * (int)Math.Pow(2, roundIndex + 1)).Min();

                var span = (int)Math.Pow(2, roundIndex + 1);
                var topStartSlot = roundIndex == 0
                    ? startSlot
                    : previousRoundMatches.Skip(matchIndex * 2).Take(1).Select(x => x.StartSlot).DefaultIfEmpty(startSlot).First();
                var bottomStartSlot = roundIndex == 0
                    ? startSlot + 1
                    : previousRoundMatches.Skip(matchIndex * 2 + 1).Take(1).Select(x => x.StartSlot).DefaultIfEmpty(startSlot + (span / 2)).First();
                var topSpan = span / 2;
                var bottomSpan = span / 2;

                var topInfo = ResolveBracketFighterInfo(match.Fighter1Name, fightersByName);
                var bottomInfo = ResolveBracketFighterInfo(match.Fighter2Name, fightersByName);

                reportRound.Matches.Add(new BracketMatchReport
                {
                    MatchNumber = match.OrderNo,
                    TopPrimaryText = topInfo.PrimaryText,
                    TopSecondaryText = topInfo.SecondaryText,
                    BottomPrimaryText = bottomInfo.PrimaryText,
                    BottomSecondaryText = bottomInfo.SecondaryText,
                    TopSourceLabel = topInfo.SourceLabel,
                    BottomSourceLabel = bottomInfo.SourceLabel,
                    StartSlot = startSlot,
                    Span = span,
                    TopStartSlot = topStartSlot,
                    TopSpan = topSpan,
                    BottomStartSlot = bottomStartSlot,
                    BottomSpan = bottomSpan
                });
            }

            previousRoundMatches = reportRound.Matches;
            category.Rounds.Add(reportRound);
        }

        return category;
    }

    private int CountDistinctScheduledFighters(List<BracketMatchData> matches)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var match in matches)
        {
            if (!IsByeName(match.Fighter1Name))
                names.Add(match.Fighter1Name);
            if (!IsByeName(match.Fighter2Name))
                names.Add(match.Fighter2Name);
        }

        return names.Count;
    }

    private BracketFighterInfo ResolveBracketFighterInfo(string fighterName, Dictionary<string, BracketFighterInfo> fightersByName)
    {
        if (string.IsNullOrWhiteSpace(fighterName) || IsByeName(fighterName))
        {
            return new BracketFighterInfo
            {
                PrimaryText = "BYE",
                SecondaryText = string.Empty
            };
        }

        if (fightersByName.TryGetValue(fighterName, out var info))
            return info;

        return new BracketFighterInfo
        {
            PrimaryText = fighterName,
            SecondaryText = string.Empty
        };
    }

    private bool IsByeName(string value)
    {
        return string.IsNullOrWhiteSpace(value) || string.Equals(value, "BYE", StringComparison.OrdinalIgnoreCase);
    }

    private string BuildBracketAthleteEntry(string firstName, string lastName, string clubName)
    {
        var fullName = string.Join(" ", new[] { firstName, lastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        return $"{fullName}||{clubName}";
    }

    private bool IsPowerOfTwo(int value)
    {
        return value > 0 && (value & (value - 1)) == 0;
    }

    private int HighestPowerOfTwoLessThan(int value)
    {
        var result = 1;
        while (result * 2 < value)
            result *= 2;

        return result;
    }

    private BracketCanvasLayout BuildBracketLayout(
        BracketCategoryReport category,
        double leafUnit,
        double boxWidth,
        double boxToStemWidth,
        double connectorWidth,
        double leftMargin,
        double topMargin)
    {
        const double rowHeight = 30;
        const double cornerBarWidth = 10;
        const double textInset = 14;
        var boxHeight = rowHeight * 2;
        var roundGap = boxWidth + boxToStemWidth + connectorWidth + 48;
        var roundTitles = new List<BracketCanvasTitle>();
        var matches = new List<BracketCanvasMatch>();

        for (int roundIndex = 0; roundIndex < category.Rounds.Count; roundIndex++)
        {
            var round = category.Rounds[roundIndex];
            var x = leftMargin + roundIndex * roundGap;

            roundTitles.Add(new BracketCanvasTitle
            {
                Text = round.Title,
                X = x,
                Y = 18
            });

            foreach (var match in round.Matches)
            {
                var topY = topMargin + ((match.TopStartSlot + (match.TopSpan / 2.0)) * leafUnit);
                var bottomY = topMargin + ((match.BottomStartSlot + (match.BottomSpan / 2.0)) * leafUnit);
                var centerY = topMargin + ((match.StartSlot + (match.Span / 2.0)) * leafUnit);
                var boxY = topY - (rowHeight / 2);
                var stemX = x + boxWidth + boxToStemWidth;

                matches.Add(new BracketCanvasMatch
                {
                    TopPrimaryText = match.TopPrimaryText,
                    TopSecondaryText = match.TopSecondaryText,
                    BottomPrimaryText = match.BottomPrimaryText,
                    BottomSecondaryText = match.BottomSecondaryText,
                    BoxX = x,
                    BoxY = boxY,
                    BoxWidth = boxWidth,
                    BoxHeight = boxHeight,
                    BoxRightX = x + boxWidth,
                    RowHeight = rowHeight,
                    RowDividerY = boxY + rowHeight,
                    BottomRowY = boxY + rowHeight,
                    CornerBarWidth = cornerBarWidth,
                    TextX = x + cornerBarWidth + textInset,
                    TopPrimaryTextY = boxY + 2,
                    TopSecondaryTextY = boxY + 15,
                    BottomPrimaryTextY = boxY + rowHeight + 2,
                    BottomSecondaryTextY = boxY + rowHeight + 15,
                    TopY = topY,
                    BottomY = bottomY,
                    CenterY = centerY,
                    StemX = stemX,
                    OutputX = stemX + connectorWidth
                });
            }
        }

        BracketCanvasWinner? winner = null;
        if (matches.Count > 0)
        {
            var finalMatch = matches.Last();
            winner = new BracketCanvasWinner
            {
                StartX = finalMatch.OutputX,
                EndX = finalMatch.OutputX + 56,
                Y = finalMatch.CenterY,
                TextX = finalMatch.OutputX + 60,
                TextY = finalMatch.CenterY - 6
            };
        }

        return new BracketCanvasLayout
        {
            Width = (category.Rounds.Count * roundGap) + leftMargin + boxWidth + boxToStemWidth + connectorWidth + 120,
            Height = topMargin + (Math.Max(1, category.LeafCount) * leafUnit) + 24,
            RoundTitles = roundTitles,
            Matches = matches,
            Winner = winner
        };
    }

    private string FormatSvg(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private void AppendSvgParticipantText(StringBuilder builder, string primary, string secondary, double x, double primaryY, double secondaryY)
    {
        if (!string.IsNullOrWhiteSpace(primary))
            builder.AppendLine($"<text class=\"fighter\" x=\"{FormatSvg(x)}\" y=\"{FormatSvg(primaryY)}\">{EscapeHtml(primary)}</text>");

        if (!string.IsNullOrWhiteSpace(secondary))
            builder.AppendLine($"<text class=\"fighter\" x=\"{FormatSvg(x)}\" y=\"{FormatSvg(secondaryY)}\">{EscapeHtml(secondary)}</text>");
    }

    private BracketSeedEntry ParseBracketSeedEntry(string rawValue)
    {
        var parts = rawValue.Split("||", 2, StringSplitOptions.None);
        var primary = parts.ElementAtOrDefault(0) ?? string.Empty;
        var secondary = parts.ElementAtOrDefault(1) ?? string.Empty;
        return new BracketSeedEntry(primary, secondary, 1);
    }

    private sealed class BracketCanvasLayout
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public List<BracketCanvasTitle> RoundTitles { get; set; } = new();
        public List<BracketCanvasMatch> Matches { get; set; } = new();
        public BracketCanvasWinner? Winner { get; set; }
    }

    private sealed class BracketCanvasTitle
    {
        public string Text { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
    }

    private sealed class BracketCanvasMatch
    {
        public string TopPrimaryText { get; set; } = "";
        public string TopSecondaryText { get; set; } = "";
        public string BottomPrimaryText { get; set; } = "";
        public string BottomSecondaryText { get; set; } = "";
        public double BoxX { get; set; }
        public double BoxY { get; set; }
        public double BoxWidth { get; set; }
        public double BoxHeight { get; set; }
        public double BoxRightX { get; set; }
        public double RowHeight { get; set; }
        public double RowDividerY { get; set; }
        public double BottomRowY { get; set; }
        public double CornerBarWidth { get; set; }
        public double TextX { get; set; }
        public double TopPrimaryTextY { get; set; }
        public double TopSecondaryTextY { get; set; }
        public double BottomPrimaryTextY { get; set; }
        public double BottomSecondaryTextY { get; set; }
        public double TopY { get; set; }
        public double BottomY { get; set; }
        public double CenterY { get; set; }
        public double StemX { get; set; }
        public double OutputX { get; set; }
    }

    private sealed class BracketCanvasWinner
    {
        public double StartX { get; set; }
        public double EndX { get; set; }
        public double Y { get; set; }
        public double TextX { get; set; }
        public double TextY { get; set; }
    }

    private sealed class BracketFighterInfo
    {
        public string PrimaryText { get; set; } = "";
        public string SecondaryText { get; set; } = "";
        public string SourceLabel => string.IsNullOrWhiteSpace(SecondaryText)
            ? PrimaryText
            : $"{PrimaryText} ({SecondaryText})";
    }

    private sealed class BracketMatchData
    {
        public int Id { get; set; }
        public int DayNumber { get; set; }
        public int OrderNo { get; set; }
        public string Fighter1Name { get; set; } = "";
        public string Fighter2Name { get; set; } = "";
        public string AgeCategory { get; set; } = "";
        public string WeightCategory { get; set; } = "";
        public string Gender { get; set; } = "";
        public string Winner { get; set; } = "";
    }

    private abstract class BracketSeedEntryBase
    {
        public int StartSlot { get; set; }
        public int Span { get; protected set; }
        public abstract string PrimaryText { get; }
        public abstract string SecondaryText { get; }
        public abstract string SourceLabel { get; }
    }

    private sealed class BracketSeedEntry : BracketSeedEntryBase
    {
        public BracketSeedEntry(string primaryText, string secondaryText, int span)
        {
            Primary = primaryText;
            Secondary = secondaryText;
            Span = span;
        }

        public string Primary { get; }
        public string Secondary { get; }
        public override string PrimaryText => Primary;
        public override string SecondaryText => Secondary;
        public override string SourceLabel => string.IsNullOrWhiteSpace(Secondary) ? Primary : $"{Primary} ({Secondary})";
    }

    private sealed class BracketMatchSeed : BracketSeedEntryBase
    {
        public BracketMatchSeed(BracketSeedEntryBase top, BracketSeedEntryBase bottom, int matchNumber)
        {
            Top = top;
            Bottom = bottom;
            MatchNumber = matchNumber;
            Span = top.Span + bottom.Span;
            SyncWithChildren();
        }

        public BracketSeedEntryBase Top { get; }
        public BracketSeedEntryBase Bottom { get; }
        public int MatchNumber { get; }
        public override string PrimaryText => string.Empty;
        public override string SecondaryText => string.Empty;
        public override string SourceLabel => $"Winner M{MatchNumber}";

        public void SyncWithChildren()
        {
            StartSlot = Top.StartSlot;
            Span = Top.Span + Bottom.Span;
        }
    }
}
