BEGIN TRANSACTION;
DROP INDEX "IX_ShiftInstances_ShiftTypeId";

DROP INDEX "IX_ShiftAssignments_UserId";

CREATE TABLE "ef_temp_ShiftAssignments" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ShiftAssignments" PRIMARY KEY AUTOINCREMENT,
    "CreatedAt" TEXT NOT NULL,
    "ShiftInstanceId" INTEGER NOT NULL,
    "UserId" INTEGER NOT NULL
);

INSERT INTO "ef_temp_ShiftAssignments" ("Id", "CreatedAt", "ShiftInstanceId", "UserId")
SELECT "Id", "CreatedAt", "ShiftInstanceId", "UserId"
FROM "ShiftAssignments";

CREATE TABLE "ef_temp_ShiftInstances" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ShiftInstances" PRIMARY KEY AUTOINCREMENT,
    "CompanyId" INTEGER NOT NULL,
    "Concurrency" INTEGER NOT NULL,
    "Name" TEXT NOT NULL,
    "ShiftTypeId" INTEGER NOT NULL,
    "StaffingRequired" INTEGER NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    "WorkDate" TEXT NOT NULL
);

INSERT INTO "ef_temp_ShiftInstances" ("Id", "CompanyId", "Concurrency", "Name", "ShiftTypeId", "StaffingRequired", "UpdatedAt", "WorkDate")
SELECT "Id", "CompanyId", "Concurrency", "Name", "ShiftTypeId", "StaffingRequired", "UpdatedAt", "WorkDate"
FROM "ShiftInstances";

COMMIT;

PRAGMA foreign_keys = 0;

BEGIN TRANSACTION;
DROP TABLE "ShiftAssignments";

ALTER TABLE "ef_temp_ShiftAssignments" RENAME TO "ShiftAssignments";

DROP TABLE "ShiftInstances";

ALTER TABLE "ef_temp_ShiftInstances" RENAME TO "ShiftInstances";

COMMIT;

PRAGMA foreign_keys = 1;

BEGIN TRANSACTION;
CREATE UNIQUE INDEX "IX_ShiftAssignments_ShiftInstanceId_UserId" ON "ShiftAssignments" ("ShiftInstanceId", "UserId");

COMMIT;

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20250928201142_AddNavigationProperties';

