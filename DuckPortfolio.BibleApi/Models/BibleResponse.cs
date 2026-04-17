namespace DuckPortfolio.BibleApi.Models;

public sealed record BibleResponse(
    int Id,
    string? Abbreviation,
    string? LocalizedAbbreviation,
    string? Title,
    string? LocalizedTitle,
    string? LanguageTag,
    string? PromotionalContent,
    string? Copyright,
    string? Info,
    string? PublisherUrl,
    IReadOnlyList<string> Books,
    string? YouVersionDeepLink,
    Guid? OrganizationId,
    string Source);
