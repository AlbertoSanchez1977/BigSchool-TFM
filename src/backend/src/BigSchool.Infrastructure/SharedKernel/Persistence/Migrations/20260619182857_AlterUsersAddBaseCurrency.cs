using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BigSchool.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlterUsersAddBaseCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BaseCurrency",
                table: "Users",
                type: "char(3)",
                nullable: false,
                defaultValueSql: "'EUR'")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseCurrency",
                table: "Users");
        }
    }
}
