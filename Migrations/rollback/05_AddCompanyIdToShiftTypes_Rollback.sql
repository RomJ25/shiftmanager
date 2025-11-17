BEGIN TRANSACTION;
DROP INDEX "IX_ShiftTypes_CompanyId_Key";

CREATE TABLE "ef_temp_ShiftTypes" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ShiftTypes" PRIMARY KEY AUTOINCREMENT,
    "End" TEXT NOT NULL,
    "Key" TEXT NOT NULL,
    "Start" TEXT NOT NULL
);

INSERT INTO "ef_temp_ShiftTypes" ("Id", "End", "Key", "Start")
SELECT "Id", "End", "Key", "Start"
FROM "ShiftTypes";

COMMIT;

PRAGMA foreign_keys = 0;

BEGIN TRANSACTION;
DROP TABLE "ShiftTypes";

ALTER TABLE "ef_temp_ShiftTypes" RENAME TO "ShiftTypes";

COMMIT;

PRAGMA foreign_keys = 1;

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20250930194706_AddCompanyIdToShiftTypes';

