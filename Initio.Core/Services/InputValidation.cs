using System.Text.RegularExpressions;

namespace Initio.Core.Services;

public static partial class InputValidation
{
    private static readonly Regex WingetIdRegex = WingetIdPattern();
    private static readonly Regex PackageNameRegex = PackageNamePattern();
    private static readonly char[] DangerousChars = ['"', ';', '&', '|', '`', '$', '(', ')'];

    public static bool IsValidWingetId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        return WingetIdRegex.IsMatch(id);
    }

    public static string SanitizeSearchQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return string.Empty;
        }

        var span = query.AsSpan();
        var builder = new char[span.Length];
        var position = 0;

        foreach (var character in span)
        {
            if (Array.IndexOf(DangerousChars, character) == -1)
            {
                builder[position++] = character;
            }
        }

        return new string(builder, 0, position).Trim();
    }

    public static bool IsValidPackageName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return PackageNameRegex.IsMatch(name);
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9\.\-\+_]*$", RegexOptions.Compiled)]
    private static partial Regex WingetIdPattern();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9\.\-_]*$", RegexOptions.Compiled)]
    private static partial Regex PackageNamePattern();
}
