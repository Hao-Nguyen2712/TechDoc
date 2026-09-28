using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechDocAI.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Init : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "documents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                NeedsOcr = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_documents", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "chunks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                PageIndex = table.Column<int>(type: "integer", nullable: true),
                StartLine = table.Column<int>(type: "integer", nullable: true),
                EndLine = table.Column<int>(type: "integer", nullable: true),
                HeadingPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Text = table.Column<string>(type: "text", nullable: false),
                NeedsOcr = table.Column<bool>(type: "boolean", nullable: false),
                EmbeddingModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                EmbeddingDimensions = table.Column<int>(type: "integer", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_chunks", x => x.Id);
                table.ForeignKey(
                    name: "FK_chunks_documents_DocumentId",
                    column: x => x.DocumentId,
                    principalTable: "documents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ingestion_jobs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "text", nullable: false),
                ErrorDetails = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ingestion_jobs", x => x.Id);
                table.ForeignKey(
                    name: "FK_ingestion_jobs_documents_DocumentId",
                    column: x => x.DocumentId,
                    principalTable: "documents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_chunks_DocumentId",
            table: "chunks",
            column: "DocumentId");

        migrationBuilder.CreateIndex(
            name: "IX_documents_ContentHash",
            table: "documents",
            column: "ContentHash");

        migrationBuilder.CreateIndex(
            name: "IX_ingestion_jobs_DocumentId",
            table: "ingestion_jobs",
            column: "DocumentId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "chunks");

        migrationBuilder.DropTable(
            name: "ingestion_jobs");

        migrationBuilder.DropTable(
            name: "documents");
    }
}
