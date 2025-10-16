using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace F500.JobMatch.Api.Data.Migrations
{
    public partial class EnsureResumesTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[Resumes]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[Resumes]
                    (
                        [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_Resumes] PRIMARY KEY,
                        [FileName] nvarchar(256) NOT NULL,
                        [Text] nvarchar(max) NOT NULL,
                        [CreatedUtc] datetime2 NOT NULL
                    );
                END
                """
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[Resumes]', N'U') IS NOT NULL
                BEGIN
                    DROP TABLE [dbo].[Resumes];
                END
                """
            );
        }
    }
}
