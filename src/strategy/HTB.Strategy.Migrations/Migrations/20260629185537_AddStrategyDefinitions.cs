using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HTB.Strategy.Migrations.Migrations;

/// <inheritdoc />
public partial class AddStrategyDefinitions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "strategy");

        migrationBuilder.CreateTable(
            name: "strategy_definitions",
            schema: "strategy",
            columns: table => new
            {
                id = table.Column<string>(type: "text", nullable: false),
                version = table.Column<int>(type: "integer", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                description = table.Column<string>(type: "text", nullable: false),
                tags = table.Column<string[]>(type: "text[]", nullable: false),
                exchanges = table.Column<string[]>(type: "text[]", nullable: false),
                symbols = table.Column<string[]>(type: "text[]", nullable: false),
                timeframes = table.Column<short[]>(type: "smallint[]", nullable: false),
                warmup_bars = table.Column<int>(type: "integer", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_strategy_definitions", x => new { x.id, x.version });
            }
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "strategy_definitions", schema: "strategy");
    }
}
