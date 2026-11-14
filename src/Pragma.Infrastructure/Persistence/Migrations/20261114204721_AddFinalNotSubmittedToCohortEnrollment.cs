using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pragma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinalNotSubmittedToCohortEnrollment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FinalNotSubmitted",
                table: "CohortEnrollments",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalNotSubmitted",
                table: "CohortEnrollments");
        }
    }
}
