using System.Text.Json.Serialization;

namespace Report.Api.DTOs.PowerBI;

public sealed record PowerBIWorkspaceDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("isReadOnly")] bool? IsReadOnly,
    [property: JsonPropertyName("isOnDedicatedCapacity")] bool? IsOnDedicatedCapacity,
    [property: JsonPropertyName("capacityId")] string? CapacityId);

public sealed record PowerBIReportDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("webUrl")] string? WebUrl,
    [property: JsonPropertyName("embedUrl")] string? EmbedUrl,
    [property: JsonPropertyName("datasetId")] string? DatasetId);

public sealed record PowerBIDatasetDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("configuredBy")] string? ConfiguredBy,
    [property: JsonPropertyName("isRefreshable")] bool? IsRefreshable,
    [property: JsonPropertyName("isEffectiveIdentityRequired")] bool? IsEffectiveIdentityRequired,
    [property: JsonPropertyName("isEffectiveIdentityRolesRequired")] bool? IsEffectiveIdentityRolesRequired);

internal sealed record PowerBICollectionResponse<T>(
    [property: JsonPropertyName("value")] IReadOnlyList<T> Value);

internal sealed record GenerateReportTokenRequest(
    [property: JsonPropertyName("accessLevel")] string AccessLevel,
    [property: JsonPropertyName("allowSaveAs")] bool AllowSaveAs = false,
    [property: JsonPropertyName("datasetId")] string? DatasetId = null);

internal sealed record GenerateReportTokenResponse(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("tokenId")] string? TokenId,
    [property: JsonPropertyName("expiration")] DateTimeOffset Expiration);
