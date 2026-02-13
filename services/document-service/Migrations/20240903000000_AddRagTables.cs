using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PLS.BuildingBlocks.Data;

#nullable disable

namespace DocumentService.Migrations;

[DbContext(typeof(PlsDbContext))]
[Migration("20240903000000_AddRagTables")]
public partial class AddRagTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DocumentChunks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                ChunkId = table.Column<string>(type: "text", nullable: false),
                Page = table.Column<int>(type: "integer", nullable: true),
                Section = table.Column<string>(type: "text", nullable: true),
                Hash = table.Column<string>(type: "text", nullable: false),
                Text = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DocumentChunks", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "RetrievalRuns",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                Lane = table.Column<int>(type: "integer", nullable: false),
                Query = table.Column<string>(type: "text", nullable: false),
                HitsJson = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RetrievalRuns", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DocumentChunks_TenantId_DocumentId",
            table: "DocumentChunks",
            columns: new[] { "TenantId", "DocumentId" });

        migrationBuilder.CreateIndex(
            name: "IX_RetrievalRuns_TenantId_DocumentId",
            table: "RetrievalRuns",
            columns: new[] { "TenantId", "DocumentId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "DocumentChunks");
        migrationBuilder.DropTable(name: "RetrievalRuns");
    }
}
