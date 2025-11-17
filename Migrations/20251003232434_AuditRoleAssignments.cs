using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftManager.Migrations
{
    /// <inheritdoc />
    public partial class AuditRoleAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoleAssignmentAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChangedBy = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    FromRole = table.Column<int>(type: "INTEGER", nullable: true),
                    ToRole = table.Column<int>(type: "INTEGER", nullable: false),
                    CompanyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleAssignmentAudits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignmentAudits_ChangedBy_Timestamp",
                table: "RoleAssignmentAudits",
                columns: new[] { "ChangedBy", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignmentAudits_CompanyId_TargetUserId_Timestamp",
                table: "RoleAssignmentAudits",
                columns: new[] { "CompanyId", "TargetUserId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoleAssignmentAudits");
        }
    }
}
