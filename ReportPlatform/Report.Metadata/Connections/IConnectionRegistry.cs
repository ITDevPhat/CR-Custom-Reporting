namespace Report.Metadata.Connections;

public interface IConnectionRegistry
{
    ConnectionDefinition Save(ConnectionDefinition definition);
    ConnectionDefinition? Find(string connectionId);
}
