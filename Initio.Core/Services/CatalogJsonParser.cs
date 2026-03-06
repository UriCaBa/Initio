using System.Text.Json;
using Initio.Core.Models;

namespace Initio.Core.Services;

public static class CatalogJsonParser
{
    public static IReadOnlyList<StoreCatalogEntry> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var items = new List<StoreCatalogEntry>(200);

        if (!document.RootElement.TryGetProperty("categories", out var categoriesElement))
        {
            return items;
        }

        foreach (var category in categoriesElement.EnumerateArray())
        {
            var categoryName = category.GetProperty("name").GetString() ?? "Unknown";
            if (!category.TryGetProperty("apps", out var appsElement))
            {
                continue;
            }

            var rank = 0;
            foreach (var app in appsElement.EnumerateArray())
            {
                if (!app.TryGetProperty("name", out var nameElement) ||
                    !app.TryGetProperty("wingetId", out var wingetIdElement))
                {
                    continue;
                }

                var name = nameElement.GetString() ?? "Unknown";
                var wingetId = wingetIdElement.GetString() ?? string.Empty;
                if (!InputValidation.IsValidWingetId(wingetId))
                {
                    continue;
                }

                rank++;
                var popularitySignal = rank switch
                {
                    <= 3 => "Top ranked",
                    <= 10 => "Top free",
                    <= 20 => "Rising",
                    _ => "New"
                };
                var rating = Math.Max(3.5, Math.Round(4.9 - ((rank - 1) * 0.04), 1));
                items.Add(new StoreCatalogEntry(categoryName, rank, name, wingetId, rating, popularitySignal));
            }
        }

        return items;
    }
}
