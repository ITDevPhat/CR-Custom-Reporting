using Report.Contracts.Relationships;
using Report.Metadata.Models;
using Report.Metadata.Stores;

namespace Report.Api.Services;

public sealed class DatasetRelationshipService
{
    private static readonly HashSet<string> Cardinalities = new(StringComparer.OrdinalIgnoreCase) { "1:1", "1:N", "N:1", "N:N" };
    private readonly ISemanticModelStore _modelStore;
    private readonly IDatasetRegistry _datasetRegistry;

    public DatasetRelationshipService(ISemanticModelStore modelStore, IDatasetRegistry datasetRegistry)
    {
        _modelStore = modelStore;
        _datasetRegistry = datasetRegistry;
    }

    public async Task<List<RelationshipDto>> ListAsync(string datasetId, CancellationToken ct)
    {
        var model = await LoadAndEnsureRegisteredAsync(datasetId, ct);
        return BuildDtos(model.Relationships);
    }

    public async Task<RelationshipDto> CreateAsync(string datasetId, CreateRelationshipRequest request, CancellationToken ct)
    {
        var model = await LoadAndEnsureRegisteredAsync(datasetId, ct);
        Validate(model, request);
        var relationship = BuildRelationship(datasetId, request, $"rel_{Guid.NewGuid():N}", "manual", 1.0m);
        model.Relationships.Add(relationship);
        if (relationship.IsActive)
        {
            ApplyActivationRule(model.Relationships, relationship.RelationshipId);
        }

        return BuildDtos(model.Relationships).First(r => r.RelationshipId == relationship.RelationshipId);
    }

    public async Task<RelationshipDto> UpdateAsync(string datasetId, string relationshipId, UpdateRelationshipRequest request, CancellationToken ct)
    {
        var model = await LoadAndEnsureRegisteredAsync(datasetId, ct);
        var index = model.Relationships.FindIndex(r => r.RelationshipId == relationshipId);
        if (index < 0) throw new InvalidOperationException($"Relationship '{relationshipId}' was not found.");
        Validate(model, request, relationshipId);
        var updated = BuildRelationship(datasetId, request, relationshipId, model.Relationships[index].Source, model.Relationships[index].Confidence);
        model.Relationships[index] = updated;
        if (request.IsActive)
        {
            ApplyActivationRule(model.Relationships, relationshipId);
        }

        return BuildDtos(model.Relationships).First(r => r.RelationshipId == relationshipId);
    }

    public async Task<List<RelationshipDto>> ActivateAsync(string datasetId, string relationshipId, CancellationToken ct)
    {
        var model = await LoadAndEnsureRegisteredAsync(datasetId, ct);
        ApplyActivationRule(model.Relationships, relationshipId);
        return BuildDtos(model.Relationships);
    }

    public async Task DeleteAsync(string datasetId, string relationshipId, CancellationToken ct)
    {
        var model = await LoadAndEnsureRegisteredAsync(datasetId, ct);
        var removed = model.Relationships.RemoveAll(r => r.RelationshipId == relationshipId);
        if (removed == 0) throw new InvalidOperationException($"Relationship '{relationshipId}' was not found.");
    }

    public async Task<AutodetectRelationshipsResponse> AutodetectAsync(string datasetId, AutodetectRelationshipsRequest request, CancellationToken ct)
    {
        var model = await LoadAndEnsureRegisteredAsync(datasetId, ct);
        var skipped = 0;
        var detected = new List<SemanticRelationship>();

        foreach (var relationship in InferByName(model))
        {
            var exists = model.Relationships.Any(r => SameEndpoint(r, relationship));
            if (exists && !request.IncludeExisting)
            {
                skipped++;
                continue;
            }

            detected.Add(relationship);
            if (!exists)
            {
                model.Relationships.Add(relationship);
            }
        }

        NormalizeGroupActives(model.Relationships);

        var dtos = BuildDtos(detected);
        return new AutodetectRelationshipsResponse
        {
            Relationships = dtos,
            Summary = new AutodetectRelationshipsSummary
            {
                Detected = dtos.Count,
                DatabaseForeignKeys = dtos.Count(r => r.Source == "database_fk"),
                InferredByName = dtos.Count(r => r.Source == "inferred"),
                SkippedExisting = skipped,
                Warnings = dtos.Where(r => !string.IsNullOrWhiteSpace(r.Warning)).Select(r => r.Warning!).Distinct().ToList()
            }
        };
    }

    private async Task<SemanticModel> LoadAndEnsureRegisteredAsync(string datasetId, CancellationToken ct)
    {
        var model = await _modelStore.LoadAsync(datasetId, ct);
        if (_datasetRegistry.Find(datasetId) is null)
        {
            _datasetRegistry.SaveExisting(datasetId, model.DisplayName, model.ConnectionId, model);
        }

        return model;
    }

    private static void Validate(SemanticModel model, CreateRelationshipRequest request, string? relationshipId = null)
    {
        if (!Cardinalities.Contains(request.Cardinality)) throw new InvalidOperationException("Unsupported cardinality.");
        if (request.FromTableId == request.ToTableId) throw new InvalidOperationException("Self relationships are not supported for this MVP.");
        var from = model.Fields.FirstOrDefault(f => f.TableId == request.FromTableId && f.PhysicalColumn == request.FromColumn);
        var to = model.Fields.FirstOrDefault(f => f.TableId == request.ToTableId && f.PhysicalColumn == request.ToColumn);
        if (from is null) throw new InvalidOperationException("From column was not found.");
        if (to is null) throw new InvalidOperationException("To column was not found.");
        if (!Compatible(from.DataType, to.DataType)) throw new InvalidOperationException("Relationship column data types are not compatible.");
    }

    private static SemanticRelationship BuildRelationship(string datasetId, CreateRelationshipRequest request, string id, string source, decimal confidence)
    {
        var warning = BuildWarning(request, source);
        return new SemanticRelationship
        {
            RelationshipId = id,
            DatasetId = datasetId,
            FromTableId = request.FromTableId,
            FromColumn = request.FromColumn,
            ToTableId = request.ToTableId,
            ToColumn = request.ToColumn,
            Cardinality = request.Cardinality,
            JoinType = request.JoinType,
            CrossFilterDirection = request.CrossFilterDirection,
            IsActive = request.IsActive && request.Cardinality != "N:N",
            IsPrimary = request.IsActive && request.IsPrimary,
            Source = source,
            Confidence = confidence,
            Status = request.IsActive && request.Cardinality != "N:N" ? warning is null ? "active" : "warning" : "inactive",
            Warning = warning
        };
    }

    private static IEnumerable<SemanticRelationship> InferByName(SemanticModel model)
    {
        var facts = model.Tables.Where(t => t.TableType == "fact" || t.TableId.StartsWith("Fact", StringComparison.OrdinalIgnoreCase));
        var dims = model.Tables.Where(t => t.TableType == "dimension" || t.TableId.StartsWith("Dim", StringComparison.OrdinalIgnoreCase));

        foreach (var fact in facts)
        {
            var factKeys = model.Fields.Where(f => f.TableId == fact.TableId && f.PhysicalColumn.EndsWith("Key", StringComparison.OrdinalIgnoreCase));
            foreach (var factKey in factKeys)
            {
                foreach (var dim in dims)
                {
                    var dimKey = model.Fields.FirstOrDefault(f => f.TableId == dim.TableId &&
                        (f.PhysicalColumn == factKey.PhysicalColumn ||
                         (dim.TableId.Contains("Date", StringComparison.OrdinalIgnoreCase) && f.PhysicalColumn == "DateKey" && factKey.PhysicalColumn.EndsWith("DateKey"))));
                    if (dimKey is null || !Compatible(factKey.DataType, dimKey.DataType)) continue;

                    var confidence = factKey.PhysicalColumn == dimKey.PhysicalColumn ? 1.0m : 0.85m;
                    if (confidence < 0.75m) continue;

                    yield return new SemanticRelationship
                    {
                        RelationshipId = $"rel_{Guid.NewGuid():N}",
                        DatasetId = model.DatasetId,
                        FromTableId = fact.TableId,
                        FromColumn = factKey.PhysicalColumn,
                        ToTableId = dim.TableId,
                        ToColumn = dimKey.PhysicalColumn,
                        Cardinality = "N:1",
                        JoinType = "INNER",
                        CrossFilterDirection = "single",
                        IsActive = true,
                        IsPrimary = true,
                        Source = "inferred",
                        Confidence = confidence,
                        Status = "warning",
                        Warning = "Inferred relationship. Please verify before production use."
                    };
                }
            }
        }
    }

    private static string? BuildWarning(CreateRelationshipRequest request, string source)
    {
        if (request.Cardinality == "N:N") return "Many-to-many relationship is not supported by automatic query planning.";
        if (request.CrossFilterDirection == "both") return "Bidirectional filter propagation is stored but not fully supported by query planner yet.";
        if (source == "inferred") return "Inferred relationship. Please verify before production use.";
        return null;
    }

    private static bool SameEndpoint(SemanticRelationship relationship, CreateRelationshipRequest request) =>
        relationship.FromTableId == request.FromTableId && relationship.FromColumn == request.FromColumn &&
        relationship.ToTableId == request.ToTableId && relationship.ToColumn == request.ToColumn;

    private static bool SameEndpoint(SemanticRelationship left, SemanticRelationship right) =>
        left.FromTableId == right.FromTableId && left.FromColumn == right.FromColumn &&
        left.ToTableId == right.ToTableId && left.ToColumn == right.ToColumn;

    private static bool Compatible(string left, string right)
    {
        if (left.Equals(right, StringComparison.OrdinalIgnoreCase)) return true;
        return IsNumber(left) && IsNumber(right);
    }

    private static bool IsNumber(string type) => type.ToLowerInvariant() is "tinyint" or "smallint" or "int" or "bigint" or "decimal" or "numeric" or "float" or "real" or "money";

    public static RelationshipDto ToDto(SemanticRelationship relationship, int groupConflictCount = 1, int groupActiveCount = 0, string? groupWarning = null) => new()
    {
        RelationshipId = relationship.RelationshipId,
        DatasetId = relationship.DatasetId,
        FromTableId = relationship.FromTableId,
        FromColumn = relationship.FromColumn,
        ToTableId = relationship.ToTableId,
        ToColumn = relationship.ToColumn,
        Cardinality = relationship.Cardinality,
        JoinType = relationship.JoinType,
        CrossFilterDirection = relationship.CrossFilterDirection,
        IsActive = relationship.IsActive,
        IsPrimary = relationship.IsPrimary,
        Source = relationship.Source,
        Confidence = relationship.Confidence,
        Status = relationship.Status,
        Warning = groupWarning ?? relationship.Warning,
        RelationshipGroupKey = BuildGroupKey(relationship.FromTableId, relationship.ToTableId),
        GroupConflictCount = groupConflictCount,
        GroupActiveCount = groupActiveCount
    };

    private static List<RelationshipDto> BuildDtos(IEnumerable<SemanticRelationship> relationships)
    {
        var list = relationships.ToList();
        var groups = list.GroupBy(r => BuildGroupKey(r.FromTableId, r.ToTableId))
            .ToDictionary(g => g.Key, g => g.ToList());

        return list.Select(r =>
        {
            var key = BuildGroupKey(r.FromTableId, r.ToTableId);
            var group = groups[key];
            var active = group.Count(x => x.IsActive);
            return ToDto(r, group.Count, active, BuildGroupWarning(group, r));
        }).ToList();
    }

    private static void ApplyActivationRule(List<SemanticRelationship> relationships, string relationshipId)
    {
        var target = relationships.FirstOrDefault(r => r.RelationshipId == relationshipId)
            ?? throw new InvalidOperationException($"Relationship '{relationshipId}' was not found.");
        var key = BuildGroupKey(target.FromTableId, target.ToTableId);
        for (var i = 0; i < relationships.Count; i++)
        {
            var rel = relationships[i];
            if (BuildGroupKey(rel.FromTableId, rel.ToTableId) != key) continue;
            var isTarget = rel.RelationshipId == relationshipId;
            relationships[i] = new SemanticRelationship
            {
                RelationshipId = rel.RelationshipId,
                DatasetId = rel.DatasetId,
                FromTableId = rel.FromTableId,
                FromColumn = rel.FromColumn,
                ToTableId = rel.ToTableId,
                ToColumn = rel.ToColumn,
                JoinType = rel.JoinType,
                Cardinality = rel.Cardinality,
                CrossFilterDirection = rel.CrossFilterDirection,
                IsActive = isTarget,
                IsPrimary = isTarget,
                Source = rel.Source,
                Confidence = rel.Confidence,
                Status = isTarget ? "active" : "inactive",
                Warning = rel.Warning
            };
        }
    }

    private static void NormalizeGroupActives(List<SemanticRelationship> relationships)
    {
        foreach (var group in relationships.GroupBy(r => BuildGroupKey(r.FromTableId, r.ToTableId)))
        {
            SemanticRelationship? chosen = null;
            if (group.Count() == 1) chosen = group.First();
            else
            {
                chosen = group.FirstOrDefault(r => r.FromColumn.Equals("OrderDateKey", StringComparison.OrdinalIgnoreCase))
                    ?? group.FirstOrDefault(r => r.FromColumn.Contains("Order", StringComparison.OrdinalIgnoreCase) && r.FromColumn.Contains("Date", StringComparison.OrdinalIgnoreCase));
            }

            if (chosen is null) continue;
            ApplyActivationRule(relationships, chosen.RelationshipId);
        }
    }

    private static string BuildGroupKey(string fromTableId, string toTableId) => $"{fromTableId}->{toTableId}";

    private static string? BuildGroupWarning(List<SemanticRelationship> group, SemanticRelationship current)
    {
        if (group.Count == 1) return current.Warning;
        var active = group.Where(g => g.IsActive).ToList();
        if (active.Count == 0) return "No active relationship is selected for this table pair.";
        if (active.Count > 1) return "Multiple active relationships detected for this table pair. Make exactly one active.";
        return active[0].RelationshipId == current.RelationshipId
            ? "Multiple relationships exist for this table pair. Only this one is active."
            : "Multiple relationships exist for this table pair. This relationship is inactive.";
    }
}
