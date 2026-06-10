using Report.Metadata.Connections;
using Report.Metadata.Models;

namespace Report.Metadata.Stores;

public sealed class RegisteredDataset
{
    public string DatasetId { get; init; } = "";
    public string DatasetName { get; init; } = "";
    public string ConnectionId { get; init; } = "";
    public SemanticModel Model { get; init; } = new();
}

public interface IDatasetRegistry
{
    RegisteredDataset Save(string datasetName, ConnectionDefinition connection, SemanticModel model);
    RegisteredDataset SaveExisting(string datasetId, string datasetName, string connectionId, SemanticModel model);
    RegisteredDataset? Find(string datasetId);
}
