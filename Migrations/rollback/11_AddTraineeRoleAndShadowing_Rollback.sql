BEGIN TRANSACTION;
DROP INDEX "IX_ShiftAssignments_TraineeId";

CREATE TABLE "ef_temp_ShiftAssignments" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ShiftAssignments" PRIMARY KEY AUTOINCREMENT,
    "CompanyId" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "ShiftInstanceId" INTEGER NOT NULL,
    "UserId" INTEGER NOT NULL,
    CONSTRAINT "FK_ShiftAssignments_ShiftInstances_ShiftInstanceId" FOREIGN KEY ("ShiftInstanceId") REFERENCES "ShiftInstances" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ShiftAssignments_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

INSERT INTO "ef_temp_ShiftAssignments" ("Id", "CompanyId", "CreatedAt", "ShiftInstanceId", "UserId")
SELECT "Id", "CompanyId", "CreatedAt", "ShiftInstanceId", "UserId"
FROM "ShiftAssignments";

COMMIT;

PRAGMA foreign_keys = 0;

BEGIN TRANSACTION;
DROP TABLE "ShiftAssignments";

ALTER TABLE "ef_temp_ShiftAssignments" RENAME TO "ShiftAssignments";

COMMIT;

PRAGMA foreign_keys = 1;

BEGIN TRANSACTION;
CREATE UNIQUE INDEX "IX_ShiftAssignments_CompanyId_ShiftInstanceId_UserId" ON "ShiftAssignments" ("CompanyId", "ShiftInstanceId", "UserId");

CREATE INDEX "IX_ShiftAssignments_ShiftInstanceId" ON "ShiftAssignments" ("ShiftInstanceId");

CREATE INDEX "IX_ShiftAssignments_UserId" ON "ShiftAssignments" ("UserId");

COMMIT;

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20251004215820_AddTraineeRoleAndShadowing';

