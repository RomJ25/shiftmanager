using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftManager.Migrations
{
    /// <inheritdoc />
    public partial class BackfillShiftTypeCompanies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use admin-provided overrides when present. Keys follow the format
            // "ShiftTypeCompanyOverride:<ShiftTypeId>" with the CompanyId stored as the value.
            const string sql = @"
WITH override_map AS (
    SELECT
        CAST(substr([Key], length('ShiftTypeCompanyOverride:') + 1) AS INTEGER) AS ShiftTypeId,
        CAST([Value] AS INTEGER) AS CompanyId
    FROM Configs
    WHERE [Key] LIKE 'ShiftTypeCompanyOverride:%'
),
ranked_usage AS (
    SELECT
        ShiftTypeId,
        CompanyId,
        COUNT(*) AS UsageCount,
        ROW_NUMBER() OVER (
            PARTITION BY ShiftTypeId
            ORDER BY COUNT(*) DESC, CompanyId ASC
        ) AS rn
    FROM ShiftInstances
    GROUP BY ShiftTypeId, CompanyId
),
resolved AS (
    SELECT
        st.Id,
        COALESCE(om.CompanyId, ru.CompanyId) AS CompanyId
    FROM ShiftTypes st
    LEFT JOIN override_map om ON om.ShiftTypeId = st.Id
    LEFT JOIN ranked_usage ru ON ru.ShiftTypeId = st.Id AND ru.rn = 1
    WHERE COALESCE(om.CompanyId, ru.CompanyId) IS NOT NULL
)
UPDATE ShiftTypes
SET CompanyId = (
    SELECT CompanyId
    FROM resolved
    WHERE resolved.Id = ShiftTypes.Id
)
WHERE EXISTS (
    SELECT 1
    FROM resolved
    WHERE resolved.Id = ShiftTypes.Id
);
";

            migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert to the previous behaviour by assigning every shift type to the first company.
            migrationBuilder.Sql(
                "UPDATE \"ShiftTypes\" SET \"CompanyId\" = (SELECT \"Id\" FROM \"Companies\" ORDER BY \"Id\" LIMIT 1);");
        }
    }
}
