using FluentAssertions;
using Report.Infrastructure.Artifacts;

namespace Report.QueryEngine.Tests.Artifacts;

public sealed class LocalReportArtifactStoreTests
{
    [Fact] public async Task SaveThenLoad_ReturnsSameBytes(){ var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")); var s=new LocalReportArtifactStore(dir); var key="reports/r/h/v/e/artifact-v1.seaf"; var bytes=new byte[]{1,2,3}; await s.SaveAsync(key,new MemoryStream(bytes),default); await using var stream=await s.LoadAsync(key,default); using var ms=new MemoryStream(); await stream.CopyToAsync(ms); ms.ToArray().Should().Equal(bytes); }
    [Fact] public async Task Exists_AfterSave(){ var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")); var s=new LocalReportArtifactStore(dir); var key="reports/r/h/v/e/artifact-v1.seaf"; await s.SaveAsync(key,new MemoryStream([1]),default); (await s.ExistsAsync(key,default)).Should().BeTrue(); }
    [Theory] [InlineData("../secret.seaf")] [InlineData("reports/../../secret.seaf")] [InlineData("C:\\Windows\\secret.seaf")] [InlineData("/etc/passwd")] public async Task RejectsTraversal(string k){ var s=new LocalReportArtifactStore(Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"))); await FluentActions.Invoking(()=>s.SaveAsync(k,new MemoryStream([1]),default)).Should().ThrowAsync<InvalidOperationException>(); }
}
