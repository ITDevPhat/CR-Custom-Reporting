using Microsoft.AspNetCore.Mvc;
using Report.Api.Services;
using Report.Contracts.Connections;
using Report.Infrastructure.Connections;
using Report.Metadata.Connections;
using Report.Metadata.Stores;

namespace Report.Api.Controllers;

[ApiController]
[Route("api/datasets")]
public sealed class DatasetsController : ControllerBase
{
    private readonly DatasetMetadataService _metadataService;
    private readonly IConnectionRegistry _connectionRegistry;
    private readonly IDatasetRegistry _datasetRegistry;
    private readonly SqlServerSchemaDiscoveryService _discoveryService;
    private readonly SemanticMetadataGenerator _metadataGenerator;
    private readonly MetadataConsistencyValidator _metadataConsistencyValidator;
    private readonly ILogger<DatasetsController> _logger;

    public DatasetsController(
        DatasetMetadataService metadataService,
        IConnectionRegistry connectionRegistry,
        IDatasetRegistry datasetRegistry,
        SqlServerSchemaDiscoveryService discoveryService,
        SemanticMetadataGenerator metadataGenerator,
        MetadataConsistencyValidator metadataConsistencyValidator,
        ILogger<DatasetsController> logger)
    {
        _metadataService = metadataService;
        _connectionRegistry = connectionRegistry;
        _datasetRegistry = datasetRegistry;
        _discoveryService = discoveryService;
        _metadataGenerator = metadataGenerator;
        _metadataConsistencyValidator = metadataConsistencyValidator;
        _logger = logger;
    }

    [HttpGet("{datasetId}/metadata")]
    public async Task<IActionResult> GetMetadata(
        string datasetId,
        CancellationToken ct)
    {
        var metadata = await _metadataService.GetMetadataAsync(datasetId, ct);
        return Ok(metadata);
    }

    [HttpPost("register-from-tables")]
    public async Task<IActionResult> RegisterFromTables(
        [FromBody] RegisterDatasetRequest request,
        CancellationToken ct)
    {
        if (request.SelectedTables.Count == 0)
        {
            return BadRequest("At least one table must be selected.");
        }

        var connection = _connectionRegistry.Save(SqlServerConnectionFactory.ToDefinition(request.Connection));
        var discovered = await _discoveryService.DiscoverAsync(connection, ct);
        var model = _metadataGenerator.Generate(request.DatasetName, discovered, request.SelectedTables);
        var consistency = _metadataConsistencyValidator.Validate(discovered.Tables, request.SelectedTables, model);

        LogRegistrationConsistency(discovered.Tables, request.SelectedTables, model);

        foreach (var warning in consistency.Warnings)
        {
            _logger.LogWarning("Metadata consistency warning: {Warning}", warning);
        }

        var dataset = _datasetRegistry.Save(request.DatasetName, connection, model);
        var metadata = await _metadataService.GetMetadataAsync(dataset.DatasetId, ct);

        return Ok(new RegisterDatasetResponse
        {
            DatasetId = dataset.DatasetId,
            ConnectionId = connection.ConnectionId,
            Metadata = metadata,
            Warnings = consistency.Warnings,
            Consistency = consistency.Consistency,
            DebugFields = model.Fields
                .OrderBy(field => field.TableId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(field => field.OrdinalPosition)
                .Select(field => new MetadataFieldDebugDto
                {
                    FieldId = field.FieldId,
                    PhysicalColumn = field.PhysicalColumn,
                    SqlDataType = string.IsNullOrWhiteSpace(field.SqlDataType) ? field.DataType : field.SqlDataType,
                    Role = field.Role,
                    SemanticType = field.SemanticType,
                    IsPrimaryKey = field.IsPrimaryKey,
                    IsForeignKey = field.IsForeignKey,
                    ParticipatesInRelationship = field.ParticipatesInRelationship,
                    IsDraggable = field.IsDraggable,
                    ClassificationReason = field.ClassificationReason
                })
                .ToList()
        });
    }

    private void LogRegistrationConsistency(
        IReadOnlyCollection<TableDto> discoveredTables,
        IReadOnlyCollection<SelectedTableDto> selectedTables,
        Report.Metadata.Models.SemanticModel model)
    {
        foreach (var selected in selectedTables)
        {
            var table = discoveredTables.FirstOrDefault(t =>
                t.Schema.Equals(selected.Schema, StringComparison.OrdinalIgnoreCase) &&
                t.Table.Equals(selected.Table, StringComparison.OrdinalIgnoreCase));

            if (table is null)
            {
                _logger.LogWarning("Selected table {Schema}.{Table} was not present in discovered schema.", selected.Schema, selected.Table);
                continue;
            }

            var tableId = selected.Schema.Equals("dbo", StringComparison.OrdinalIgnoreCase)
                ? selected.Table
                : $"{selected.Schema}.{selected.Table}";

            var registeredColumns = model.Fields
                .Where(field => field.TableId.Equals(tableId, StringComparison.OrdinalIgnoreCase))
                .Select(field => field.PhysicalColumn)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missingColumns = table.Columns
                .OrderBy(column => column.OrdinalPosition)
                .Where(column => !registeredColumns.Contains(column.Column))
                .Select(column => column.Column)
                .ToList();

            _logger.LogInformation(
                "Table {TableId}: discovered {DiscoveredColumnCount} columns, registered {RegisteredFieldCount} fields. Missing columns: {MissingColumns}.",
                tableId,
                table.Columns.Count,
                registeredColumns.Count,
                missingColumns.Count == 0 ? "none" : string.Join(", ", missingColumns));
        }
    }
}
