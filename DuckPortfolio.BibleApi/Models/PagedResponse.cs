namespace DuckPortfolio.BibleApi.Models;

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Data,
    string? NextPageToken,
    int? TotalSize);
