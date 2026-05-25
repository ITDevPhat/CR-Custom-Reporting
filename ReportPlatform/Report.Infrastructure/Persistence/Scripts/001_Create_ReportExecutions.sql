IF OBJECT_ID(N'dbo.ReportExecutions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReportExecutions
    (
        ExecutionId nvarchar(64) NOT NULL CONSTRAINT PK_ReportExecutions PRIMARY KEY,
        ReportId nvarchar(128) NOT NULL,
        ReportName nvarchar(256) NULL,
        TemplateId nvarchar(128) NULL,
        Status nvarchar(32) NOT NULL,
        RowCount int NULL,
        ArtifactKey nvarchar(1024) NULL,
        ArtifactAvailable bit NOT NULL CONSTRAINT DF_ReportExecutions_ArtifactAvailable DEFAULT 0,
        StorageMode nvarchar(32) NOT NULL,
        QueryFingerprint nvarchar(128) NULL,
        SemanticModelVersion nvarchar(64) NULL,
        CompiledSql nvarchar(max) NULL,
        CreatedAtUtc datetime2 NOT NULL,
        StartedAtUtc datetime2 NULL,
        CompletedAtUtc datetime2 NULL,
        FailedAtUtc datetime2 NULL,
        DurationMs bigint NULL,
        ErrorMessage nvarchar(max) NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ReportExecutions_CreatedAtUtc' AND object_id = OBJECT_ID(N'dbo.ReportExecutions'))
    CREATE INDEX IX_ReportExecutions_CreatedAtUtc ON dbo.ReportExecutions (CreatedAtUtc DESC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ReportExecutions_Status' AND object_id = OBJECT_ID(N'dbo.ReportExecutions'))
    CREATE INDEX IX_ReportExecutions_Status ON dbo.ReportExecutions (Status);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ReportExecutions_QueryFingerprint' AND object_id = OBJECT_ID(N'dbo.ReportExecutions'))
    CREATE INDEX IX_ReportExecutions_QueryFingerprint ON dbo.ReportExecutions (QueryFingerprint);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ReportExecutions_ReportId' AND object_id = OBJECT_ID(N'dbo.ReportExecutions'))
    CREATE INDEX IX_ReportExecutions_ReportId ON dbo.ReportExecutions (ReportId);
