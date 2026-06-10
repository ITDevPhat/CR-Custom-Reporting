using Report.Metadata.Models;
using Report.QueryEngine.Expressions.Validation;

namespace Report.QueryEngine.Expressions.Dependencies;

public sealed class ExpressionDependencyGraphService
{
    public void ValidateNoCycles(SemanticModel model, string? candidateId, IReadOnlyCollection<string> candidateDependencies)
    {
        if (string.IsNullOrWhiteSpace(candidateId)) return;

        var graph = model.Metrics.ToDictionary(
            m => m.MetricId,
            m => ExtractMetricDependencies(m.Formula),
            StringComparer.OrdinalIgnoreCase);

        graph[candidateId] = candidateDependencies
            .Where(d => d.StartsWith("metric.", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in graph.Keys)
        {
            Visit(node, []);
        }

        void Visit(string node, List<string> path)
        {
            if (visited.Contains(node)) return;
            if (!visiting.Add(node))
            {
                var cycle = string.Join(" -> ", path.Concat([node]));
                throw new ExpressionValidationException("CIRCULAR_DEPENDENCY", $"Circular dependency detected: {cycle}.");
            }

            if (graph.TryGetValue(node, out var dependencies))
            {
                foreach (var dependency in dependencies.Where(graph.ContainsKey))
                {
                    Visit(dependency, [.. path, node]);
                }
            }

            visiting.Remove(node);
            visited.Add(node);
        }
    }

    private static List<string> ExtractMetricDependencies(string formula)
    {
        var result = new List<string>();
        var index = 0;
        while (index < formula.Length)
        {
            var start = formula.IndexOf('[', index);
            if (start < 0) break;
            var end = formula.IndexOf(']', start + 1);
            if (end < 0) break;
            var reference = formula[(start + 1)..end].Trim();
            if (reference.StartsWith("metric.", StringComparison.OrdinalIgnoreCase)) result.Add(reference);
            index = end + 1;
        }
        return result;
    }
}
