using FluentAssertions;
using Report.Contracts.Requests;
using Report.QueryEngine.Artifacts;

namespace Report.QueryEngine.Tests.Artifacts;

public sealed class QueryFingerprintServiceTests
{
    [Fact] public void SameRequest_SameHash(){ var s=new QueryFingerprintService(); var r=new VisualQueryRequest{DatasetId="d",Rows=["a"],Values=["m"],Filters=[new FilterRequest{Field="f",Operator="=",Value=1}],Sort=[new SortRequest{Field="a",Direction="ASC"}],Limit=10,Offset=0}; s.Compute(r,"v1").Should().Be(s.Compute(r,"v1")); }
    [Fact] public void DifferentFilter_DifferentHash(){ var s=new QueryFingerprintService(); var a=new VisualQueryRequest{DatasetId="d",Filters=[new FilterRequest{Field="f",Operator="=",Value=1}]}; var b=new VisualQueryRequest{DatasetId="d",Filters=[new FilterRequest{Field="f",Operator="=",Value=2}]}; s.Compute(a,"v1").Should().NotBe(s.Compute(b,"v1")); }
}
