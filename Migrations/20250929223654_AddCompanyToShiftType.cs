using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftManager.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyToShiftType : Migration
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

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ShiftTypes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "ShiftTypes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTypes_CompanyId_Key",
                table: "ShiftTypes",
                columns: new[] { "CompanyId", "Key" });

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
                name: "IX_ShiftTypes_CompanyId_Key",
                table: "ShiftTypes");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ShiftTypes");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ShiftTypes");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "ShiftTypes");
        }
    }
}
