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

            // Backfill: Duplicate existing ShiftTypes for each company
            // Get all existing ShiftTypes (currently with CompanyId=0)
            // For each company, create a copy of each ShiftType
            migrationBuilder.Sql(@"
                -- Create company-specific copies of each shift type
                INSERT INTO ShiftTypes (CompanyId, Key, Start, End)
                SELECT c.Id, st.Key, st.Start, st.End
                FROM Companies c
                CROSS JOIN (SELECT DISTINCT Key, Start, End FROM ShiftTypes WHERE CompanyId = 0) st;

                -- Update ShiftInstances to reference the new company-specific ShiftTypes
                -- For each ShiftInstance, find the matching ShiftType for its company
                UPDATE ShiftInstances
                SET ShiftTypeId = (
                    SELECT st_new.Id
                    FROM ShiftTypes st_new
                    INNER JOIN ShiftTypes st_old ON st_new.Key = st_old.Key
                    WHERE st_old.Id = ShiftInstances.ShiftTypeId
                      AND st_new.CompanyId = ShiftInstances.CompanyId
                    LIMIT 1
                )
                WHERE EXISTS (
                    SELECT 1 FROM ShiftTypes st WHERE st.Id = ShiftInstances.ShiftTypeId AND st.CompanyId = 0
                );

                -- Delete the old ShiftTypes with CompanyId=0
                DELETE FROM ShiftTypes WHERE CompanyId = 0;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTypes_CompanyId_Key",
                table: "ShiftTypes",
                columns: new[] { "CompanyId", "Key" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShiftTypes_CompanyId_Key",
                table: "ShiftTypes");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ShiftTypes");
        }
    }
}
