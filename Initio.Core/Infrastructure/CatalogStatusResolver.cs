using Initio.Core.Models;

namespace Initio.Core.Infrastructure;

internal static class CatalogStatusResolver
{
    public static string Resolve(string wingetId, string name, IReadOnlyCollection<AppItem> catalogItems, string? installedInventoryOutput)
    {
        var match = catalogItems.FirstOrDefault(app => string.Equals(app.WingetId, wingetId, StringComparison.OrdinalIgnoreCase));
        if (match?.IsInstalled == true)
        {
            return "Installed";
        }

        if (match is not null)
        {
            return "In My Setup";
        }

        return IsReportedInstalled(wingetId, name, installedInventoryOutput) ? "Installed" : string.Empty;
    }

    public static bool IsReportedInstalled(string wingetId, string? name, string? installedInventoryOutput)
    {
        if (string.IsNullOrWhiteSpace(installedInventoryOutput))
        {
            return false;
        }

        if (installedInventoryOutput.Contains(wingetId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(name) &&
            installedInventoryOutput.Contains(name, StringComparison.OrdinalIgnoreCase);
    }
}
