using System.Data;
using FluentAssertions;
using Report.Contracts.Artifacts;
using Report.QueryEngine.Artifacts;

namespace Report.QueryEngine.Tests.Artifacts;

public sealed class ReportArtifactRoundtripTests
{
    [Fact]
    public async Task BuildAndLoad_Roundtrip_PreservesValues()
    {
        var t = new DataTable();
        t.Columns.Add(new DataColumn("name", typeof(string)) { AllowDBNull = true });
        t.Columns.Add(new DataColumn("age", typeof(int)) { AllowDBNull = true });
        t.Columns.Add(new DataColumn("amount", typeof(decimal)) { AllowDBNull = true });
        t.Columns.Add(new DataColumn("created", typeof(DateTime)) { AllowDBNull = true });
        t.Columns.Add(new DataColumn("active", typeof(bool)) { AllowDBNull = true });
        t.Rows.Add("a", 1, 12.34m, new DateTime(2025,1,2,3,4,5,DateTimeKind.Utc), true);
        t.Rows.Add(DBNull.Value, DBNull.Value, 9999999999.1234m, DBNull.Value, DBNull.Value);
        var b = new ReportArtifactBuilder();
        var header = new ReportExecutionArtifactHeader{ExecutionId="e",ReportId="r",TemplateId="t",QueryFingerprint="f",SemanticModelVersion="v",ExecutedAtUtc=DateTime.UtcNow};
        var built = b.Build("reports/r/f/v/e/artifact-v1.seaf", header, t);
        var loaded = await new ReportArtifactLoader().LoadAsync(built.ArtifactStream, default);
        loaded.DataTable.Columns.Count.Should().Be(5);
        loaded.DataTable.Rows.Count.Should().Be(2);
        ((decimal)loaded.DataTable.Rows[1][2]).Should().Be(9999999999.1234m);
        ((DateTime)loaded.DataTable.Rows[0][3]).Should().Be(new DateTime(2025,1,2,3,4,5,DateTimeKind.Utc));
        loaded.DataTable.Rows[1][0].Should().Be(DBNull.Value);
    }
}
