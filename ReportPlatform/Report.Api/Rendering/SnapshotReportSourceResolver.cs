using Report.Contracts.Artifacts;
using Report.QueryEngine.Artifacts;
using Telerik.Reporting;
using Telerik.Reporting.Drawing;
using Telerik.Reporting.Services;

namespace Report.Api.Rendering;

public sealed class SnapshotReportSourceResolver : IReportSourceResolver
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SnapshotReportSourceResolver> _logger;
    private readonly IConfiguration _configuration;

    public SnapshotReportSourceResolver(
        IServiceScopeFactory scopeFactory,
        ILogger<SnapshotReportSourceResolver> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
    }

    public Telerik.Reporting.ReportSource Resolve(
        string report,
        OperationOrigin operationOrigin,
        IDictionary<string, object> currentParameterValues)
    {
        _logger.LogInformation(
            "Snapshot resolver invoked. ReportSource={ReportSource} OperationOrigin={OperationOrigin}",
            report,
            operationOrigin);

        var smokeMode = IsSmokeMode(report);
        _logger.LogInformation("Snapshot resolver smoke mode evaluated. SmokeMode={SmokeMode}", smokeMode);

        if (smokeMode)
        {
            _logger.LogInformation("Snapshot resolver returning smoke-test report.");
            return CreateSmokeReport();
        }

        var executionId = ParseExecutionId(report);
        _logger.LogInformation(
            "Snapshot resolver parsed execution id. ExecutionId={ExecutionId} OperationOrigin={OperationOrigin}",
            executionId,
            operationOrigin);

        using var scope = _scopeFactory.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<IReportExecutionRepository>();
        var artifactStore = scope.ServiceProvider.GetRequiredService<IReportArtifactStore>();
        var artifactLoader = scope.ServiceProvider.GetRequiredService<ReportArtifactLoader>();
        var factory = scope.ServiceProvider.GetRequiredService<ITelerikSnapshotReportFactory>();

        var execution = repository
            .GetAsync(executionId, CancellationToken.None)
            .GetAwaiter()
            .GetResult()
            ?? throw new ReportExportException($"Execution '{executionId}' not found.", 404);

        if (!string.Equals(execution.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            throw new ReportExportException($"Execution '{executionId}' is not completed.", 400);
        }

        if (string.IsNullOrWhiteSpace(execution.ArtifactKey))
        {
            throw new ReportExportException($"Execution '{executionId}' does not have an artifact key.", 400);
        }

        if (!artifactStore.ExistsAsync(execution.ArtifactKey, CancellationToken.None).GetAwaiter().GetResult())
        {
            repository.MarkArtifactMissingAsync(executionId, CancellationToken.None).GetAwaiter().GetResult();
            throw new ReportExportException($"Artifact for execution '{executionId}' is missing.", 404);
        }

        try
        {
            using var stream = artifactStore
                .LoadAsync(execution.ArtifactKey, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            var loaded = artifactLoader
                .LoadAsync(stream, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            var reportDocument = factory.CreateSnapshotBackedTableReport(
                loaded.DataTable,
                execution.ReportName,
                execution,
                loaded.Header);

            _logger.LogInformation(
                "Snapshot resolver returning snapshot report. ExecutionId={ExecutionId} Source=Artifact",
                executionId);

            return new Telerik.Reporting.InstanceReportSource // != trdp
            {
                ReportDocument = reportDocument
            };
        }
        catch (ReportArtifactException ex) when (
            string.Equals(ex.Code, "ARTIFACT_VERSION_UNSUPPORTED", StringComparison.OrdinalIgnoreCase))
        {
            
            throw new ReportExportException("Artifact version is incompatible.", 400, ex);
        }
        catch (ReportArtifactException ex)
        {
            
            throw new ReportExportException("Artifact is corrupted and cannot be previewed.", 400, ex);
        }
        catch (InvalidDataException ex)
        {
            
            throw new ReportExportException("Artifact is corrupted and cannot be previewed.", 400, ex);
        }
    }

    private static string ParseExecutionId(string report)
    {
        if (string.IsNullOrWhiteSpace(report))
        {
            throw new ReportExportException("Missing report source.", 400);
        }

        var value = report.Trim();

        if (value.StartsWith("execution:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["execution:".Length..];
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ReportExportException("Missing execution id.", 400);
        }

        return value;
    }

    private bool IsSmokeMode(string report)
    {
        if (!string.IsNullOrWhiteSpace(report) &&
            report.Trim().StartsWith("smoke:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return _configuration.GetValue<bool>("TelerikPreview:SmokeTest");
    }

    private static Telerik.Reporting.ReportSource CreateSmokeReport()
    {
        var report = new Telerik.Reporting.Report
        {
            Width = Unit.Cm(18),
            PageSettings =
            {
                Margins =
                {
                    Left = Unit.Cm(0.5),
                    Right = Unit.Cm(0.5),
                    Top = Unit.Cm(0.5),
                    Bottom = Unit.Cm(0.5)
                }
            }
        };

        var detail = new DetailSection
        {
            Height = Unit.Cm(3)
        };

        detail.Items.Add(new TextBox
        {
            Value = "HELLO TELERIK PREVIEW",
            Location = new PointU(Unit.Cm(1), Unit.Cm(1)),
            Size = new SizeU(Unit.Cm(12), Unit.Cm(1)),
            Style =
            {
                BackgroundColor = System.Drawing.Color.Yellow,
                Color = System.Drawing.Color.Red,
                Font =
                {
                    Bold = true,
                    Size = Unit.Point(18)
                }
            }
        });

        report.Items.Add(detail);

        return new Telerik.Reporting.InstanceReportSource
        {
            ReportDocument = report
        };
    }
}

//using Report.Contracts.Artifacts;
//using Report.QueryEngine.Artifacts;
//using Telerik.Reporting.Services;

//namespace Report.Api.Rendering;

//public sealed class SnapshotReportSourceResolver : IReportSourceResolver
//{
//    private readonly IReportExecutionRepository _repository;
//    private readonly IReportArtifactStore _artifactStore;
//    private readonly ReportArtifactLoader _artifactLoader;
//    private readonly ITelerikSnapshotReportFactory _factory;

//    public SnapshotReportSourceResolver(
//        IReportExecutionRepository repository,
//        IReportArtifactStore artifactStore,
//        ReportArtifactLoader artifactLoader,
//        ITelerikSnapshotReportFactory factory)
//    {
//        _repository = repository;
//        _artifactStore = artifactStore;
//        _artifactLoader = artifactLoader;
//        _factory = factory;
//    }

//    public Telerik.Reporting.ReportSource Resolve(
//        string report,
//        OperationOrigin operationOrigin,
//        IDictionary<string, object> currentParameterValues)
//    {
//        var executionId = ParseExecutionId(report);
//        var execution = _repository.GetAsync(executionId, CancellationToken.None).GetAwaiter().GetResult()
//            ?? throw new ReportExportException($"Execution '{executionId}' not found.", 404);

//        if (!string.Equals(execution.Status, "Completed", StringComparison.OrdinalIgnoreCase))
//            throw new ReportExportException($"Execution '{executionId}' is not completed.", 400);
//        if (string.IsNullOrWhiteSpace(execution.ArtifactKey))
//            throw new ReportExportException($"Execution '{executionId}' does not have an artifact key.", 400);

//        if (!_artifactStore.ExistsAsync(execution.ArtifactKey, CancellationToken.None).GetAwaiter().GetResult())
//        {
//            _repository.MarkArtifactMissingAsync(executionId, CancellationToken.None).GetAwaiter().GetResult();
//            throw new ReportExportException($"Artifact for execution '{executionId}' is missing.", 404);
//        }

//        try
//        {
//            using var stream = _artifactStore.LoadAsync(execution.ArtifactKey, CancellationToken.None).GetAwaiter().GetResult();
//            var loaded = _artifactLoader.LoadAsync(stream, CancellationToken.None).GetAwaiter().GetResult();
//            var reportDocument = _factory.CreateSnapshotBackedTableReport(
//                loaded.DataTable,
//                execution.ReportName,
//                execution,
//                loaded.Header);
//            return new Telerik.Reporting.InstanceReportSource { ReportDocument = reportDocument };
//        }
//        catch (ReportArtifactException ex) when (string.Equals(ex.Code, "ARTIFACT_VERSION_UNSUPPORTED", StringComparison.OrdinalIgnoreCase))
//        {
//            _repository.MarkFailedAsync(executionId, "ArtifactVersionMismatch", CancellationToken.None).GetAwaiter().GetResult();
//            throw new ReportExportException("Artifact version is incompatible.", 400, ex);
//        }
//        catch (ReportArtifactException ex)
//        {
//            _repository.MarkFailedAsync(executionId, "ArtifactCorrupted", CancellationToken.None).GetAwaiter().GetResult();
//            throw new ReportExportException("Artifact is corrupted and cannot be previewed.", 400, ex);
//        }
//        catch (InvalidDataException ex)
//        {
//            _repository.MarkFailedAsync(executionId, "ArtifactCorrupted", CancellationToken.None).GetAwaiter().GetResult();
//            throw new ReportExportException("Artifact is corrupted and cannot be previewed.", 400, ex);
//        }
//    }

//    private static string ParseExecutionId(string report)
//    {
//        if (string.IsNullOrWhiteSpace(report)) throw new ReportExportException("Missing report source.", 400);
//        var value = report.Trim();
//        if (value.StartsWith("execution:", StringComparison.OrdinalIgnoreCase))
//        {
//            value = value["execution:".Length..];
//        }
//        if (string.IsNullOrWhiteSpace(value)) throw new ReportExportException("Missing execution id.", 400);
//        return value;
//    }
//}
