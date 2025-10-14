using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace F500.JobMatch.Api.Data.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Resumes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resumes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobPostings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StableIdHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Company = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    DescriptionHtml = table.Column<string>(type: "TEXT", nullable: true),
                    DescriptionText = table.Column<string>(type: "TEXT", nullable: false),
                    EmploymentType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    PostedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Url = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    FetchedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobPostings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobPostings_StableIdHash",
                table: "JobPostings",
                column: "StableIdHash",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobPostings");

            migrationBuilder.DropTable(
                name: "Resumes");
        }
    }
}
