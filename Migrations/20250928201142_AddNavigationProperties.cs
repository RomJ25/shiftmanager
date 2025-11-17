using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftManager.Migrations
{
    /// <inheritdoc />
    public partial class AddNavigationProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ShiftInstances_ShiftTypeId",
                table: "ShiftInstances",
                column: "ShiftTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_UserId",
                table: "ShiftAssignments",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftAssignments_ShiftInstances_ShiftInstanceId",
                table: "ShiftAssignments",
                column: "ShiftInstanceId",
                principalTable: "ShiftInstances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftAssignments_Users_UserId",
                table: "ShiftAssignments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftInstances_ShiftTypes_ShiftTypeId",
                table: "ShiftInstances",
                column: "ShiftTypeId",
                principalTable: "ShiftTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShiftAssignments_ShiftInstances_ShiftInstanceId",
                table: "ShiftAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftAssignments_Users_UserId",
                table: "ShiftAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftInstances_ShiftTypes_ShiftTypeId",
                table: "ShiftInstances");

            migrationBuilder.DropIndex(
                name: "IX_ShiftInstances_ShiftTypeId",
                table: "ShiftInstances");

            migrationBuilder.DropIndex(
                name: "IX_ShiftAssignments_UserId",
                table: "ShiftAssignments");
        }
    }
}
