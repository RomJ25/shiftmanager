using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftManager.Migrations
{
    /// <inheritdoc />
    public partial class FixShiftTypeKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove the SHIFTTYPE_ prefix from shift type keys if present
            migrationBuilder.Sql(@"
                UPDATE ShiftTypes
                SET Key = REPLACE(Key, 'SHIFTTYPE_', '')
                WHERE Key LIKE 'SHIFTTYPE_%'
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the SHIFTTYPE_ prefix if rolling back
            migrationBuilder.Sql(@"
                UPDATE ShiftTypes
                SET Key = 'SHIFTTYPE_' || Key
                WHERE Key IN ('MORNING', 'NOON', 'NIGHT', 'MIDDLE', 'EVENING')
            ");
        }
    }
}
