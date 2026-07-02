using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BigSchool.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateCompanies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    IdCompany = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Ticker = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Sector = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Market = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Currency = table.Column<string>(type: "char(3)", nullable: false, defaultValueSql: "'EUR'")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IdStatus = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)2),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.IdCompany);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Valuations",
                columns: table => new
                {
                    IdValuation = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Price = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PriceCurrency = table.Column<string>(type: "char(3)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Source = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IdStatus = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)2),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IdCompany = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Valuations", x => x.IdValuation);
                    table.ForeignKey(
                        name: "FK_Valuations_Companies_IdCompany",
                        column: x => x.IdCompany,
                        principalTable: "Companies",
                        principalColumn: "IdCompany",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "Companies",
                columns: new[] { "IdCompany", "CreatedAt", "Currency", "IdStatus", "Market", "Name", "Sector", "Ticker", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "USD", (short)2, "NASDAQ", "Apple Inc.", "Technology", "AAPL", null },
                    { 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "USD", (short)2, "NASDAQ", "Microsoft Corp.", "Technology", "MSFT", null },
                    { 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "EUR", (short)2, "BME", "Banco Santander", "Financials", "SAN", null },
                    { 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "GBP", (short)2, "LSE", "Shell plc", "Energy", "SHEL", null }
                });

            migrationBuilder.InsertData(
                table: "Valuations",
                columns: new[] { "IdValuation", "CreatedAt", "Date", "IdCompany", "IdStatus", "Source", "UpdatedAt", "Price", "PriceCurrency" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 2), 1, (short)2, "seed", null, 195.0000m, "USD" },
                    { 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 3, 2), 1, (short)2, "seed", null, 210.0000m, "USD" },
                    { 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 2), 2, (short)2, "seed", null, 420.0000m, "USD" },
                    { 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 3, 2), 2, (short)2, "seed", null, 440.0000m, "USD" },
                    { 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 2), 3, (short)2, "seed", null, 4.5000m, "EUR" },
                    { 6, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 3, 2), 3, (short)2, "seed", null, 4.8000m, "EUR" },
                    { 7, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 1, 2), 4, (short)2, "seed", null, 28.0000m, "GBP" },
                    { 8, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 3, 2), 4, (short)2, "seed", null, 30.0000m, "GBP" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Ticker",
                table: "Companies",
                column: "Ticker",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Valuations_IdCompany_Date",
                table: "Valuations",
                columns: new[] { "IdCompany", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Valuations");

            migrationBuilder.DropTable(
                name: "Companies");
        }
    }
}
