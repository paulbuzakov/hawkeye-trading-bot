using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HTB.Strategy.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddStrategyRuleSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "strategy_rule_sets",
                schema: "strategy",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    rules = table.Column<string>(type: "jsonb", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_strategy_rule_sets", x => new { x.id, x.version });
                    table.ForeignKey(
                        name: "FK_strategy_rule_sets_strategy_definitions_id_version",
                        columns: x => new { x.id, x.version },
                        principalSchema: "strategy",
                        principalTable: "strategy_definitions",
                        principalColumns: new[] { "id", "version" },
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "strategy_rule_sets", schema: "strategy");
        }
    }
}
