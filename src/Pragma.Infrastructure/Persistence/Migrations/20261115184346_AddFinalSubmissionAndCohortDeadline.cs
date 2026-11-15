using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pragma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinalSubmissionAndCohortDeadline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FinalDeadlineUtc",
                table: "Cohorts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FinalSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CohortId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinalSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinalSubmissions_Cohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "Cohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinalSubmissions_CohortId_StudentId",
                table: "FinalSubmissions",
                columns: new[] { "CohortId", "StudentId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinalSubmissions");

            migrationBuilder.DropColumn(
                name: "FinalDeadlineUtc",
                table: "Cohorts");
        }
    }
}
