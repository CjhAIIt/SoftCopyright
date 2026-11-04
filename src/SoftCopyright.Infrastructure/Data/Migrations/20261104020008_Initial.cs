using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoftCopyright.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ApplicationCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    SoftwareFullName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    SoftwareVersion = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Stage = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    StageUpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    StatusMessage = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    FailureReason = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    UploadedFileName = table.Column<string>(type: "varchar(260)", maxLength: 260, nullable: true),
                    SourceRepositoryUrl = table.Column<string>(type: "varchar(600)", maxLength: 600, nullable: true),
                    SourceCommit = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    MaterialKind = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    DataDirectory = table.Column<string>(type: "varchar(600)", maxLength: 600, nullable: false),
                    ScanSummaryJson = table.Column<string>(type: "longtext", nullable: true),
                    ProfileJson = table.Column<string>(type: "longtext", nullable: true),
                    RulesVersion = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    AnalyzerVersion = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationCases", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CaseDeletionAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    CaseId = table.Column<Guid>(type: "char(36)", nullable: false),
                    CaseName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FileCount = table.Column<int>(type: "int", nullable: false),
                    ArtifactCount = table.Column<int>(type: "int", nullable: false),
                    ConfirmationCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseDeletionAudits", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ApplicantInfos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    CaseId = table.Column<Guid>(type: "char(36)", nullable: false),
                    SoftwareFullName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    SoftwareAbbreviation = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    VersionNumber = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    CopyrightOwner = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    DevelopmentMode = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    RightsAcquisitionMethod = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    CompletionDate = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    FirstPublicationStatus = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    FirstPublicationDate = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    DevelopmentEnvironment = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    RuntimeEnvironment = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    ProgrammingLanguages = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    SourceProgramAmount = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    FunctionSummary = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false),
                    FieldSourcesJson = table.Column<string>(type: "longtext", nullable: false),
                    RulesVersion = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    RowVersion = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicantInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicantInfos_ApplicationCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "ApplicationCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "BackgroundJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    CaseId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    ProgressPercent = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    ErrorMessage = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    PayloadJson = table.Column<string>(type: "longtext", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LeaseUntil = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    NextRunAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackgroundJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BackgroundJobs_ApplicationCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "ApplicationCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CodeSelections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    CaseId = table.Column<Guid>(type: "char(36)", nullable: false),
                    IsConfirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ConfirmedHash = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeSelections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CodeSelections_ApplicationCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "ApplicationCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ConfirmationRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    CaseId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Stage = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    ConfirmedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DataHash = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    Note = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    InvalidatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    InvalidatedReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfirmationRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConfirmationRecords_ApplicationCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "ApplicationCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Evidences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    CaseId = table.Column<Guid>(type: "char(36)", nullable: false),
                    RelativePath = table.Column<string>(type: "varchar(600)", maxLength: 600, nullable: false),
                    StartLine = table.Column<int>(type: "int", nullable: true),
                    EndLine = table.Column<int>(type: "int", nullable: true),
                    EvidenceType = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                    Sha256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Excerpt = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false),
                    Source = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Evidences_ApplicationCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "ApplicationCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GeneratedArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    CaseId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Kind = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    FileName = table.Column<string>(type: "varchar(260)", maxLength: 260, nullable: false),
                    RelativePath = table.Column<string>(type: "varchar(800)", maxLength: 800, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    ContentType = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsStale = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneratedArtifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeneratedArtifacts_ApplicationCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "ApplicationCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "InventionDisclosures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    CaseId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Title = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    TechnicalField = table.Column<string>(type: "text", nullable: false),
                    BackgroundTechnology = table.Column<string>(type: "text", nullable: false),
                    ProblemStatement = table.Column<string>(type: "text", nullable: false),
                    TechnicalSolution = table.Column<string>(type: "text", nullable: false),
                    BeneficialEffects = table.Column<string>(type: "text", nullable: false),
                    DrawingDescription = table.Column<string>(type: "text", nullable: false),
                    PriorArt = table.Column<string>(type: "text", nullable: false),
                    PriorArtSources = table.Column<string>(type: "text", nullable: false),
                    Abstract = table.Column<string>(type: "text", nullable: false),
                    TerminologyJson = table.Column<string>(type: "text", nullable: false),
                    TechnicalFeaturesJson = table.Column<string>(type: "text", nullable: false),
                    EmbodimentsJson = table.Column<string>(type: "text", nullable: false),
                    ApplicantName = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    InventorNames = table.Column<string>(type: "varchar(600)", maxLength: 600, nullable: false),
                    PatentType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    RulesVersion = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventionDisclosures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventionDisclosures_ApplicationCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "ApplicationCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PatentClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    CaseId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    DependsOn = table.Column<int>(type: "int", nullable: true),
                    Text = table.Column<string>(type: "text", nullable: false),
                    FeaturesJson = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatentClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatentClaims_ApplicationCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "ApplicationCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ProjectAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    CaseId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProfileJson = table.Column<string>(type: "longtext", nullable: false),
                    CoverageJson = table.Column<string>(type: "longtext", nullable: false),
                    OpenQuestionsJson = table.Column<string>(type: "longtext", nullable: false),
                    OperationSectionsJson = table.Column<string>(type: "longtext", nullable: false),
                    AdapterHitsJson = table.Column<string>(type: "longtext", nullable: false),
                    AnalyzerVersion = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    RulesVersion = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectAnalyses_ApplicationCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "ApplicationCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ProjectFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    CaseId = table.Column<Guid>(type: "char(36)", nullable: false),
                    RelativePath = table.Column<string>(type: "varchar(600)", maxLength: 600, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    IsText = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DetectedEncoding = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    DetectedLanguage = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    Category = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    IsExcluded = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ExclusionReason = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    LineCount = table.Column<int>(type: "int", nullable: true),
                    SecretRisk = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    IsCodeCandidate = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectFiles_ApplicationCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "ApplicationCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CodeSelectionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    CaseId = table.Column<Guid>(type: "char(36)", nullable: false),
                    SelectionId = table.Column<Guid>(type: "char(36)", nullable: false),
                    RelativePath = table.Column<string>(type: "varchar(600)", maxLength: 600, nullable: false),
                    LineCount = table.Column<int>(type: "int", nullable: false),
                    Sha256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Included = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeSelectionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CodeSelectionItems_CodeSelections_SelectionId",
                        column: x => x.SelectionId,
                        principalTable: "CodeSelections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicantInfos_CaseId",
                table: "ApplicantInfos",
                column: "CaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationCases_IsDeleted",
                table: "ApplicationCases",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_CaseId",
                table: "BackgroundJobs",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_Status_NextRunAt",
                table: "BackgroundJobs",
                columns: new[] { "Status", "NextRunAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CodeSelectionItems_CaseId_RelativePath",
                table: "CodeSelectionItems",
                columns: new[] { "CaseId", "RelativePath" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodeSelectionItems_SelectionId",
                table: "CodeSelectionItems",
                column: "SelectionId");

            migrationBuilder.CreateIndex(
                name: "IX_CodeSelections_CaseId",
                table: "CodeSelections",
                column: "CaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmationRecords_CaseId",
                table: "ConfirmationRecords",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Evidences_CaseId",
                table: "Evidences",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedArtifacts_CaseId",
                table: "GeneratedArtifacts",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventionDisclosures_CaseId",
                table: "InventionDisclosures",
                column: "CaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatentClaims_CaseId_Number",
                table: "PatentClaims",
                columns: new[] { "CaseId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAnalyses_CaseId",
                table: "ProjectAnalyses",
                column: "CaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFiles_CaseId",
                table: "ProjectFiles",
                column: "CaseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicantInfos");

            migrationBuilder.DropTable(
                name: "BackgroundJobs");

            migrationBuilder.DropTable(
                name: "CaseDeletionAudits");

            migrationBuilder.DropTable(
                name: "CodeSelectionItems");

            migrationBuilder.DropTable(
                name: "ConfirmationRecords");

            migrationBuilder.DropTable(
                name: "Evidences");

            migrationBuilder.DropTable(
                name: "GeneratedArtifacts");

            migrationBuilder.DropTable(
                name: "InventionDisclosures");

            migrationBuilder.DropTable(
                name: "PatentClaims");

            migrationBuilder.DropTable(
                name: "ProjectAnalyses");

            migrationBuilder.DropTable(
                name: "ProjectFiles");

            migrationBuilder.DropTable(
                name: "CodeSelections");

            migrationBuilder.DropTable(
                name: "ApplicationCases");
        }
    }
}
