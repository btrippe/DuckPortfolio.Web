using System.Net.Http.Headers;
using System.Text.Json;
using DuckPortfolio.BibleApi.Models;
using DuckPortfolio.BibleApi.Options;
using Microsoft.Extensions.Options;

namespace DuckPortfolio.BibleApi.Services;

public sealed class YouVersionClient
{
    private readonly HttpClient _httpClient;
    private readonly YouVersionOptions _options;

    public YouVersionClient(HttpClient httpClient, IOptions<YouVersionOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<BiblePassageResponse> GetPassageAsync(
        int bibleId,
        string reference,
        BibleContentFormat? format,
        bool? includeHeadings,
        bool? includeNotes,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            BuildPassageUri(bibleId, reference, format, includeHeadings, includeNotes));

        request.Headers.Add("X-YVP-App-Key", _options.AppKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new YouVersionApiException(
                response.StatusCode,
                $"YouVersion returned {(int)response.StatusCode}: {responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement.Clone();

        return new BiblePassageResponse(
            bibleId,
            GetFirstString(root, "id"),
            GetFirstString(root, "reference") ?? reference,
            GetFirstString(root, "content", "text", "html"),
            ToApiFormat(format ?? BibleContentFormat.Text),
            includeHeadings,
            includeNotes,
            "YouVersion");
    }

    public async Task<BibleResponse> GetBibleAsync(
        int bibleId,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/v1/bibles/{bibleId}");

        request.Headers.Add("X-YVP-App-Key", _options.AppKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new YouVersionApiException(
                response.StatusCode,
                $"YouVersion returned {(int)response.StatusCode}: {responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement.Clone();

        return new BibleResponse(
            GetFirstInt32(root, "id") ?? bibleId,
            GetFirstString(root, "abbreviation"),
            GetFirstString(root, "localized_abbreviation"),
            GetFirstString(root, "title"),
            GetFirstString(root, "localized_title"),
            GetFirstString(root, "language_tag"),
            GetFirstString(root, "promotional_content"),
            GetFirstString(root, "copyright"),
            GetFirstString(root, "info"),
            GetFirstString(root, "publisher_url"),
            GetStringArray(root, "books"),
            GetFirstString(root, "youversion_deep_link"),
            GetFirstGuid(root, "organization_id"),
            "YouVersion");
    }

    private static string BuildPassageUri(
        int bibleId,
        string reference,
        BibleContentFormat? format,
        bool? includeHeadings,
        bool? includeNotes)
    {
        var queryParameters = new List<string>
        {
            $"format={Uri.EscapeDataString(ToApiFormat(format ?? BibleContentFormat.Text))}"
        };

        if (includeHeadings.HasValue)
        {
            queryParameters.Add($"include_headings={includeHeadings.Value.ToString().ToLowerInvariant()}");
        }

        if (includeNotes.HasValue)
        {
            queryParameters.Add($"include_notes={includeNotes.Value.ToString().ToLowerInvariant()}");
        }

        return $"/v1/bibles/{bibleId}/passages/{Uri.EscapeDataString(reference)}?{string.Join('&', queryParameters)}";
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.AppKey))
        {
            throw new InvalidOperationException(
                "Set YouVersion:AppKey locally or YouVersion__AppKey in Azure Container Apps.");
        }
    }

    private static string ToApiFormat(BibleContentFormat format) =>
        format == BibleContentFormat.Html ? "html" : "text";

    private static string? GetFirstString(JsonElement root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty(propertyName, out var value)
                && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static int? GetFirstInt32(JsonElement root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty(propertyName, out var value)
                && value.ValueKind == JsonValueKind.Number
                && value.TryGetInt32(out var number))
            {
                return number;
            }
        }

        return null;
    }

    private static Guid? GetFirstGuid(JsonElement root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty(propertyName, out var value)
                && value.ValueKind == JsonValueKind.String
                && Guid.TryParse(value.GetString(), out var guid))
            {
                return guid;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .OfType<string>()
            .ToArray();
    }
}
