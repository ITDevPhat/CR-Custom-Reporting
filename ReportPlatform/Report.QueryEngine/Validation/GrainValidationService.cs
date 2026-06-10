using Report.Contracts.Semantic;
using Report.Metadata.Models;
using Report.QueryEngine.Context;
using Report.QueryEngine.Measures;
using Report.QueryEngine.Relationships;

namespace Report.QueryEngine.Validation;

public sealed class GrainValidationService
{
    public GrainValidationResult Validate(
        EvaluationContext context,
        List<ExpandedMeasure> measures,
        JoinPlan joinPlan,
        SemanticModel model)
    {
        var errors = new List<ValidationMessage>();
        var warnings = new List<ValidationMessage>();

        foreach (var tableId in joinPlan.Joins.SelectMany(j => new[] { j.FromTableId, j.ToTableId }).Append(joinPlan.BaseTableId).Distinct())
        {
            var table = model.Tables.FirstOrDefault(t => t.TableId == tableId);
            if (table is null || string.IsNullOrWhiteSpace(table.Grain))
            {
                warnings.Add(new ValidationMessage { Code = "GRAIN_VIOLATION", Message = $"Table '{tableId}' does not declare a grain." });
            }
        }

        foreach (var rel in model.Relationships.Where(r => r.IsActive && r.Cardinality == "N:N"))
        {
            errors.Add(new ValidationMessage { Code = "UNSUPPORTED_MANY_TO_MANY", Message = $"Many-to-many relationship '{rel.RelationshipId}' cannot be used by automatic planning." });
        }

        foreach (var metric in context.Measures.Where(m => m.AggregationBehavior == "ratio"))
        {
            warnings.Add(new ValidationMessage { Code = "INVALID_AGGREGATION_BEHAVIOR", Message = $"Ratio metric '{metric.DisplayName}' is calculated at query time and should not be summed again." });
        }

        if (joinPlan.Joins.GroupBy(j => $"{j.FromTableId}->{j.ToTableId}").Any(g => g.Count() > 1))
        {
            errors.Add(new ValidationMessage { Code = "FANOUT_RISK", Message = "Multiple relationship paths may cause fanout risk." });
        }

        return new GrainValidationResult
        {
            Valid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }
}
