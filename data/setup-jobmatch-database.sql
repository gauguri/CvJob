/*
    Setup script for the JobMatch SQL Server database.
    - Creates the JobMatch database if it does not already exist.
    - Ensures the required tables and indexes exist.
    Execute on SQL Server (e.g., SQL Server Express) before running the application.
*/

IF DB_ID(N'JobMatch') IS NULL
BEGIN
    PRINT N'Creating database [JobMatch]...';
    CREATE DATABASE [JobMatch];
END
ELSE
BEGIN
    PRINT N'Database [JobMatch] already exists.';
END
GO

USE [JobMatch];
GO

IF OBJECT_ID(N'[dbo].[Resumes]', N'U') IS NULL
BEGIN
    PRINT N'Creating table [dbo].[Resumes]...';
    CREATE TABLE [dbo].[Resumes]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Resumes] PRIMARY KEY,
        [FileName] NVARCHAR(256) NOT NULL,
        [Text] NVARCHAR(MAX) NOT NULL,
        [CreatedUtc] DATETIME2 NOT NULL
    );
END
ELSE
BEGIN
    PRINT N'Table [dbo].[Resumes] already exists.';
END
GO

IF OBJECT_ID(N'[dbo].[JobPostings]', N'U') IS NULL
BEGIN
    PRINT N'Creating table [dbo].[JobPostings]...';
    CREATE TABLE [dbo].[JobPostings]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_JobPostings] PRIMARY KEY,
        [StableIdHash] NVARCHAR(64) NOT NULL,
        [Title] NVARCHAR(256) NOT NULL,
        [Company] NVARCHAR(256) NOT NULL,
        [Location] NVARCHAR(256) NULL,
        [DescriptionHtml] NVARCHAR(MAX) NULL,
        [DescriptionText] NVARCHAR(MAX) NOT NULL,
        [EmploymentType] NVARCHAR(64) NULL,
        [PostedAtUtc] DATETIME2 NULL,
        [Url] NVARCHAR(512) NOT NULL,
        [Source] NVARCHAR(128) NULL,
        [FetchedAtUtc] DATETIME2 NOT NULL
    );
END
ELSE
BEGIN
    PRINT N'Table [dbo].[JobPostings] already exists.';
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes i
    WHERE i.name = N'IX_JobPostings_StableIdHash'
      AND i.object_id = OBJECT_ID(N'[dbo].[JobPostings]', N'U')
)
BEGIN
    PRINT N'Creating unique index [IX_JobPostings_StableIdHash] on [dbo].[JobPostings]...';
    CREATE UNIQUE INDEX [IX_JobPostings_StableIdHash]
        ON [dbo].[JobPostings] ([StableIdHash]);
END
ELSE
BEGIN
    PRINT N'Index [IX_JobPostings_StableIdHash] already exists.';
END
GO
