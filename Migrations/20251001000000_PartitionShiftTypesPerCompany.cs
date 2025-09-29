using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftManager.Migrations
{
    /// <inheritdoc />
    public partial class PartitionShiftTypesPerCompany : Migration
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

            migrationBuilder.Sql(@"
                UPDATE ShiftTypes
                SET CompanyId = (
                    SELECT CompanyId
                    FROM ShiftInstances
                    WHERE ShiftInstances.ShiftTypeId = ShiftTypes.Id
                    LIMIT 1
                )
                WHERE EXISTS (
                    SELECT 1
                    FROM ShiftInstances
                    WHERE ShiftInstances.ShiftTypeId = ShiftTypes.Id
                );
            ");

            migrationBuilder.Sql(@"
                UPDATE ShiftTypes
                SET CompanyId = (
                    SELECT Id
                    FROM Companies
                    ORDER BY Id
                    LIMIT 1
                )
                WHERE CompanyId = 0;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTypes_CompanyId",
                table: "ShiftTypes",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTypes_CompanyId_Key",
                table: "ShiftTypes",
                columns: new[] { "CompanyId", "Key" },
                unique: true);

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

            migrationBuilder.DropIndex(
                name: "IX_ShiftTypes_CompanyId",
                table: "ShiftTypes");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ShiftTypes");
        }
    }
}
