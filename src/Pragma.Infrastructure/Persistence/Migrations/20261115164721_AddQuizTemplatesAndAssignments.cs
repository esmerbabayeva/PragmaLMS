using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pragma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuizTemplatesAndAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "QuizAssignmentId",
                table: "QuizAttempts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "QuizTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefaultTimeLimitMinutes = table.Column<int>(type: "int", nullable: true),
                    DefaultPassingScorePercentage = table.Column<int>(type: "int", nullable: false),
                    DefaultPointsPerQuestion = table.Column<int>(type: "int", nullable: false),
                    DefaultMaxAttempts = table.Column<int>(type: "int", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QuestionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuizTemplates_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuizAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CohortId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StudentIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LessonId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomTimeLimitMinutes = table.Column<int>(type: "int", nullable: true),
                    PassingScorePercentage = table.Column<int>(type: "int", nullable: false),
                    PointsPerQuestion = table.Column<int>(type: "int", nullable: true),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    StartDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    QuestionsSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QuizId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuizAssignments_Cohorts_CohortId",
                        column: x => x.CohortId,
                        principalTable: "Cohorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuizAssignments_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuizAssignments_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuizAssignments_QuizTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "QuizTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuizAssignments_Quizzes_QuizId",
                        column: x => x.QuizId,
                        principalTable: "Quizzes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuizAttempts_QuizAssignmentId",
                table: "QuizAttempts",
                column: "QuizAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizAssignments_CohortId",
                table: "QuizAssignments",
                column: "CohortId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizAssignments_CourseId",
                table: "QuizAssignments",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizAssignments_LessonId",
                table: "QuizAssignments",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizAssignments_QuizId",
                table: "QuizAssignments",
                column: "QuizId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizAssignments_TeacherId",
                table: "QuizAssignments",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizAssignments_TemplateId",
                table: "QuizAssignments",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizTemplates_CourseId",
                table: "QuizTemplates",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizTemplates_TeacherId",
                table: "QuizTemplates",
                column: "TeacherId");

            migrationBuilder.AddForeignKey(
                name: "FK_QuizAttempts_QuizAssignments_QuizAssignmentId",
                table: "QuizAttempts",
                column: "QuizAssignmentId",
                principalTable: "QuizAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuizAttempts_QuizAssignments_QuizAssignmentId",
                table: "QuizAttempts");

            migrationBuilder.DropTable(
                name: "QuizAssignments");

            migrationBuilder.DropTable(
                name: "QuizTemplates");

            migrationBuilder.DropIndex(
                name: "IX_QuizAttempts_QuizAssignmentId",
                table: "QuizAttempts");

            migrationBuilder.DropColumn(
                name: "QuizAssignmentId",
                table: "QuizAttempts");
        }
    }
}
