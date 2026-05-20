using Report.Metadata.Models;

namespace Report.Metadata.Stores;

public interface ISemanticModelStore
{
    Task<SemanticModel> LoadAsync(string datasetId, CancellationToken ct);
}