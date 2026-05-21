using System.Text.RegularExpressions;
using Report.Contracts.Semantic;
using Report.Metadata.Models;
using Report.Metadata.Stores;

namespace Report.Api.Services;

public sealed partial class SemanticModelMutationService
{
    private static readonly HashSet<string> Roles = new(StringComparer.OrdinalIgnoreCase) { "dimension", "measure_candidate", "key", "hidden", "calculated_field" };
    private static readonly HashSet<string> Aggregations = new(StringComparer.OrdinalIgnoreCase) { "none", "SUM", "COUNT", "COUNT_DISTINCT", "AVG", "MIN", "MAX" };
    private readonly ISemanticModelStore _modelStore;
    private readonly IDatasetRegistry _datasetRegistry;

    public SemanticModelMutationService(ISemanticModelStore modelStore, IDatasetRegistry datasetRegistry)
    {
        _modelStore = modelStore;
        _datasetRegistry = datasetRegistry;
    }

    public async Task<SemanticModel> LoadAsync(string datasetId, CancellationToken ct)
    {
        var model = await _modelStore.LoadAsync(datasetId, ct);
        if (_datasetRegistry.Find(datasetId) is null)
        {
            _datasetRegistry.SaveExisting(datasetId, model.DisplayName, model.ConnectionId, model);
        }
        return model;
    }

    public async Task<List<SemanticField>> GetFieldsAsync(string datasetId, CancellationToken ct) =>
        (await LoadAsync(datasetId, ct)).Fields;

    public async Task<SemanticField> UpdateFieldAsync(string datasetId, string fieldId, UpdateFieldRequest request, CancellationToken ct)
    {
        var model = await LoadAsync(datasetId, ct);
        var index = model.Fields.FindIndex(f => f.FieldId.Equals(fieldId, StringComparison.OrdinalIgnoreCase));
        if (index < 0) throw new InvalidOperationException($"Field '{fieldId}' was not found.");
        if (!Roles.Contains(request.Role)) throw new InvalidOperationException("Invalid field role.");
        if (!Aggregations.Contains(request.DefaultAggregation)) throw new InvalidOperationException("Invalid default aggregation.");
        if (request.DefaultAggregation is "SUM" or "AVG" && !IsNumeric(model.Fields[index].DataType)) throw new InvalidOperationException("SUM/AVG are invalid for non-numeric fields.");

        var role = request.Role.Equals("hidden", StringComparison.OrdinalIgnoreCase) ? "hidden" : request.Role;
        var updated = model.Fields[index].withField(
            displayName: request.DisplayName,
            role: role,
            semanticType: request.SemanticType,
            aggregation: request.DefaultAggregation,
            format: request.Format,
            hidden: request.IsHidden || role == "hidden",
            draggable: role != "hidden" && request.IsDraggable,
            grain: request.Grain);
        model.Fields[index] = updated;
        Save(model);
        return updated;
    }

    public async Task<List<SemanticMetric>> GetMetricsAsync(string datasetId, CancellationToken ct) =>
        (await LoadAsync(datasetId, ct)).Metrics.Where(m => !m.IsHidden).ToList();

    public async Task<ValidationResponse> ValidateMetricAsync(string datasetId, MetricRequest request, CancellationToken ct)
    {
        var model = await LoadAsync(datasetId, ct);
        var errors = ValidateMetric(model, request).ToList();
        return new ValidationResponse { Valid = errors.Count == 0, Errors = errors };
    }

    public async Task<SemanticMetric> CreateMetricAsync(string datasetId, MetricRequest request, CancellationToken ct)
    {
        var model = await LoadAsync(datasetId, ct);
        var errors = ValidateMetric(model, request).ToList();
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors));
        var metric = new SemanticMetric
        {
            MetricId = Slug(request.DisplayName),
            DatasetId = datasetId,
            DisplayName = request.DisplayName,
            Formula = request.Formula,
            BaseTableId = string.IsNullOrWhiteSpace(request.BaseTableId) ? InferMetricBaseTable(model, request.Formula) : request.BaseTableId,
            AggregationBehavior = request.AggregationBehavior,
            DataType = request.DataType,
            Format = request.Format,
            IsHidden = request.IsHidden,
            IsDraggable = request.IsDraggable
        };
        if (model.Metrics.Any(m => m.MetricId == metric.MetricId)) metric = metric.withMetricId($"{metric.MetricId}_{Guid.NewGuid():N}"[..^24]);
        model.Metrics.Add(metric);
        Save(model);
        return metric;
    }

    public async Task<SemanticMetric> UpdateMetricAsync(string datasetId, string metricId, MetricRequest request, CancellationToken ct)
    {
        var model = await LoadAsync(datasetId, ct);
        var index = model.Metrics.FindIndex(m => m.MetricId == metricId);
        if (index < 0) throw new InvalidOperationException($"Metric '{metricId}' was not found.");
        var errors = ValidateMetric(model, request).ToList();
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors));
        var metric = new SemanticMetric
        {
            MetricId = metricId,
            DatasetId = datasetId,
            DisplayName = request.DisplayName,
            Formula = request.Formula,
            BaseTableId = string.IsNullOrWhiteSpace(request.BaseTableId) ? InferMetricBaseTable(model, request.Formula) : request.BaseTableId,
            AggregationBehavior = request.AggregationBehavior,
            DataType = request.DataType,
            Format = request.Format,
            IsHidden = request.IsHidden,
            IsDraggable = request.IsDraggable
        };
        model.Metrics[index] = metric;
        Save(model);
        return metric;
    }

    public async Task DeleteMetricAsync(string datasetId, string metricId, CancellationToken ct)
    {
        var model = await LoadAsync(datasetId, ct);
        model.Metrics.RemoveAll(m => m.MetricId == metricId);
        Save(model);
    }

    public async Task<List<SemanticField>> GetDerivedFieldsAsync(string datasetId, CancellationToken ct) =>
        (await LoadAsync(datasetId, ct)).Fields.Where(f => f.IsDerived).ToList();

    public async Task<ValidationResponse> ValidateDerivedAsync(string datasetId, DerivedFieldRequest request, CancellationToken ct)
    {
        var model = await LoadAsync(datasetId, ct);
        var errors = ValidateDerived(model, request).ToList();
        return new ValidationResponse { Valid = errors.Count == 0, Errors = errors.Select(e => e.Message).ToList(), Details = errors };
    }

    public async Task<SemanticField> CreateDerivedAsync(string datasetId, DerivedFieldRequest request, CancellationToken ct)
    {
        var model = await LoadAsync(datasetId, ct);
        var errors = ValidateDerived(model, request).ToList();
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors.Select(e => e.Message)));
        var field = new SemanticField
        {
            FieldId = $"{Slug(request.BaseTableId)}.{Slug(request.DisplayName)}",
            DatasetId = datasetId,
            TableId = request.BaseTableId,
            PhysicalTable = request.BaseTableId,
            PhysicalColumn = Slug(request.DisplayName),
            DisplayName = request.DisplayName,
            DataType = request.DataType,
            Role = "dimension",
            Grain = "",
            SemanticType = request.SemanticType,
            Format = request.Format,
            IsHidden = request.IsHidden,
            IsDraggable = request.IsDraggable,
            Expression = request.Expression,
            BaseTableId = request.BaseTableId,
            IsDerived = true
        };
        model.Fields.Add(field);
        Save(model);
        return field;
    }

    public async Task DeleteDerivedAsync(string datasetId, string fieldId, CancellationToken ct)
    {
        var model = await LoadAsync(datasetId, ct);
        model.Fields.RemoveAll(f => f.FieldId == fieldId && f.IsDerived);
        Save(model);
    }

    private void Save(SemanticModel model) => _datasetRegistry.SaveExisting(model.DatasetId, model.DisplayName, model.ConnectionId, model);

    private static IEnumerable<string> ValidateMetric(SemanticModel model, MetricRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName)) yield return "Metric display name is required.";
        if (RawSqlTokens().IsMatch(request.Formula)) yield return "Metric formula contains unsupported SQL tokens.";
        var refs = FieldRefs().Matches(request.Formula).Select(m => m.Groups[1].Value).ToList();
        if (refs.Count == 0) yield return "Metric formula must reference at least one field.";
        foreach (var fieldId in refs)
        {
            var field = model.Fields.FirstOrDefault(f => f.FieldId.Equals(fieldId, StringComparison.OrdinalIgnoreCase));
            if (field is null) yield return $"Unknown field reference: {fieldId}.";
        }
        foreach (Match aggregate in AggregateRefs().Matches(request.Formula))
        {
            var fn = aggregate.Groups[1].Value.ToUpperInvariant();
            var fieldId = aggregate.Groups[2].Value;
            var field = model.Fields.FirstOrDefault(f => f.FieldId.Equals(fieldId, StringComparison.OrdinalIgnoreCase));
            if (field is not null && fn is "SUM" or "AVG" && !IsNumeric(field.DataType)) yield return $"{fn} is invalid for {field.DataType}.";
        }
    }

    private static IEnumerable<ValidationMessage> ValidateDerived(SemanticModel model, DerivedFieldRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            yield return new ValidationMessage { Code = "INVALID_DERIVED_FIELD_EXPRESSION", Message = "Derived field display name is required." };
        }
        if (AggregateRefs().IsMatch(request.Expression) || MetricRef().IsMatch(request.Expression))
        {
            yield return new ValidationMessage { Code = "INVALID_DERIVED_FIELD_EXPRESSION", Message = "Derived fields cannot reference measures or aggregate functions. Create a measure instead." };
        }
        if (RawSqlTokens().IsMatch(request.Expression))
        {
            yield return new ValidationMessage { Code = "INVALID_DERIVED_FIELD_EXPRESSION", Message = "Derived expression contains unsupported SQL tokens." };
        }
        var refs = FieldRefs().Matches(request.Expression).Select(m => m.Groups[1].Value).ToList();
        if (refs.Count == 0) yield return new ValidationMessage { Code = "INVALID_DERIVED_FIELD_EXPRESSION", Message = "Derived expression must reference at least one field." };
        foreach (var fieldId in refs)
        {
            if (fieldId.StartsWith("metric.metric.", StringComparison.OrdinalIgnoreCase))
            {
                yield return new ValidationMessage { Code = "UNKNOWN_METRIC_REFERENCE", Message = $"Unknown metric reference: {fieldId}." };
                continue;
            }
            if (fieldId.StartsWith("metric.", StringComparison.OrdinalIgnoreCase))
            {
                yield return new ValidationMessage { Code = "INVALID_DERIVED_FIELD_EXPRESSION", Message = "Derived fields cannot reference measures or aggregate functions. Create a measure instead." };
                continue;
            }
            var field = model.Fields.FirstOrDefault(f => f.FieldId.Equals(fieldId, StringComparison.OrdinalIgnoreCase));
            if (field is null) yield return new ValidationMessage { Code = "UNKNOWN_FIELD_REFERENCE", Message = $"Unknown field reference: {fieldId}." };
            else if (!field.TableId.Equals(request.BaseTableId, StringComparison.OrdinalIgnoreCase)) yield return new ValidationMessage { Code = "INVALID_DERIVED_FIELD_EXPRESSION", Message = "Derived field references must belong to the same base table." };
        }
    }

    private static string InferMetricBaseTable(SemanticModel model, string formula)
    {
        return FieldRefs().Matches(formula)
            .Select(m => model.Fields.First(f => f.FieldId.Equals(m.Groups[1].Value, StringComparison.OrdinalIgnoreCase)).TableId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Single();
    }

    private static bool IsNumeric(string dataType) => dataType.ToLowerInvariant() is "tinyint" or "smallint" or "int" or "bigint" or "decimal" or "numeric" or "float" or "real" or "money";
    private static string Slug(string value) => SlugCleaner().Replace(value.ToLowerInvariant(), "_").Trim('_');

    [GeneratedRegex(@"\[([^\]]+)\]")]
    private static partial Regex FieldRefs();
    [GeneratedRegex(@"\b(SUM|COUNT|COUNT_DISTINCT|AVG|MIN|MAX)\s*\(\s*\[([^\]]+)\]\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex AggregateRefs();
    [GeneratedRegex(@"\[\s*metric\.", RegexOptions.IgnoreCase)]
    private static partial Regex MetricRef();
    [GeneratedRegex(@"\b(SELECT|FROM|JOIN|INSERT|UPDATE|DELETE|DROP|ALTER|EXEC|;|--)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RawSqlTokens();
    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SlugCleaner();
}

file static class SemanticMutationExtensions
{
    public static SemanticField withField(this SemanticField f, string displayName, string role, string semanticType, string aggregation, string format, bool hidden, bool draggable, string grain) => new()
    {
        FieldId = f.FieldId, DatasetId = f.DatasetId, TableId = f.TableId, PhysicalSchema = f.PhysicalSchema,
        PhysicalTable = f.PhysicalTable, PhysicalColumn = f.PhysicalColumn, OrdinalPosition = f.OrdinalPosition,
        IsNullable = f.IsNullable, IsPrimaryKey = f.IsPrimaryKey, IsForeignKey = f.IsForeignKey,
        ParticipatesInRelationship = f.ParticipatesInRelationship, IsUnique = f.IsUnique, ReferencedSchema = f.ReferencedSchema,
        ReferencedTable = f.ReferencedTable, ReferencedColumn = f.ReferencedColumn, ForeignKeyName = f.ForeignKeyName,
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? f.DisplayName : displayName, DataType = f.DataType, SqlDataType = f.SqlDataType,
        CharacterMaximumLength = f.CharacterMaximumLength, NumericPrecision = f.NumericPrecision, NumericScale = f.NumericScale, DatetimePrecision = f.DatetimePrecision,
        Role = role, Grain = grain,
        SemanticType = semanticType, DefaultAggregation = aggregation, Format = format, IsHidden = hidden, IsDraggable = draggable,
        Expression = f.Expression, BaseTableId = f.BaseTableId, IsDerived = f.IsDerived, ClassificationReason = f.ClassificationReason
    };
    public static SemanticMetric withMetricId(this SemanticMetric m, string id) => new()
    {
        MetricId = id, DatasetId = m.DatasetId, DisplayName = m.DisplayName, Formula = m.Formula, BaseTableId = m.BaseTableId,
        AggregationBehavior = m.AggregationBehavior, DataType = m.DataType, Format = m.Format, IsHidden = m.IsHidden, IsDraggable = m.IsDraggable
    };
}
