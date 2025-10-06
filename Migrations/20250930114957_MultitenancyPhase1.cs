using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftManager.Migrations
{
    /// <inheritdoc />
    public partial class MultitenancyPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserNotifications_UserId_CreatedAt",
                table: "UserNotifications");

            migrationBuilder.DropIndex(
                name: "IX_ShiftAssignments_ShiftInstanceId_UserId",
                table: "ShiftAssignments");

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "UserNotifications",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "TimeOffRequests",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "SwapRequests",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "ShiftAssignments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "Companies",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettingsJson",
                table: "Companies",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Companies",
                type: "TEXT",
                nullable: true);

            // Multitenancy Phase 1: Populate Company fields (Slug, DisplayName, SettingsJson)
            migrationBuilder.Sql(@"
                UPDATE Companies
                SET Slug = lower(replace(trim(Name), ' ', '-')),
                    DisplayName = Name,
                    SettingsJson = '{}'
                WHERE Slug IS NULL;
            ");

            // Multitenancy Phase 1: Backfill CompanyId for existing records using deterministic joins
            // UserNotifications: Join via Users table
            migrationBuilder.Sql(@"
                UPDATE UserNotifications
                SET CompanyId = (
                    SELECT u.CompanyId
                    FROM Users u
                    WHERE u.Id = UserNotifications.UserId
                )
                WHERE CompanyId = 0
                  AND EXISTS (SELECT 1 FROM Users u WHERE u.Id = UserNotifications.UserId);
            ");

            // TimeOffRequests: Join via Users table
            migrationBuilder.Sql(@"
                UPDATE TimeOffRequests
                SET CompanyId = (
                    SELECT u.CompanyId
                    FROM Users u
                    WHERE u.Id = TimeOffRequests.UserId
                )
                WHERE CompanyId = 0
                  AND EXISTS (SELECT 1 FROM Users u WHERE u.Id = TimeOffRequests.UserId);
            ");

            // ShiftAssignments: Join via ShiftInstances table
            migrationBuilder.Sql(@"
                UPDATE ShiftAssignments
                SET CompanyId = (
                    SELECT si.CompanyId
                    FROM ShiftInstances si
                    WHERE si.Id = ShiftAssignments.ShiftInstanceId
                )
                WHERE CompanyId = 0
                  AND EXISTS (SELECT 1 FROM ShiftInstances si WHERE si.Id = ShiftAssignments.ShiftInstanceId);
            ");

            // SwapRequests: Join via ShiftAssignments → ShiftInstances
            migrationBuilder.Sql(@"
                UPDATE SwapRequests
                SET CompanyId = (
                    SELECT si.CompanyId
                    FROM ShiftAssignments sa
                    INNER JOIN ShiftInstances si ON sa.ShiftInstanceId = si.Id
                    WHERE sa.Id = SwapRequests.FromAssignmentId
                )
                WHERE CompanyId = 0
                  AND EXISTS (
                      SELECT 1
                      FROM ShiftAssignments sa
                      INNER JOIN ShiftInstances si ON sa.ShiftInstanceId = si.Id
                      WHERE sa.Id = SwapRequests.FromAssignmentId
                  );
            ");

            // Fallback: Any remaining records with CompanyId = 0 use first available company
            // This handles edge cases where foreign key relationships are broken
            migrationBuilder.Sql(@"
                UPDATE UserNotifications
                SET CompanyId = (SELECT MIN(Id) FROM Companies)
                WHERE CompanyId = 0;

                UPDATE TimeOffRequests
                SET CompanyId = (SELECT MIN(Id) FROM Companies)
                WHERE CompanyId = 0;

                UPDATE ShiftAssignments
                SET CompanyId = (SELECT MIN(Id) FROM Companies)
                WHERE CompanyId = 0;

                UPDATE SwapRequests
                SET CompanyId = (SELECT MIN(Id) FROM Companies)
                WHERE CompanyId = 0;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_CompanyId_UserId_CreatedAt",
                table: "UserNotifications",
                columns: new[] { "CompanyId", "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_UserId",
                table: "UserNotifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffRequests_CompanyId_UserId_StartDate",
                table: "TimeOffRequests",
                columns: new[] { "CompanyId", "UserId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SwapRequests_CompanyId_Status_CreatedAt",
                table: "SwapRequests",
                columns: new[] { "CompanyId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_CompanyId_ShiftInstanceId_UserId",
                table: "ShiftAssignments",
                columns: new[] { "CompanyId", "ShiftInstanceId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_ShiftInstanceId",
                table: "ShiftAssignments",
                column: "ShiftInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Slug",
                table: "Companies",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserNotifications_CompanyId_UserId_CreatedAt",
                table: "UserNotifications");

            migrationBuilder.DropIndex(
                name: "IX_UserNotifications_UserId",
                table: "UserNotifications");

            migrationBuilder.DropIndex(
                name: "IX_TimeOffRequests_CompanyId_UserId_StartDate",
                table: "TimeOffRequests");

            migrationBuilder.DropIndex(
                name: "IX_SwapRequests_CompanyId_Status_CreatedAt",
                table: "SwapRequests");

            migrationBuilder.DropIndex(
                name: "IX_ShiftAssignments_CompanyId_ShiftInstanceId_UserId",
                table: "ShiftAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ShiftAssignments_ShiftInstanceId",
                table: "ShiftAssignments");

            migrationBuilder.DropIndex(
                name: "IX_Companies_Slug",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "TimeOffRequests");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "SwapRequests");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ShiftAssignments");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "SettingsJson",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Companies");

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_UserId_CreatedAt",
                table: "UserNotifications",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_ShiftInstanceId_UserId",
                table: "ShiftAssignments",
                columns: new[] { "ShiftInstanceId", "UserId" },
                unique: true);
        }
    }
}
