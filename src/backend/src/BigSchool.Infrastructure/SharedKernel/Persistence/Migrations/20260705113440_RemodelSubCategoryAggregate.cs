using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BigSchool.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemodelSubCategoryAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubCategories_Users_IdUser",
                table: "SubCategories");

            migrationBuilder.DropIndex(
                name: "IX_SubCategories_IdUser",
                table: "SubCategories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SubCategories_IdUser",
                table: "SubCategories",
                column: "IdUser");

            migrationBuilder.AddForeignKey(
                name: "FK_SubCategories_Users_IdUser",
                table: "SubCategories",
                column: "IdUser",
                principalTable: "Users",
                principalColumn: "IdUser",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
