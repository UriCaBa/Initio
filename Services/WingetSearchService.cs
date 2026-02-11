// WingetSearchService.cs
// Provides asynchronous winget search functionality to dynamically discover apps
// from the winget repository. Uses proper async process handling with timeout
// to prevent UI freezes.

using System.Diagnostics;

namespace NewPCSetupWPF.Services;

/// <summary>
/// Searches the winget repository asynchronously and parses results
/// into StoreTrendItem-compatible data (Name + WingetId).
/// Uses background thread for process execution to prevent UI deadlocks.
/// </summary>
public static class WingetSearchService
{
    private const int TimeoutSeconds = 15;

    /// <summary>
    /// Searches winget for apps matching the given query string.
    /// Runs the process on a background thread to prevent UI thread blocking.
    /// </summary>
    public static async Task<List<WingetSearchResult>> SearchAsync(
        string query, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        // Run the entire process on a background thread to avoid any UI blocking
        return await Task.Run(() => RunWingetSearch(query, maxResults, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Executes winget search synchronously on a background thread.
    /// This avoids the async deadlock issues with Process.StandardOutput.ReadToEndAsync.
    /// </summary>
    private static List<WingetSearchResult> RunWingetSearch(
        string query, int maxResults, CancellationToken cancellationToken)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "winget",
                // Use --disable-interactivity to prevent any interactive prompts
                // Do NOT use --count as it may not exist in all winget versions
                Arguments = $"search \"{query}\" --source winget --disable-interactivity",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            // Read stdout synchronously (we're on a background thread)
            var output = process.StandardOutput.ReadToEnd();

            // Wait for exit with timeout
            if (!process.WaitForExit(TimeoutSeconds * 1000))
            {
                try { process.Kill(true); } catch { /* best effort */ }
                return [];
            }

            cancellationToken.ThrowIfCancellationRequested();

            // winget returns 0 on success, non-zero values may include "no results"
            // Some versions return non-zero for empty results, so we parse anyway
            return ParseWingetOutput(output, maxResults);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // winget not installed, PATH issues, or other errors
            return [];
        }
    }

    /// <summary>
    /// Parses winget's column-based text output into structured results.
    /// Supports standard "---" separators and fallback to header-based parsing.
    /// </summary>
    private static List<WingetSearchResult> ParseWingetOutput(string output, int maxResults)
    {
        var results = new List<WingetSearchResult>();
        if (string.IsNullOrWhiteSpace(output))
            return results;

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                          .Select(l => l.TrimEnd())
                          .Where(l => !string.IsNullOrWhiteSpace(l))
                          .ToArray();

        if (lines.Length == 0) return results;

        // Strategy 1: Find separator line (starts with dashes)
        var separatorIndex = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith("---") || lines[i].Contains("----"))
            {
                separatorIndex = i;
                break;
            }
        }

        // Strategy 2: If no separator, find header containing "Id" (case-insensitive)
        int headerIndex = -1;
        if (separatorIndex == -1)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                // Look for " Id " surrounded by spaces, or "Id" at end/start of expected column
                // Common headers: "Name Id Version", "Nombre Id Versión", etc.
                var line = lines[i];
                var idx = line.IndexOf(" Id ", StringComparison.OrdinalIgnoreCase);
                if (idx > -1 || line.Trim().Equals("Id", StringComparison.OrdinalIgnoreCase))
                {
                    headerIndex = i;
                    break;
                }
            }
        }
        else
        {
            headerIndex = separatorIndex - 1;
        }

        // Determine column bounds
        int nameStart = 0;
        int idStart = -1;

        if (separatorIndex > -1)
        {
            // Use separator line to find columns
            var parts = FindColumnPositions(lines[separatorIndex]);
            if (parts.Count >= 2)
            {
                nameStart = parts[0].Start;
                idStart = parts[1].Start;
            }
        }
        else if (headerIndex > -1)
        {
            // Infer from header line
            var header = lines[headerIndex];
            // Find "Id" in header
            idStart = header.IndexOf("Id", StringComparison.OrdinalIgnoreCase);
            if (idStart == -1) idStart = header.IndexOf(" Id ", StringComparison.OrdinalIgnoreCase) + 1;
        }

        // If we still can't find ID column, fallback to splitting by multiple spaces
        // This is risky but better than nothing for "Name      Id"
        bool useSplitStrategy = idStart == -1;

        int startRow = (separatorIndex > -1 ? separatorIndex : headerIndex) + 1;
        if (startRow < 0) startRow = 0; // Fallback: try to parse all lines? Unlikely to work but safe.

        for (int i = startRow; i < lines.Length && results.Count < maxResults; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            string name, id;

            if (!useSplitStrategy && idStart > 0 && idStart < line.Length)
            {
                // Fixed width parsing
                // Name is everything up to Id column (trimmed)
                name = line[..idStart].Trim();
                
                // Id is from idStart until next whitespace/column
                var remainder = line[idStart..];
                var endOfId = remainder.IndexOf(' ');
                id = endOfId == -1 ? remainder.Trim() : remainder[..endOfId].Trim();
            }
            else
            {
                // Split strategy: split by 2+ spaces
                var parts = System.Text.RegularExpressions.Regex.Split(line.Trim(), @"\s{2,}");
                if (parts.Length < 2) continue;
                name = parts[0];
                id = parts[1];
            }

            // Remove ellipses "…" often found in truncated output
            if (id.EndsWith('…')) id = id.TrimEnd('…');
            
            // Validate ID format (must contain dot, e.g., Publisher.App) to avoid garbage
            // Also winget IDs don't have spaces
            if (!string.IsNullOrEmpty(name) && 
                !string.IsNullOrEmpty(id) && 
                id.Contains('.') && 
                !id.Contains(' '))
            {
                 // Filter out the header if we accidentally parsed it
                 if (id.Equals("Id", StringComparison.OrdinalIgnoreCase)) continue;
                 
                 results.Add(new WingetSearchResult(name, id));
            }
        }

        return results;
    }

    /// <summary>Finds column start/end positions from the separator line (e.g. "---- ---- ----").</summary>
    private static List<(int Start, int End)> FindColumnPositions(string separatorLine)
    {
        var columns = new List<(int Start, int End)>();
        int i = 0;

        while (i < separatorLine.Length)
        {
            if (separatorLine[i] != '-') { i++; continue; }
            int start = i;
            while (i < separatorLine.Length && separatorLine[i] == '-') i++;
            columns.Add((start, i));
        }

        return columns;
    }
}

/// <summary>Represents a single search result from winget.</summary>
public sealed record WingetSearchResult(string Name, string WingetId);
