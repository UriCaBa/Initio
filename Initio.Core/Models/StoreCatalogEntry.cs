namespace Initio.Core.Models;

public sealed record StoreCatalogEntry(string Category, int Rank, string Name, string WingetId, double Rating, string PopularitySignal);
