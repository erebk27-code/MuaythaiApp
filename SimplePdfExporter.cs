using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MuaythaiApp;

public static class SimplePdfExporter
{
    public static void WriteTablePdf(
        string path,
        string title,
        IReadOnlyList<string> columns,
        IReadOnlyList<ReportRow> rows,
        string tournamentTitle,
        string generatedAt)
    {
        var pageContents = BuildTablePages(title, columns, rows, tournamentTitle, generatedAt);
        var objects = new List<byte[]>();

        objects.Add(Encode("<< /Type /Catalog /Pages 2 0 R >>"));
        objects.Add(Encode($"<< /Type /Pages /Kids [{string.Join(" ", Enumerable.Range(0, pageContents.Count).Select(i => $"{3 + i * 2} 0 R"))}] /Count {pageContents.Count} >>"));

        var fontObjectNumber = 3 + pageContents.Count * 2;

        for (int i = 0; i < pageContents.Count; i++)
        {
            var pageObjectNumber = 3 + i * 2;
            var contentObjectNumber = pageObjectNumber + 1;
            objects.Add(Encode($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 842 595] /Resources << /Font << /F1 {fontObjectNumber} 0 R >> >> /Contents {contentObjectNumber} 0 R >>"));
            objects.Add(Encode($"<< /Length {pageContents[i].Length} >>\nstream\n{pageContents[i]}\nendstream"));
        }

        objects.Add(Encode("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"));

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream, Encoding.ASCII);

        writer.Write(Encoding.ASCII.GetBytes("%PDF-1.4\n"));

        var offsets = new List<long> { 0 };
        for (int i = 0; i < objects.Count; i++)
        {
            offsets.Add(stream.Position);
            writer.Write(Encoding.ASCII.GetBytes($"{i + 1} 0 obj\n"));
            writer.Write(objects[i]);
            writer.Write(Encoding.ASCII.GetBytes("\nendobj\n"));
        }

        var xrefPosition = stream.Position;
        writer.Write(Encoding.ASCII.GetBytes($"xref\n0 {objects.Count + 1}\n"));
        writer.Write(Encoding.ASCII.GetBytes("0000000000 65535 f \n"));

        foreach (var offset in offsets.Skip(1))
            writer.Write(Encoding.ASCII.GetBytes($"{offset:D10} 00000 n \n"));

        writer.Write(Encoding.ASCII.GetBytes($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefPosition}\n%%EOF"));
    }

    public static void WriteBracketPdf(
        string path,
        string title,
        IReadOnlyList<BracketCategoryReport> categories,
        string tournamentTitle,
        string generatedAt)
    {
        var pageContents = BuildBracketPages(title, categories, tournamentTitle, generatedAt);
        var objects = new List<byte[]>
        {
            Encode("<< /Type /Catalog /Pages 2 0 R >>"),
            Encode($"<< /Type /Pages /Kids [{string.Join(" ", Enumerable.Range(0, pageContents.Count).Select(i => $"{3 + i * 2} 0 R"))}] /Count {pageContents.Count} >>")
        };

        var fontObjectNumber = 3 + pageContents.Count * 2;

        for (int i = 0; i < pageContents.Count; i++)
        {
            var pageObjectNumber = 3 + i * 2;
            var contentObjectNumber = pageObjectNumber + 1;
            objects.Add(Encode($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 842 595] /Resources << /Font << /F1 {fontObjectNumber} 0 R >> >> /Contents {contentObjectNumber} 0 R >>"));
            objects.Add(Encode($"<< /Length {pageContents[i].Length} >>\nstream\n{pageContents[i]}\nendstream"));
        }

        objects.Add(Encode("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"));

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream, Encoding.ASCII);

        writer.Write(Encoding.ASCII.GetBytes("%PDF-1.4\n"));

        var offsets = new List<long> { 0 };
        for (int i = 0; i < objects.Count; i++)
        {
            offsets.Add(stream.Position);
            writer.Write(Encoding.ASCII.GetBytes($"{i + 1} 0 obj\n"));
            writer.Write(objects[i]);
            writer.Write(Encoding.ASCII.GetBytes("\nendobj\n"));
        }

        var xrefPosition = stream.Position;
        writer.Write(Encoding.ASCII.GetBytes($"xref\n0 {objects.Count + 1}\n"));
        writer.Write(Encoding.ASCII.GetBytes("0000000000 65535 f \n"));

        foreach (var offset in offsets.Skip(1))
            writer.Write(Encoding.ASCII.GetBytes($"{offset:D10} 00000 n \n"));

        writer.Write(Encoding.ASCII.GetBytes($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefPosition}\n%%EOF"));
    }

    private static List<string> BuildTablePages(
        string title,
        IReadOnlyList<string> columns,
        IReadOnlyList<ReportRow> rows,
        string tournamentTitle,
        string generatedAt)
    {
        const double pageWidth = 842;
        const double margin = 28;
        const double titleTop = 560;
        const double headerY = 488;
        const double rowHeight = 24;
        const double fontSize = 7;
        var usableWidth = pageWidth - (margin * 2);
        var rowsPerPage = Math.Max(1, (int)Math.Floor((headerY - 32) / rowHeight));
        var widths = ResolveColumnWidths(columns, usableWidth);
        var pages = new List<string>();

        for (var start = 0; start < rows.Count || start == 0; start += rowsPerPage)
        {
            var pageRows = rows.Skip(start).Take(rowsPerPage).ToList();
            var builder = new StringBuilder();
            builder.AppendLine("0.8 w");
            builder.AppendLine("0 0 0 RG 0 0 0 rg");
            builder.AppendLine("BT");
            builder.AppendLine("/F1 15 Tf");
            builder.AppendLine($"1 0 0 1 {FormatPdf(margin)} {FormatPdf(titleTop)} Tm ({EscapePdf(title)}) Tj");
            builder.AppendLine("/F1 10 Tf");
            builder.AppendLine($"1 0 0 1 {FormatPdf(margin)} {FormatPdf(titleTop - 18)} Tm ({EscapePdf(tournamentTitle)}) Tj");
            builder.AppendLine($"1 0 0 1 {FormatPdf(margin)} {FormatPdf(titleTop - 34)} Tm ({EscapePdf($"Generated At: {generatedAt}")}) Tj");
            builder.AppendLine("ET");

            var x = margin;
            for (var i = 0; i < columns.Count; i++)
            {
                AppendRect(builder, x, headerY, widths[i], rowHeight, fill: "0.94 0.94 0.94 rg");
                x += widths[i];
            }

            x = margin;
            builder.AppendLine("BT");
            builder.AppendLine($"/F1 {fontSize} Tf");
            for (var i = 0; i < columns.Count; i++)
            {
                AppendPdfText(builder, TruncateToColumn(columns[i], widths[i], fontSize), x + 3, headerY + 9);
                x += widths[i];
            }
            builder.AppendLine("ET");

            var rowY = headerY - rowHeight;
            foreach (var row in pageRows)
            {
                x = margin;
                for (var i = 0; i < columns.Count; i++)
                {
                    AppendRect(builder, x, rowY, widths[i], rowHeight, fill: "1 1 1 rg");
                    x += widths[i];
                }

                x = margin;
                builder.AppendLine("BT");
                builder.AppendLine($"/F1 {fontSize} Tf");
                for (var i = 0; i < columns.Count; i++)
                {
                    var value = row.Values.ElementAtOrDefault(i) ?? string.Empty;
                    AppendPdfText(builder, TruncateToColumn(value, widths[i], fontSize), x + 3, rowY + 9);
                    x += widths[i];
                }
                builder.AppendLine("ET");
                rowY -= rowHeight;
            }

            pages.Add(builder.ToString());

            if (rows.Count == 0)
                break;
        }

        return pages;
    }

    private static double[] ResolveColumnWidths(IReadOnlyList<string> columns, double usableWidth)
    {
        if (columns.Count == 9)
            return new[] { 34.0, 62.0, 62.0, 58.0, 150.0, 100.0, 150.0, 100.0, 70.0 };

        if (columns.Count == 7)
            return new[] { 34.0, 82.0, 82.0, 72.0, 220.0, 220.0, 76.0 };

        var width = usableWidth / Math.Max(1, columns.Count);
        return Enumerable.Repeat(width, columns.Count).ToArray();
    }

    private static void AppendRect(StringBuilder builder, double x, double y, double width, double height, string fill)
    {
        builder.AppendLine($"{fill} {FormatPdf(x)} {FormatPdf(y)} {FormatPdf(width)} {FormatPdf(height)} re B");
    }

    private static string EscapePdf(string value)
    {
        return NormalizeForPdf(value)
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)");
    }

    private static string TruncateToColumn(string value, double columnWidth, double fontSize)
    {
        var normalized = NormalizeForPdf(value);
        var maxChars = Math.Max(4, (int)Math.Floor((columnWidth - 6) / (fontSize * 0.55)));

        return normalized.Length <= maxChars
            ? normalized
            : normalized[..Math.Max(1, maxChars - 3)] + "...";
    }

    private static string NormalizeForPdf(string value)
    {
        return value
            .Replace('ą', 'a').Replace('ć', 'c').Replace('ę', 'e').Replace('ł', 'l').Replace('ń', 'n')
            .Replace('ó', 'o').Replace('ś', 's').Replace('ż', 'z').Replace('ź', 'z')
            .Replace('Ą', 'A').Replace('Ć', 'C').Replace('Ę', 'E').Replace('Ł', 'L').Replace('Ń', 'N')
            .Replace('Ó', 'O').Replace('Ś', 'S').Replace('Ż', 'Z').Replace('Ź', 'Z')
            .Replace('ç', 'c').Replace('ğ', 'g').Replace('ı', 'i').Replace('İ', 'I').Replace('ö', 'o')
            .Replace('ş', 's').Replace('ü', 'u').Replace('Ç', 'C').Replace('Ğ', 'G').Replace('Ö', 'O')
            .Replace('Ş', 'S').Replace('Ü', 'U');
    }

    private static byte[] Encode(string value) => Encoding.ASCII.GetBytes(value);

    private static List<string> BuildBracketPages(
        string title,
        IReadOnlyList<BracketCategoryReport> categories,
        string tournamentTitle,
        string generatedAt)
    {
        var pages = new List<string>();

        if (categories.Count == 0)
        {
            pages.Add(BuildBracketTextPage(title, tournamentTitle, generatedAt, "No categories available for bracket draw."));
            return pages;
        }

        foreach (var category in categories)
            pages.Add(BuildBracketCategoryPage(title, tournamentTitle, generatedAt, category));

        return pages;
    }

    private static string BuildBracketTextPage(
        string title,
        string tournamentTitle,
        string generatedAt,
        string message)
    {
        var builder = new StringBuilder();
        builder.AppendLine("BT");
        builder.AppendLine("/F1 16 Tf");
        builder.AppendLine($"1 0 0 1 40 550 Tm ({EscapePdf(title)}) Tj");
        builder.AppendLine("/F1 11 Tf");
        builder.AppendLine($"1 0 0 1 40 528 Tm ({EscapePdf(tournamentTitle)}) Tj");
        builder.AppendLine($"1 0 0 1 40 510 Tm ({EscapePdf($"Generated At: {generatedAt}")}) Tj");
        builder.AppendLine($"1 0 0 1 40 470 Tm ({EscapePdf(message)}) Tj");
        builder.AppendLine("ET");
        return builder.ToString();
    }

    private static string BuildBracketCategoryPage(
        string title,
        string tournamentTitle,
        string generatedAt,
        BracketCategoryReport category)
    {
        var builder = new StringBuilder();
        var matches = new List<PdfBracketMatchLayout>();
        var roundTitles = new List<PdfBracketTitleLayout>();
        var leafUnit = ResolveLeafUnit(category.LeafCount);
        var boxWidth = 92.0;
        var boxToStemWidth = 24.0;
        var connectorWidth = 26.0;
        var leftMargin = 38.0;
        var topMargin = 430.0;
        const double rowHeight = 16.0;
        const double boxHeight = rowHeight * 2;
        const double cornerBarWidth = 6.0;
        var roundGap = boxWidth + boxToStemWidth + connectorWidth + 18.0;

        builder.AppendLine("BT");
        builder.AppendLine("/F1 16 Tf");
        builder.AppendLine($"1 0 0 1 38 560 Tm ({EscapePdf(title)}) Tj");
        builder.AppendLine("/F1 11 Tf");
        builder.AppendLine($"1 0 0 1 38 540 Tm ({EscapePdf(tournamentTitle)}) Tj");
        builder.AppendLine($"1 0 0 1 38 524 Tm ({EscapePdf($"Generated At: {generatedAt}")}) Tj");
        builder.AppendLine("/F1 12 Tf");
        builder.AppendLine($"1 0 0 1 38 498 Tm ({EscapePdf($"{category.Title} ({category.FighterCount} athletes)")}) Tj");

        if (category.Rounds.Count == 0)
        {
            builder.AppendLine($"1 0 0 1 38 470 Tm ({EscapePdf("Not enough athletes to generate a draw.")}) Tj");
            builder.AppendLine("ET");
            return builder.ToString();
        }

        for (int roundIndex = 0; roundIndex < category.Rounds.Count; roundIndex++)
        {
            var round = category.Rounds[roundIndex];
            var x = leftMargin + roundIndex * roundGap;
            roundTitles.Add(new PdfBracketTitleLayout(round.Title, x, 470));

            foreach (var match in round.Matches)
            {
                var topY = topMargin - ((match.TopStartSlot + (match.TopSpan / 2.0)) * leafUnit);
                var bottomY = topMargin - ((match.BottomStartSlot + (match.BottomSpan / 2.0)) * leafUnit);
                var centerY = topMargin - ((match.StartSlot + (match.Span / 2.0)) * leafUnit);
                var boxY = topY - (rowHeight / 2.0);
                var stemX = x + boxWidth + boxToStemWidth;

                matches.Add(new PdfBracketMatchLayout(
                    TruncatePdfLabel(match.TopPrimaryText, 18),
                    TruncatePdfLabel(match.TopSecondaryText, 18),
                    TruncatePdfLabel(match.BottomPrimaryText, 18),
                    TruncatePdfLabel(match.BottomSecondaryText, 18),
                    x,
                    boxY,
                    boxWidth,
                    boxHeight,
                    x + boxWidth,
                    boxY + rowHeight,
                    boxY + rowHeight,
                    rowHeight,
                    cornerBarWidth,
                    x + cornerBarWidth + 4,
                    topY,
                    bottomY,
                    centerY,
                    stemX,
                    stemX + connectorWidth));
            }
        }

        foreach (var roundTitle in roundTitles)
        {
            builder.AppendLine("/F1 9 Tf");
            builder.AppendLine($"1 0 0 1 {FormatPdf(roundTitle.X)} {FormatPdf(roundTitle.Y)} Tm ({EscapePdf(roundTitle.Text)}) Tj");
        }

        foreach (var match in matches)
        {
            builder.AppendLine("/F1 6 Tf");
            AppendPdfText(builder, match.TopPrimaryText, match.TextX, match.BoxY + 10);
            AppendPdfText(builder, match.TopSecondaryText, match.TextX, match.BoxY + 4);
            AppendPdfText(builder, match.BottomPrimaryText, match.TextX, match.BottomRowY + 10);
            AppendPdfText(builder, match.BottomSecondaryText, match.TextX, match.BottomRowY + 4);
        }

        builder.AppendLine("ET");
        builder.AppendLine("0.8 w");

        foreach (var match in matches)
        {
            builder.AppendLine($"0 0 0 RG {FormatPdf(match.BoxX)} {FormatPdf(match.BoxY)} {FormatPdf(match.BoxWidth)} {FormatPdf(match.BoxHeight)} re S");
            builder.AppendLine($"0.84 0.08 0.09 rg {FormatPdf(match.BoxX)} {FormatPdf(match.BoxY + rowHeight)} {FormatPdf(match.CornerBarWidth)} {FormatPdf(match.RowHeight)} re f");
            builder.AppendLine($"0.10 0.28 0.82 rg {FormatPdf(match.BoxX)} {FormatPdf(match.BoxY)} {FormatPdf(match.CornerBarWidth)} {FormatPdf(match.RowHeight)} re f");
            builder.AppendLine($"0 0 0 RG 0 0 0 rg");
            builder.AppendLine($"{FormatPdf(match.BoxX)} {FormatPdf(match.RowDividerY)} m {FormatPdf(match.BoxRightX)} {FormatPdf(match.RowDividerY)} l S");
            builder.AppendLine($"{FormatPdf(match.BoxRightX)} {FormatPdf(match.TopY)} m {FormatPdf(match.StemX)} {FormatPdf(match.TopY)} l S");
            builder.AppendLine($"{FormatPdf(match.BoxRightX)} {FormatPdf(match.BottomY)} m {FormatPdf(match.StemX)} {FormatPdf(match.BottomY)} l S");
            builder.AppendLine($"{FormatPdf(match.StemX)} {FormatPdf(match.TopY)} m {FormatPdf(match.StemX)} {FormatPdf(match.BottomY)} l S");
            builder.AppendLine($"{FormatPdf(match.StemX)} {FormatPdf(match.CenterY)} m {FormatPdf(match.OutputX)} {FormatPdf(match.CenterY)} l S");
        }

        var finalMatch = matches.Last();
        var winnerLineStart = finalMatch.OutputX;
        var winnerLineEnd = winnerLineStart + 42;
        builder.AppendLine($"{FormatPdf(winnerLineStart)} {FormatPdf(finalMatch.CenterY)} m {FormatPdf(winnerLineEnd)} {FormatPdf(finalMatch.CenterY)} l S");
        builder.AppendLine("BT");
        builder.AppendLine("/F1 10 Tf");
        builder.AppendLine($"1 0 0 1 {FormatPdf(winnerLineEnd + 6)} {FormatPdf(finalMatch.CenterY + 4)} Tm ({EscapePdf("Winner")}) Tj");
        builder.AppendLine("ET");

        return builder.ToString();
    }

    private static double ResolveLeafUnit(int leafCount)
    {
        if (leafCount >= 32)
            return 12;
        if (leafCount >= 16)
            return 18;
        if (leafCount >= 8)
            return 26;

        return 38;
    }

    private static string TruncatePdfLabel(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        return value.Length <= maxLength
            ? value
            : value.Substring(0, maxLength - 3) + "...";
    }

    private static string FormatPdf(double value)
    {
        return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AppendPdfText(StringBuilder builder, string value, double x, double y)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        builder.AppendLine($"1 0 0 1 {FormatPdf(x)} {FormatPdf(y)} Tm ({EscapePdf(value)}) Tj");
    }

    private sealed record PdfBracketTitleLayout(string Text, double X, double Y);

    private sealed record PdfBracketMatchLayout(
        string TopPrimaryText,
        string TopSecondaryText,
        string BottomPrimaryText,
        string BottomSecondaryText,
        double BoxX,
        double BoxY,
        double BoxWidth,
        double BoxHeight,
        double BoxRightX,
        double RowDividerY,
        double BottomRowY,
        double RowHeight,
        double CornerBarWidth,
        double TextX,
        double TopY,
        double BottomY,
        double CenterY,
        double StemX,
        double OutputX);
}
