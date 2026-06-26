using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HTB.Strategy.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "strategy");

            migrationBuilder.CreateTable(
                name: "strategy_versions",
                schema: "strategy",
                columns: table => new
                {
                    strategy_id = table.Column<string>(type: "text", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    schema_version = table.Column<string>(type: "text", nullable: false),
                    rules_hash = table.Column<string>(type: "text", nullable: false),
                    timeframe = table.Column<short>(type: "smallint", nullable: false),
                    warmup_bars = table.Column<int>(type: "integer", nullable: false),
                    meta_json = table.Column<string>(type: "text", nullable: false),
                    rules_json = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    registered_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "now()"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_strategy_versions",
                        x => new { x.strategy_id, x.version_number }
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_strategy_versions_status",
                schema: "strategy",
                table: "strategy_versions",
                column: "status"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "strategy_versions", schema: "strategy");
        }
    }
}
