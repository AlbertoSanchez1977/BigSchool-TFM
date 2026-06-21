using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BigSchool.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreatePortfolios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Portfolios",
                columns: table => new
                {
                    IdPortfolio = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IdUser = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RealizedPnL = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RealizedPnLCurrency = table.Column<string>(type: "char(3)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IdStatus = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)2),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Portfolios", x => x.IdPortfolio);
                    table.ForeignKey(
                        name: "FK_Portfolios_Users_IdUser",
                        column: x => x.IdUser,
                        principalTable: "Users",
                        principalColumn: "IdUser",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Holdings",
                columns: table => new
                {
                    IdHolding = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IdCompany = table.Column<int>(type: "int", nullable: false),
                    Shares = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BuyOriginalAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BuyOriginalCurrency = table.Column<string>(type: "char(3)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BuyExchangeRate = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    BuyBaseAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BuyBaseCurrency = table.Column<string>(type: "char(3)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BuyRateDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BuyDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IdStatus = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)2),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IdPortfolio = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holdings", x => x.IdHolding);
                    table.ForeignKey(
                        name: "FK_Holdings_Companies_IdCompany",
                        column: x => x.IdCompany,
                        principalTable: "Companies",
                        principalColumn: "IdCompany",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Holdings_Portfolios_IdPortfolio",
                        column: x => x.IdPortfolio,
                        principalTable: "Portfolios",
                        principalColumn: "IdPortfolio",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Disposals",
                columns: table => new
                {
                    IdDisposal = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Shares = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SellOriginalAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SellOriginalCurrency = table.Column<string>(type: "char(3)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SellExchangeRate = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    SellBaseAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SellBaseCurrency = table.Column<string>(type: "char(3)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SellRateDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SellDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RealizedPnL = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RealizedPnLCurrency = table.Column<string>(type: "char(3)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IdStatus = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)2),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IdHolding = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Disposals", x => x.IdDisposal);
                    table.ForeignKey(
                        name: "FK_Disposals_Holdings_IdHolding",
                        column: x => x.IdHolding,
                        principalTable: "Holdings",
                        principalColumn: "IdHolding",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Disposals_IdHolding",
                table: "Disposals",
                column: "IdHolding");

            migrationBuilder.CreateIndex(
                name: "IX_Holdings_IdCompany",
                table: "Holdings",
                column: "IdCompany");

            migrationBuilder.CreateIndex(
                name: "IX_Holdings_IdPortfolio_IdCompany",
                table: "Holdings",
                columns: new[] { "IdPortfolio", "IdCompany" });

            migrationBuilder.CreateIndex(
                name: "IX_Portfolios_IdUser",
                table: "Portfolios",
                column: "IdUser");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Disposals");

            migrationBuilder.DropTable(
                name: "Holdings");

            migrationBuilder.DropTable(
                name: "Portfolios");
        }
    }
}
