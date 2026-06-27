using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HTB.MarketData.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "marketdata");

            migrationBuilder.CreateTable(
                name: "exchanges",
                schema: "marketdata",
                columns: table => new
                {
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exchanges", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "symbols",
                schema: "marketdata",
                columns: table => new
                {
                    code = table.Column<string>(type: "text", nullable: false),
                    exchange_code = table.Column<string>(type: "text", nullable: false),
                    base_asset = table.Column<string>(type: "text", nullable: false),
                    quote_asset = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_symbols", x => x.code);
                    table.ForeignKey(
                        name: "FK_symbols_exchanges_exchange_code",
                        column: x => x.exchange_code,
                        principalSchema: "marketdata",
                        principalTable: "exchanges",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "candles",
                schema: "marketdata",
                columns: table => new
                {
                    exchange_code = table.Column<string>(type: "text", nullable: false),
                    symbol_code = table.Column<string>(type: "text", nullable: false),
                    interval = table.Column<short>(type: "smallint", nullable: false),
                    open_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    open = table.Column<decimal>(type: "numeric", nullable: false),
                    high = table.Column<decimal>(type: "numeric", nullable: false),
                    low = table.Column<decimal>(type: "numeric", nullable: false),
                    close = table.Column<decimal>(type: "numeric", nullable: false),
                    volume = table.Column<decimal>(type: "numeric", nullable: false),
                    quote_volume = table.Column<decimal>(type: "numeric", nullable: false),
                    trade_count = table.Column<int>(type: "integer", nullable: false),
                    is_closed = table.Column<bool>(type: "boolean", nullable: false),
                    ingested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candles", x => new { x.exchange_code, x.symbol_code, x.interval, x.open_time });
                    table.ForeignKey(
                        name: "FK_candles_exchanges_exchange_code",
                        column: x => x.exchange_code,
                        principalSchema: "marketdata",
                        principalTable: "exchanges",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_candles_symbols_symbol_code",
                        column: x => x.symbol_code,
                        principalSchema: "marketdata",
                        principalTable: "symbols",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_candles_symbol_interval_time",
                schema: "marketdata",
                table: "candles",
                columns: new[] { "symbol_code", "interval", "open_time" });

            migrationBuilder.CreateIndex(
                name: "IX_exchanges_code",
                schema: "marketdata",
                table: "exchanges",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_symbols_exchange_code_code",
                schema: "marketdata",
                table: "symbols",
                columns: new[] { "exchange_code", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candles",
                schema: "marketdata");

            migrationBuilder.DropTable(
                name: "symbols",
                schema: "marketdata");

            migrationBuilder.DropTable(
                name: "exchanges",
                schema: "marketdata");
        }
    }
}
