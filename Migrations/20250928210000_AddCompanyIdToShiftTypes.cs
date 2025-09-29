using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftManager.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyIdToShiftTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "ShiftTypes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                "UPDATE \"ShiftTypes\" SET \"CompanyId\" = (SELECT \"Id\" FROM \"Companies\" ORDER BY \"Id\" LIMIT 1);");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTypes_CompanyId",
                table: "ShiftTypes",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftTypes_Companies_CompanyId",
                table: "ShiftTypes",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShiftTypes_Companies_CompanyId",
                table: "ShiftTypes");

            migrationBuilder.DropIndex(
                name: "IX_ShiftTypes_CompanyId",
                table: "ShiftTypes");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ShiftTypes");
        }
    }
}
