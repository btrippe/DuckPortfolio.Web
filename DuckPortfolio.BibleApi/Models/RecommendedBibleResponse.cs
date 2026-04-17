namespace DuckPortfolio.BibleApi.Models;

public sealed record RecommendedBibleResponse(
    int Id,
    string Abbreviation,
    string Title,
    string Reason);
