namespace Report.Api.Rendering;

public interface IReportConnectionStringResolver
{
    string Resolve(string connectionId);
}
