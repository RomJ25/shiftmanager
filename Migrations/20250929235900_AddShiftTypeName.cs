using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftManager.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftTypeName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "ShiftTypes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
                UPDATE ShiftTypes
                SET Name = CASE Key
                    WHEN 'MORNING' THEN 'Morning Shift'
                    WHEN 'NOON' THEN 'Afternoon Shift'
                    WHEN 'NIGHT' THEN 'Night Shift'
                    WHEN 'MIDDLE' THEN 'Mid Shift'
                    WHEN 'EVENING' THEN 'Evening Shift'
                    ELSE Key
                END
                WHERE Name = '';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "ShiftTypes");
        }
    }
}
