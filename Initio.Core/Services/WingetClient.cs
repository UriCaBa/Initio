using System.Text.RegularExpressions;
using Initio.Core.Abstractions;
using Initio.Core.Models;

namespace Initio.Core.Services;

public sealed class WingetClient : IWingetClient
{
    private const int SearchTimeoutSeconds = 15;
    private const string WingetSourceFlags = "--accept-source-agreements";
    private const string WingetInstallFlags = "--accept-package-agreements --accept-source-agreements";

    private readonly IProcessRunner _processRunner;
    private readonly string? _wingetExecutablePath;

    public WingetClient(IProcessRunner processRunner, string? wingetExecutablePath = null)
    {
        _processRunner = processRunner;
        _wingetExecutablePath = string.IsNullOrWhiteSpace(wingetExecutablePath)
            ? WindowsCommandPaths.TryGetWingetPath()
            : wingetExecutablePath;
    }

    public async Task<string?> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        return await RunWingetCommandAsync("--version", TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> ListInstalledAsync(string? query = null, CancellationToken cancellationToken = default)
    {
        var arguments = string.IsNullOrWhiteSpace(query)
            ? $"list {WingetSourceFlags}"
            : $"list \"{query}\" {WingetSourceFlags}";
        return await RunWingetCommandAsync(arguments, TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<WingetSearchResult>> SearchAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        var safeQuery = InputValidation.SanitizeSearchQuery(query);
        if (string.IsNullOrWhiteSpace(safeQuery))
        {
            return Array.Empty<WingetSearchResult>();
        }

        var output = await RunWingetCommandAsync(
            $"search \"{safeQuery}\" --source winget --disable-interactivity",
            TimeSpan.FromSeconds(SearchTimeoutSeconds),
            cancellationToken).ConfigureAwait(false);

        return ParseSearchResults(output, maxResults);
    }

    public async Task<string?> InstallAsync(string wingetId, bool silent, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (!InputValidation.IsValidWingetId(wingetId))
        {
            return null;
        }

        var silentFlag = silent ? "--silent " : string.Empty;
        return await RunWingetCommandAsync(
            $"install --id \"{wingetId}\" {silentFlag}{WingetInstallFlags}",
            timeout,
            cancellationToken).ConfigureAwait(false);
    }

    public static IReadOnlyList<WingetSearchResult> ParseSearchResults(string? output, int maxResults)
    {
        var results = new List<WingetSearchResult>();
        if (string.IsNullOrWhiteSpace(output))
        {
            return results;
        }

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.TrimEnd())
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (lines.Length == 0)
        {
            return results;
        }

        var separatorIndex = -1;
        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].TrimStart().StartsWith("---", StringComparison.Ordinal) ||
                lines[index].Contains("----", StringComparison.Ordinal))
            {
                separatorIndex = index;
                break;
            }
        }

        var headerIndex = -1;
        if (separatorIndex == -1)
        {
            for (var index = 0; index < lines.Length; index++)
            {
                var currentLine = lines[index];
                var idIndex = currentLine.IndexOf(" Id ", StringComparison.OrdinalIgnoreCase);
                if (idIndex > -1 || currentLine.Trim().Equals("Id", StringComparison.OrdinalIgnoreCase))
                {
                    headerIndex = index;
                    break;
                }
            }
        }
        else
        {
            headerIndex = separatorIndex - 1;
        }

        var idStart = -1;
        if (separatorIndex > -1)
        {
            var columns = FindColumnPositions(lines[separatorIndex]);
            if (columns.Count >= 2)
            {
                idStart = columns[1].Start;
            }
        }
        else if (headerIndex > -1)
        {
            var header = lines[headerIndex];
            idStart = header.IndexOf("Id", StringComparison.OrdinalIgnoreCase);
            if (idStart == -1)
            {
                var spaced = header.IndexOf(" Id ", StringComparison.OrdinalIgnoreCase);
                if (spaced >= 0)
                {
                    idStart = spaced + 1;
                }
            }
        }

        var useSplitStrategy = idStart == -1;
        var startRow = (separatorIndex > -1 ? separatorIndex : headerIndex) + 1;
        if (startRow < 0)
        {
            startRow = 0;
        }

        for (var index = startRow; index < lines.Length && results.Count < maxResults; index++)
        {
            var line = lines[index];
            string name;
            string id;

            if (!useSplitStrategy && idStart > 0 && idStart < line.Length)
            {
                name = line[..idStart].Trim();
                var remainder = line[idStart..];
                var endOfId = remainder.IndexOf(' ');
                id = (endOfId == -1 ? remainder : remainder[..endOfId]).Trim();
            }
            else
            {
                var parts = Regex.Split(line.Trim(), @"\s{2,}");
                if (parts.Length < 2)
                {
                    continue;
                }

                name = parts[0];
                id = parts[1];
            }

            id = id.TrimEnd('.', '…');
            id = id.Replace("â€¦", string.Empty, StringComparison.Ordinal);
            if (!string.IsNullOrWhiteSpace(name) &&
                !string.IsNullOrWhiteSpace(id) &&
                id.Contains('.', StringComparison.Ordinal) &&
                !id.Contains(' ', StringComparison.Ordinal) &&
                !id.Equals("Id", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new WingetSearchResult(name, id));
            }
        }

        return results;
    }

    private async Task<string?> RunWingetCommandAsync(string arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (!HasTrustedExecutablePath(_wingetExecutablePath))
        {
            return null;
        }

        var result = await _processRunner.RunAsync(
            new ProcessSpec(_wingetExecutablePath!, arguments),
            timeout,
            cancellationToken).ConfigureAwait(false);

        return result?.CombinedOutput;
    }

    private static bool HasTrustedExecutablePath(string? executablePath)
    {
        return !string.IsNullOrWhiteSpace(executablePath) &&
            Path.IsPathRooted(executablePath) &&
            File.Exists(executablePath);
    }

    private static List<(int Start, int End)> FindColumnPositions(string separatorLine)
    {
        var columns = new List<(int Start, int End)>();
        var index = 0;
        while (index < separatorLine.Length)
        {
            if (separatorLine[index] != '-')
            {
                index++;
                continue;
            }

            var start = index;
            while (index < separatorLine.Length && separatorLine[index] == '-')
            {
                index++;
            }

            columns.Add((start, index));
        }

        return columns;
    }
}
