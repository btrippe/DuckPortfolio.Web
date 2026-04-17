namespace DuckPortfolio.BibleApi.Options;

public sealed class YouVersionOptions
{
    public const string SectionName = "YouVersion";

    public string BaseUrl { get; init; } = "https://api.youversion.com";

    public string? AppKey { get; init; }
}
