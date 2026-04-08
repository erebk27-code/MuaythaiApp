using Avalonia.Controls;
using Avalonia.Interactivity;
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
    private readonly List<string> reportTypes = new()
    {
        "Fighter List",
        "Category List",
        "Match List",
        "Medal Table"
    };

    private ReportDefinition? currentReport;
    private readonly string generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    private readonly DatabaseAutoRefresh databaseAutoRefresh;

    public ReportsWindow()
    {
        InitializeComponent();
        databaseAutoRefresh = new DatabaseAutoRefresh(this, LoadSelectedReport);
        TournamentTitleBox.Text = "Muaythai Championship";
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
        if (currentReport == null)
            return;

        var path = Path.Combine(GetReportsDirectory(), $"{BuildReportFilePrefix(currentReport.Name)}.csv");
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
        if (currentReport == null)
            return;

        var path = Path.Combine(GetReportsDirectory(), $"{BuildReportFilePrefix(currentReport.Name)}.pdf");
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
        if (currentReport == null)
            return;

        var path = Path.Combine(GetReportsDirectory(), $"{BuildReportFilePrefix(currentReport.Name)}_print.html");
        File.WriteAllText(path, BuildHtml(currentReport, autoPrint: true), Encoding.UTF8);

        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        };

        Process.Start(startInfo);
        SummaryText.Text = $"{currentReport.Summary} | Print file opened: {path}";
    }

    private void LoadSelectedReport()
    {
        currentReport = BuildReport(ReportTypeCombo.SelectedItem?.ToString() ?? reportTypes[0]);
        PreviewTextBox.Text = BuildPreview(currentReport);
        SummaryText.Text = currentReport.Summary;
    }

    private void ApplyLocalization()
    {
        Title = LocalizationService.T("ReportsTitle");
        ReportsTitleText.Text = LocalizationService.T("ReportsTitle");
        TournamentTitleLabelText.Text = LocalizationService.T("TournamentTitle");
        ReportTypeLabelText.Text = LocalizationService.T("ReportType");
        RefreshButton.Content = LocalizationService.T("Refresh");
        ExportCsvButton.Content = LocalizationService.T("ExportCsv");
        ExportPdfButton.Content = LocalizationService.T("ExportPdf");
        PrintButton.Content = LocalizationService.T("Print");
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
                "Day", "Bout", "Red", "Blue", "Category", "Weight", "Gender", "Winner", "Method"
            }
        };

        using var connection = DatabaseHelper.CreateConnection();
        connection.Open();

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
            mr.Winner,
            mr.Method
        FROM Matches m
        LEFT JOIN MatchResult mr
            ON mr.MatchId = m.Id
        ORDER BY m.DayNumber, m.OrderNo, m.Id
        ";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var winnerSide = ReadString(reader, 7);
            var redName = ReadString(reader, 2);
            var blueName = ReadString(reader, 3);

            report.Rows.Add(new ReportRow
            {
                Values = new[]
                {
                    $"Day {ReadInt(reader, 0)}",
                    ReadInt(reader, 1),
                    redName,
                    blueName,
                    ReadString(reader, 4),
                    ReadString(reader, 5),
                    ReadString(reader, 6),
                    winnerSide == "Red" ? redName : winnerSide == "Blue" ? blueName : "-",
                    ReadString(reader, 8)
                }
            });
        }

        report.Summary = $"{report.Rows.Count} matches listed";
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

    private string GetReportsDirectory()
    {
        return AppPaths.GetReportsDirectory();
    }

    private string BuildSafeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
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
}
