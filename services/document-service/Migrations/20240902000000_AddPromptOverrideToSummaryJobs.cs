using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PLS.BuildingBlocks.Data;

#nullable disable

namespace DocumentService.Migrations;

[DbContext(typeof(PlsDbContext))]
[Migration("20240902000000_AddPromptOverrideToSummaryJobs")]
public partial class AddPromptOverrideToSummaryJobs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PromptOverride",
            table: "SummaryJobs",
            type: "text",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PromptOverride",
            table: "SummaryJobs");
    }
}
