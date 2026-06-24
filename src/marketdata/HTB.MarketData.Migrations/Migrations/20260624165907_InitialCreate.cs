#nullable disable

namespace HTB.MarketData.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS timescaledb;");

            migrationBuilder.CreateTable(
                name: "exchanges",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exchanges", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "symbols",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    exchange_id = table.Column<int>(type: "integer", nullable: false),
                    base_asset = table.Column<string>(type: "text", nullable: false),
                    quote_asset = table.Column<string>(type: "text", nullable: false),
                    exchange_symbol = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_symbols", x => x.id);
                    table.ForeignKey(
                        name: "FK_symbols_exchanges_exchange_id",
                        column: x => x.exchange_id,
                        principalTable: "exchanges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "candles",
                columns: table => new
                {
                    exchange_id = table.Column<int>(type: "integer", nullable: false),
                    symbol_id = table.Column<int>(type: "integer", nullable: false),
                    interval = table.Column<short>(type: "smallint", nullable: false),
                    open_time = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    open = table.Column<decimal>(type: "numeric", nullable: false),
                    high = table.Column<decimal>(type: "numeric", nullable: false),
                    low = table.Column<decimal>(type: "numeric", nullable: false),
                    close = table.Column<decimal>(type: "numeric", nullable: false),
                    volume = table.Column<decimal>(type: "numeric", nullable: false),
                    quote_volume = table.Column<decimal>(type: "numeric", nullable: false),
                    trade_count = table.Column<int>(type: "integer", nullable: false),
                    is_closed = table.Column<bool>(type: "boolean", nullable: false),
                    ingested_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "now()"
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_candles",
                        x => new
                        {
                            x.exchange_id,
                            x.symbol_id,
                            x.interval,
                            x.open_time,
                        }
                    );
                    table.ForeignKey(
                        name: "FK_candles_symbols_symbol_id",
                        column: x => x.symbol_id,
                        principalTable: "symbols",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_candles_symbol_interval_time",
                table: "candles",
                columns: new[] { "symbol_id", "interval", "open_time" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_exchanges_code",
                table: "exchanges",
                column: "code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_symbols_exchange_id_exchange_symbol",
                table: "symbols",
                columns: new[] { "exchange_id", "exchange_symbol" },
                unique: true
            );

            // Convert the candles fact table into a TimescaleDB hypertable partitioned by
            // open_time. The primary key already includes open_time, satisfying Timescale's
            // requirement that unique constraints cover the partitioning column.
            migrationBuilder.Sql(
                "SELECT create_hypertable('candles', 'open_time', if_not_exists => TRUE);"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "candles");

            migrationBuilder.DropTable(name: "symbols");

            migrationBuilder.DropTable(name: "exchanges");
        }
    }
}
