using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pragma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteFilterToUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Courses_Slug",
                table: "Courses");

            migrationBuilder.DropIndex(
                name: "IX_CohortGradeEntries_CohortId_StudentId_ComponentType",
                table: "CohortGradeEntries");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Slug",
                table: "Categories");

            migrationBuilder.CreateIndex(
                name: "IX_Courses_Slug",
                table: "Courses",
                column: "Slug",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CohortGradeEntries_CohortId_StudentId_ComponentType",
                table: "CohortGradeEntries",
                columns: new[] { "CohortId", "StudentId", "ComponentType" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                table: "Categories",
                column: "Slug",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Courses_Slug",
                table: "Courses");

            migrationBuilder.DropIndex(
                name: "IX_CohortGradeEntries_CohortId_StudentId_ComponentType",
                table: "CohortGradeEntries");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Slug",
                table: "Categories");

            migrationBuilder.CreateIndex(
                name: "IX_Courses_Slug",
                table: "Courses",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CohortGradeEntries_CohortId_StudentId_ComponentType",
                table: "CohortGradeEntries",
                columns: new[] { "CohortId", "StudentId", "ComponentType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                table: "Categories",
                column: "Slug",
                unique: true);
        }
    }
}
