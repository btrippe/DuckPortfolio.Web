namespace DuckPortfolio.BibleApi.Models;

public sealed record BiblePassageResponse(
    int BibleId,
    string? Id,
    string Reference,
    string? Content,
    string Format,
    bool? IncludeHeadings,
    bool? IncludeNotes,
    string Source);
