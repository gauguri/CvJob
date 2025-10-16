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
                CREATE TABLE IF NOT EXISTS "Resumes" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_Resumes" PRIMARY KEY,
                    "FileName" TEXT NOT NULL,
                    "Text" TEXT NOT NULL,
                    "CreatedUtc" TEXT NOT NULL
                );
                """
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"Resumes\";");
        }
    }
}
