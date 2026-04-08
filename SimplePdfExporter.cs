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
        var lines = new List<string>
        {
            title,
            tournamentTitle,
            $"Generated At: {generatedAt}",
            string.Empty,
            string.Join(" | ", columns),
            new string('-', 100)
        };
        lines.AddRange(rows.Select(row => string.Join(" | ", row.Values)));

        var pageLines = Paginate(lines, 42);
        var objects = new List<byte[]>();

        objects.Add(Encode("<< /Type /Catalog /Pages 2 0 R >>"));
        objects.Add(Encode($"<< /Type /Pages /Kids [{string.Join(" ", Enumerable.Range(0, pageLines.Count).Select(i => $"{3 + i * 2} 0 R"))}] /Count {pageLines.Count} >>"));

        var fontObjectNumber = 3 + pageLines.Count * 2;

        for (int i = 0; i < pageLines.Count; i++)
        {
            var pageObjectNumber = 3 + i * 2;
            var contentObjectNumber = pageObjectNumber + 1;
            objects.Add(Encode($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 {fontObjectNumber} 0 R >> >> /Contents {contentObjectNumber} 0 R >>"));

            var content = BuildContent(pageLines[i]);
            objects.Add(Encode($"<< /Length {content.Length} >>\nstream\n{content}\nendstream"));
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

    private static List<List<string>> Paginate(List<string> lines, int linesPerPage)
    {
        var pages = new List<List<string>>();

        for (int i = 0; i < lines.Count; i += linesPerPage)
            pages.Add(lines.Skip(i).Take(linesPerPage).ToList());

        if (pages.Count == 0)
            pages.Add(new List<string> { "No data" });

        return pages;
    }

    private static string BuildContent(List<string> lines)
    {
        var builder = new StringBuilder();
        builder.AppendLine("BT");
        builder.AppendLine("/F1 10 Tf");

        var y = 800;
        foreach (var line in lines)
        {
            builder.AppendLine($"1 0 0 1 40 {y} Tm ({EscapePdf(line)}) Tj");
            y -= 18;
        }

        builder.AppendLine("ET");
        return builder.ToString();
    }

    private static string EscapePdf(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)");
    }

    private static byte[] Encode(string value) => Encoding.ASCII.GetBytes(value);
}
