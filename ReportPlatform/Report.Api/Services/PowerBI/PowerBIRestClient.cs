using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Report.Api.DTOs.PowerBI;
using Report.Api.Options.PowerBI;

namespace Report.Api.Services.PowerBI;

public sealed class PowerBIRestClient : IPowerBIRestClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly IPowerBIAuthService _authService;
    private readonly ILogger<PowerBIRestClient> _logger;

    public PowerBIRestClient(
        HttpClient httpClient,
        IPowerBIAuthService authService,
        ILogger<PowerBIRestClient> logger)
    {
        _httpClient = httpClient;
        _authService = authService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PowerBIWorkspaceDto>> GetWorkspacesAsync(
        PowerBIOptions options,
        CancellationToken ct)
    {
        var response = await SendAsync(options, HttpMethod.Get, "groups", null, ct);
        var payload = await ReadJsonAsync<PowerBICollectionResponse<PowerBIWorkspaceDto>>(response, ct);
        return payload.Value;
    }

    public async Task<IReadOnlyList<PowerBIReportDto>> GetReportsAsync(
        PowerBIOptions options,
        string workspaceId,
        CancellationToken ct)
    {
        var response = await SendAsync(options, HttpMethod.Get, $"groups/{workspaceId}/reports", null, ct);
        var payload = await ReadJsonAsync<PowerBICollectionResponse<PowerBIReportDto>>(response, ct);
        return payload.Value;
    }

    public async Task<PowerBIReportDto> GetReportAsync(
        PowerBIOptions options,
        string workspaceId,
        string reportId,
        CancellationToken ct)
    {
        var response = await SendAsync(options, HttpMethod.Get, $"groups/{workspaceId}/reports/{reportId}", null, ct);
        return await ReadJsonAsync<PowerBIReportDto>(response, ct);
    }

    public async Task<IReadOnlyList<PowerBIDatasetDto>> GetDatasetsAsync(
        PowerBIOptions options,
        string workspaceId,
        CancellationToken ct)
    {
        var response = await SendAsync(options, HttpMethod.Get, $"groups/{workspaceId}/datasets", null, ct);
        var payload = await ReadJsonAsync<PowerBICollectionResponse<PowerBIDatasetDto>>(response, ct);
        return payload.Value;
    }

    public async Task<PowerBIEmbedTokenResponse> GenerateEmbedTokenAsync(
        PowerBIOptions options,
        string workspaceId,
        string reportId,
        string? datasetId,
        CancellationToken ct)
    {
        var report = await GetReportAsync(options, workspaceId, reportId, ct);
        if (string.IsNullOrWhiteSpace(report.EmbedUrl))
        {
            throw new PowerBIIntegrationException(
                "POWERBI_MISSING_EMBED_URL",
                "The report does not include an embed URL.",
                StatusCodes.Status502BadGateway);
        }

        var tokenRequest = new GenerateReportTokenRequest("View", false, datasetId ?? report.DatasetId);
        var response = await SendAsync(
            options,
            HttpMethod.Post,
            $"groups/{workspaceId}/reports/{reportId}/GenerateToken",
            tokenRequest,
            ct);

        var token = await ReadJsonAsync<GenerateReportTokenResponse>(response, ct);
        if (string.IsNullOrWhiteSpace(token.Token))
        {
            throw new PowerBIIntegrationException(
                "POWERBI_GENERATE_TOKEN_FAILURE",
                "Power BI returned an empty embed token.",
                StatusCodes.Status502BadGateway);
        }

        return new PowerBIEmbedTokenResponse(
            report.Id,
            report.Name,
            report.EmbedUrl,
            token.Token,
            "Embed",
            token.Expiration);
    }

    private async Task<HttpResponseMessage> SendAsync(
        PowerBIOptions options,
        HttpMethod method,
        string relativeUrl,
        object? body,
        CancellationToken ct)
    {
        var token = await _authService.AcquireAccessTokenAsync(options, ct);
        var requestUri = new Uri(new Uri(options.ApiBaseUrl.TrimEnd('/') + "/"), relativeUrl);

        using var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: SerializerOptions);
        }

        var response = await _httpClient.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var detail = await response.Content.ReadAsStringAsync(ct);
        _logger.LogWarning(
            "Power BI REST request failed. Method={Method} Url={Url} StatusCode={StatusCode}",
            method,
            requestUri.GetLeftPart(UriPartial.Path),
            (int)response.StatusCode);

        throw new PowerBIIntegrationException(
            MapRestError(response.StatusCode, relativeUrl),
            "Power BI REST API request failed.",
            MapStatusCode(response.StatusCode),
            SanitizeRestDetail(detail));
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var payload = await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, ct);
        return payload ?? throw new PowerBIIntegrationException(
            "POWERBI_EMPTY_RESPONSE",
            "Power BI REST API returned an empty response.",
            StatusCodes.Status502BadGateway);
    }

    private static string MapRestError(HttpStatusCode statusCode, string relativeUrl)
    {
        if (statusCode == HttpStatusCode.Forbidden)
        {
            return "POWERBI_WORKSPACE_ACCESS_DENIED";
        }

        if (statusCode == HttpStatusCode.NotFound && relativeUrl.Contains("/reports/", StringComparison.OrdinalIgnoreCase))
        {
            return "POWERBI_REPORT_NOT_FOUND";
        }

        if (statusCode == HttpStatusCode.NotFound && relativeUrl.Contains("groups/", StringComparison.OrdinalIgnoreCase))
        {
            return "POWERBI_WORKSPACE_NOT_FOUND";
        }

        if (relativeUrl.EndsWith("GenerateToken", StringComparison.OrdinalIgnoreCase))
        {
            return "POWERBI_GENERATE_TOKEN_FAILURE";
        }

        return $"POWERBI_REST_{(int)statusCode}";
    }

    private static int MapStatusCode(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized => StatusCodes.Status401Unauthorized,
            HttpStatusCode.Forbidden => StatusCodes.Status403Forbidden,
            HttpStatusCode.NotFound => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status502BadGateway
        };
    }

    private static string? SanitizeRestDetail(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return null;
        }

        return detail.Length <= 1000 ? detail : detail[..1000];
    }
}
