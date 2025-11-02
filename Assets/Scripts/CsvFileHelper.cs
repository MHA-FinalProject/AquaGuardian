using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

// Centralized CSV file I/O and parsing utilities
// Handles file locking, retry logic, and consistent parsing
public static class CsvFileHelper
{
    private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

    // Read CSV lines with retry logic to handle file locks (e.g., Excel)
    public static string[] ReadAllLinesWithRetry(string path, int maxRetries = 3, int delayMs = 100)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                {
                    var lines = new List<string>();
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }
                    return lines.ToArray();
                }
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                Debug.LogWarning($"[CsvFileHelper] File locked, retrying... ({i + 1}/{maxRetries})");
                System.Threading.Thread.Sleep(delayMs);
            }
        }
        throw new IOException($"Failed to read file after {maxRetries} attempts: {path}");
    }

    // Write all lines to a file with retry logic to handle file locks
    public static void WriteAllLinesWithRetry(string path, string[] lines, int maxRetries = 3, int delayMs = 100)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(stream))
                {
                    foreach (var line in lines)
                    {
                        writer.WriteLine(line);
                    }
                }
                return;
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                Debug.LogWarning($"[CsvFileHelper] File locked, retrying write... ({i + 1}/{maxRetries})");
                System.Threading.Thread.Sleep(delayMs);
            }
        }
        throw new IOException($"Failed to write file after {maxRetries} attempts: {path}");
    }

    // Parse CSV text into lines (simple version without retry - use ReadAllLinesWithRetry if needed)
    public static string[] ReadCsvLines(string path)
    {
        if (!File.Exists(path)) return Array.Empty<string>();
        var text = File.ReadAllText(path);
        return text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
    }

    // Find column index in header array (case-insensitive)
    public static int IndexOf(string[] headers, string name)
    {
        for (int i = 0; i < headers.Length; i++)
            if (string.Equals(headers[i].Trim(), name, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    // Try to parse float value (handles %, spaces, etc.)
    public static bool TryParseFloat(string s, out float f)
    {
        f = 0f;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim().Replace("%", string.Empty);
        return float.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CI, out f);
    }

    // Parse float field from CSV row with header index or fallback
    public static float ParseField(string[] fields, int headerIndex, int fallbackIndex)
    {
        int idx = headerIndex >= 0 ? headerIndex : fallbackIndex;
        if (idx >= 0 && idx < fields.Length && TryParseFloat(fields[idx], out var f))
            return f;
        return 0f;
    }

    // Parse int field from CSV row with header index or fallback
    public static int ParseIntField(string[] fields, int headerIndex, int fallbackIndex)
    {
        int idx = headerIndex >= 0 ? headerIndex : fallbackIndex;
        if (idx >= 0 && idx < fields.Length)
        {
            var s = fields[idx].Trim();
            if (int.TryParse(s, NumberStyles.Integer, CI, out var v))
                return v;
        }
        return 0;
    }

    // Find all oxygen column indices in header
    public static List<int> FindOxygenColumns(string[] headers)
    {
        var idxs = new List<int>();
        for (int i = 0; i < headers.Length; i++)
        {
            var h = headers[i].Trim().ToLowerInvariant();
            if (h.StartsWith("o2_run") || h == "o2_result")
                idxs.Add(i);
        }
        if (idxs.Count == 0)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                var h = headers[i].Trim().ToLowerInvariant();
                if (h == "oxygen" || h.EndsWith("oxygen") || h == "o2" || h.EndsWith("o2"))
                    idxs.Add(i);
            }
        }
        return idxs;
    }
}

