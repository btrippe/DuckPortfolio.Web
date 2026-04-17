using DuckPortfolio.BibleApi.Models;
using DuckPortfolio.BibleApi.Options;
using DuckPortfolio.BibleApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<YouVersionOptions>(
    builder.Configuration.GetSection(YouVersionOptions.SectionName));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "DuckPortfolio Bible API",
        Version = "v1",
        Description = "A small API for retrieving Bible passages through YouVersion."
    });
});
builder.Services.AddHttpClient<YouVersionClient>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "DuckPortfolio Bible API v1");
    options.RoutePrefix = "swagger";
});

app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "DuckPortfolio.BibleApi"
}))
.WithName("Health")
.WithTags("Status")
.WithSummary("Reports whether the Bible API is running.")
.Produces(StatusCodes.Status200OK);

app.MapGet("/api/passages", async (
    int bibleId,
    string reference,
    BibleContentFormat? format,
    bool? includeHeadings,
    bool? includeNotes,
    YouVersionClient youVersionClient,
    CancellationToken cancellationToken) =>
{
    if (bibleId <= 0)
    {
        return Results.BadRequest(new ErrorResponse("bibleId must be greater than zero."));
    }

    if (string.IsNullOrWhiteSpace(reference))
    {
        return Results.BadRequest(new ErrorResponse("reference is required."));
    }

    try
    {
        var passage = await youVersionClient.GetPassageAsync(
            bibleId,
            reference,
            format,
            includeHeadings,
            includeNotes,
            cancellationToken);

        return Results.Ok(passage);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(
            title: "YouVersion configuration is missing.",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
    catch (YouVersionApiException ex)
    {
        return Results.Problem(
            title: "YouVersion request failed.",
            detail: ex.Message,
            statusCode: (int)ex.StatusCode);
    }
})
.WithName("GetPassage")
.WithTags("Passages")
.WithSummary("Gets a Bible passage from YouVersion.")
.WithDescription("Returns a normalized response for a Bible passage. Example: bibleId=3034, reference=JHN.3.16, format=Text. If format is omitted, text is requested.")
.Produces<BiblePassageResponse>(StatusCodes.Status200OK)
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status500InternalServerError);

app.MapGet("/api/verses", async (
    int bibleId,
    string reference,
    BibleContentFormat? format,
    bool? includeHeadings,
    bool? includeNotes,
    YouVersionClient youVersionClient,
    CancellationToken cancellationToken) =>
{
    if (bibleId <= 0)
    {
        return Results.BadRequest(new ErrorResponse("bibleId must be greater than zero."));
    }

    if (string.IsNullOrWhiteSpace(reference))
    {
        return Results.BadRequest(new ErrorResponse("reference is required."));
    }

    try
    {
        var passage = await youVersionClient.GetPassageAsync(
            bibleId,
            reference,
            format,
            includeHeadings,
            includeNotes,
            cancellationToken);

        return Results.Ok(passage);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(
            title: "YouVersion configuration is missing.",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
    catch (YouVersionApiException ex)
    {
        return Results.Problem(
            title: "YouVersion request failed.",
            detail: ex.Message,
            statusCode: (int)ex.StatusCode);
    }
})
.WithName("GetVerse")
.WithTags("Passages")
.WithSummary("Compatibility alias for getting a Bible passage.")
.WithDescription("Prefer /api/passages for new callers.")
.Produces<BiblePassageResponse>(StatusCodes.Status200OK)
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status500InternalServerError);

app.MapGet("/api/bibles", async (
    string[] languageRanges,
    int? licenseId,
    string? pageSize,
    string[]? fields,
    string? pageToken,
    YouVersionClient youVersionClient,
    CancellationToken cancellationToken) =>
{
    if (languageRanges.Length == 0 || languageRanges.Any(string.IsNullOrWhiteSpace))
    {
        return Results.BadRequest(new ErrorResponse("languageRanges is required. Example: en"));
    }

    if (!IsValidPageSize(pageSize, fields))
    {
        return Results.BadRequest(new ErrorResponse("pageSize must be between 1 and 100. The special value * is only allowed when fields contains three or fewer values."));
    }

    try
    {
        var bibles = await youVersionClient.GetBiblesAsync(
            languageRanges,
            licenseId,
            pageSize,
            fields,
            pageToken,
            cancellationToken);

        return Results.Ok(bibles);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(
            title: "YouVersion configuration is missing.",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
    catch (YouVersionApiException ex)
    {
        return Results.Problem(
            title: "YouVersion request failed.",
            detail: ex.Message,
            statusCode: (int)ex.StatusCode);
    }
})
.WithName("GetBibles")
.WithTags("Bibles")
.WithSummary("Gets Bible versions available to this app key.")
.WithDescription("Returns a paginated list of Bible versions. languageRanges is required and can be repeated, for example languageRanges=en. fields can also be repeated, for example fields=id&fields=abbreviation&fields=title.")
.Produces<PagedResponse<BibleResponse>>(StatusCodes.Status200OK)
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status500InternalServerError);

app.MapGet("/api/bibles/recommended", () => Results.Ok(new RecommendedBibleResponse[]
{
    new(
        3034,
        "BSB",
        "Berean Standard Bible",
        "Modern, readable, and already confirmed working as the default."),
    new(
        2692,
        "NASB2020",
        "New American Standard Bible 2020",
        "Formal and study-friendly; a good ESV-adjacent option."),
    new(
        111,
        "NIV11",
        "New International Version 2011",
        "Widely recognized and approachable for general readers."),
    new(
        206,
        "WEBUS",
        "World English Bible, American English Edition",
        "Useful public-friendly fallback with broad coverage."),
    new(
        1588,
        "AMP",
        "Amplified Bible",
        "Helpful study option with expanded wording.")
}))
.WithName("GetRecommendedBibles")
.WithTags("Bibles")
.WithSummary("Gets the curated starter Bible version list.")
.WithDescription("Returns a stable set of recommended English Bible versions for default app or Unity dropdowns.")
.Produces<IReadOnlyList<RecommendedBibleResponse>>(StatusCodes.Status200OK);

app.MapGet("/api/bibles/{bibleId:int}", async (
    int bibleId,
    YouVersionClient youVersionClient,
    CancellationToken cancellationToken) =>
{
    if (bibleId <= 0)
    {
        return Results.BadRequest(new ErrorResponse("bibleId must be greater than zero."));
    }

    try
    {
        var bible = await youVersionClient.GetBibleAsync(bibleId, cancellationToken);
        return Results.Ok(bible);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(
            title: "YouVersion configuration is missing.",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
    catch (YouVersionApiException ex)
    {
        return Results.Problem(
            title: "YouVersion request failed.",
            detail: ex.Message,
            statusCode: (int)ex.StatusCode);
    }
})
.WithName("GetBible")
.WithTags("Bibles")
.WithSummary("Gets metadata for a Bible version.")
.WithDescription("Returns metadata for a YouVersion Bible ID. Example: bibleId=3034.")
.Produces<BibleResponse>(StatusCodes.Status200OK)
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status500InternalServerError);

app.Run();

static bool IsValidPageSize(string? pageSize, string[]? fields)
{
    if (string.IsNullOrWhiteSpace(pageSize))
    {
        return true;
    }

    if (pageSize == "*")
    {
        return fields is { Length: > 0 and <= 3 };
    }

    return int.TryParse(pageSize, out var numericPageSize)
        && numericPageSize is >= 1 and <= 100;
}
