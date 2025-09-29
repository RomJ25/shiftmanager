# Multi-tenant Shift Type Company Backfill

This document describes how to correct the `ShiftTypes.CompanyId` assignments after the
`BackfillShiftTypeCompanies` migration, as well as how to remediate environments that were
impacted by the earlier default-to-first-company behaviour.

## Background

The `20250928210000_AddCompanyIdToShiftTypes` migration introduced the `CompanyId` column on
`ShiftTypes` and defaulted every existing row to the first company in the database. In a
multi-tenant system this caused shift types that belonged to other companies to be
mis-assigned. The follow-up migration `20251015100000_BackfillShiftTypeCompanies` resolves the
issue by:

1. Allowing administrators to specify overrides in `Configs` using keys with the format
   `ShiftTypeCompanyOverride:<ShiftTypeId>` and a numeric `CompanyId` as the value.
2. Backfilling any remaining rows based on historical usage found in `ShiftInstances`, picking
   the company that has used the shift type most often.

## Preparing overrides (optional)

If a shift type has never been scheduled, it will not have a corresponding record in
`ShiftInstances`. In those cases, create overrides prior to running the migration:

```sql
INSERT INTO "Configs" ("CompanyId", "Key", "Value")
VALUES (<owning-company-id>, 'ShiftTypeCompanyOverride:<shift-type-id>', '<owning-company-id>');
```

The `CompanyId` column on `Configs` can hold the owning company as well (this makes the record
self-describing but is not required by the migration logic).

## Applying the migration

1. Ensure a recent backup is available.
2. Apply migrations as usual (e.g. `dotnet ef database update` or `dotnet run` in hosted
   environments). The `BackfillShiftTypeCompanies` migration will execute automatically.
3. Verify that every shift type is now assigned to the correct company using:

   ```sql
   SELECT st.Id, st.Name, st.CompanyId, c.Name AS CompanyName,
          si_counts.UsageCount
   FROM "ShiftTypes" st
   LEFT JOIN "Companies" c ON c."Id" = st."CompanyId"
   LEFT JOIN (
       SELECT "ShiftTypeId", COUNT(*) AS UsageCount
       FROM "ShiftInstances"
       GROUP BY "ShiftTypeId"
   ) si_counts ON si_counts."ShiftTypeId" = st."Id"
   ORDER BY st."CompanyId", st."Id";
   ```

4. Remove any override entries that are no longer needed to keep the configuration surface
   tidy.

## Manual remediation for already-impacted databases

For environments that cannot immediately take the migration, run the following reversible SQL
script to correct the data in place. It contains the same logic as the migration but can be
applied and rolled back manually.

### Fix script

```sql
-- Optional overrides
WITH override_map AS (
    SELECT
        CAST(substr([Key], length('ShiftTypeCompanyOverride:') + 1) AS INTEGER) AS ShiftTypeId,
        CAST([Value] AS INTEGER) AS CompanyId
    FROM "Configs"
    WHERE [Key] LIKE 'ShiftTypeCompanyOverride:%'
),
ranked_usage AS (
    SELECT
        "ShiftTypeId",
        "CompanyId",
        COUNT(*) AS UsageCount,
        ROW_NUMBER() OVER (
            PARTITION BY "ShiftTypeId"
            ORDER BY COUNT(*) DESC, "CompanyId" ASC
        ) AS rn
    FROM "ShiftInstances"
    GROUP BY "ShiftTypeId", "CompanyId"
),
resolved AS (
    SELECT
        st."Id",
        COALESCE(om.CompanyId, ru."CompanyId") AS CompanyId
    FROM "ShiftTypes" st
    LEFT JOIN override_map om ON om.ShiftTypeId = st."Id"
    LEFT JOIN ranked_usage ru ON ru."ShiftTypeId" = st."Id" AND ru.rn = 1
    WHERE COALESCE(om.CompanyId, ru."CompanyId") IS NOT NULL
)
UPDATE "ShiftTypes"
SET "CompanyId" = (
    SELECT CompanyId
    FROM resolved
    WHERE resolved.Id = "ShiftTypes"."Id"
)
WHERE EXISTS (
    SELECT 1
    FROM resolved
    WHERE resolved.Id = "ShiftTypes"."Id"
);
```

### Revert script

```sql
UPDATE "ShiftTypes"
SET "CompanyId" = (
    SELECT "Id" FROM "Companies" ORDER BY "Id" LIMIT 1
);
```

> **Note:** The revert script intentionally returns the database to the pre-fix state where
> all shift types point to the first company. Use it only if the remediation must be undone
> before the permanent migration can be applied.

## Deployment checklist

- [ ] Identify any shift types without historical usage and add override entries.
- [ ] Back up the database.
- [ ] Apply migrations through the standard deployment pipeline.
- [ ] Validate company ownership using the verification query.
- [ ] Remove stale override entries.
- [ ] Record the remediation in the change log or runbook for the tenant.

