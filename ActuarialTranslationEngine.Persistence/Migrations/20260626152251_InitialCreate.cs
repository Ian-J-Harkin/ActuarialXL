using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActuarialTranslationEngine.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TranslationJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", nullable: false),
                    FileHash = table.Column<string>(type: "TEXT", nullable: false),
                    ModelUsed = table.Column<string>(type: "TEXT", nullable: false),
                    WorkbookSessionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TargetSheet = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranslationJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TranslationPartitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    JobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartitionIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceName = table.Column<string>(type: "TEXT", nullable: false),
                    FinalAuditableMarkdown = table.Column<string>(type: "TEXT", nullable: false),
                    GeneratedCSharpMirrorCode = table.Column<string>(type: "TEXT", nullable: false),
                    IsCertified = table.Column<bool>(type: "INTEGER", nullable: false),
                    VarianceDelta = table.Column<decimal>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    DisruptiveNodesJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranslationPartitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TranslationPartitions_TranslationJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "TranslationJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TranslationPartitions_JobId",
                table: "TranslationPartitions",
                column: "JobId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TranslationPartitions");

            migrationBuilder.DropTable(
                name: "TranslationJobs");
        }
    }
}
