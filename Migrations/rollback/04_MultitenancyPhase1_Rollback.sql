BEGIN TRANSACTION;
DROP INDEX "IX_UserNotifications_CompanyId_UserId_CreatedAt";

DROP INDEX "IX_UserNotifications_UserId";

DROP INDEX "IX_TimeOffRequests_CompanyId_UserId_StartDate";

DROP INDEX "IX_SwapRequests_CompanyId_Status_CreatedAt";

DROP INDEX "IX_ShiftAssignments_CompanyId_ShiftInstanceId_UserId";

DROP INDEX "IX_ShiftAssignments_ShiftInstanceId";

DROP INDEX "IX_Companies_Slug";

CREATE INDEX "IX_UserNotifications_UserId_CreatedAt" ON "UserNotifications" ("UserId", "CreatedAt");

CREATE UNIQUE INDEX "IX_ShiftAssignments_ShiftInstanceId_UserId" ON "ShiftAssignments" ("ShiftInstanceId", "UserId");

CREATE TABLE "ef_temp_UserNotifications" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_UserNotifications" PRIMARY KEY AUTOINCREMENT,
    "CreatedAt" TEXT NOT NULL,
    "IsRead" INTEGER NOT NULL,
    "Message" TEXT NOT NULL,
    "ReadAt" TEXT NULL,
    "RelatedEntityId" INTEGER NULL,
    "RelatedEntityType" TEXT NULL,
    "Title" TEXT NOT NULL,
    "Type" INTEGER NOT NULL,
    "UserId" INTEGER NOT NULL,
    CONSTRAINT "FK_UserNotifications_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

INSERT INTO "ef_temp_UserNotifications" ("Id", "CreatedAt", "IsRead", "Message", "ReadAt", "RelatedEntityId", "RelatedEntityType", "Title", "Type", "UserId")
SELECT "Id", "CreatedAt", "IsRead", "Message", "ReadAt", "RelatedEntityId", "RelatedEntityType", "Title", "Type", "UserId"
FROM "UserNotifications";

CREATE TABLE "ef_temp_TimeOffRequests" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_TimeOffRequests" PRIMARY KEY AUTOINCREMENT,
    "CreatedAt" TEXT NOT NULL,
    "EndDate" TEXT NOT NULL,
    "Reason" TEXT NULL,
    "StartDate" TEXT NOT NULL,
    "Status" INTEGER NOT NULL,
    "UserId" INTEGER NOT NULL
);

INSERT INTO "ef_temp_TimeOffRequests" ("Id", "CreatedAt", "EndDate", "Reason", "StartDate", "Status", "UserId")
SELECT "Id", "CreatedAt", "EndDate", "Reason", "StartDate", "Status", "UserId"
FROM "TimeOffRequests";

CREATE TABLE "ef_temp_SwapRequests" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_SwapRequests" PRIMARY KEY AUTOINCREMENT,
    "CreatedAt" TEXT NOT NULL,
    "FromAssignmentId" INTEGER NOT NULL,
    "Status" INTEGER NOT NULL,
    "ToUserId" INTEGER NOT NULL
);

INSERT INTO "ef_temp_SwapRequests" ("Id", "CreatedAt", "FromAssignmentId", "Status", "ToUserId")
SELECT "Id", "CreatedAt", "FromAssignmentId", "Status", "ToUserId"
FROM "SwapRequests";

CREATE TABLE "ef_temp_ShiftAssignments" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ShiftAssignments" PRIMARY KEY AUTOINCREMENT,
    "CreatedAt" TEXT NOT NULL,
    "ShiftInstanceId" INTEGER NOT NULL,
    "UserId" INTEGER NOT NULL,
    CONSTRAINT "FK_ShiftAssignments_ShiftInstances_ShiftInstanceId" FOREIGN KEY ("ShiftInstanceId") REFERENCES "ShiftInstances" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ShiftAssignments_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

INSERT INTO "ef_temp_ShiftAssignments" ("Id", "CreatedAt", "ShiftInstanceId", "UserId")
SELECT "Id", "CreatedAt", "ShiftInstanceId", "UserId"
FROM "ShiftAssignments";

CREATE TABLE "ef_temp_Companies" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Companies" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL
);

INSERT INTO "ef_temp_Companies" ("Id", "Name")
SELECT "Id", "Name"
FROM "Companies";

COMMIT;

PRAGMA foreign_keys = 0;

BEGIN TRANSACTION;
DROP TABLE "UserNotifications";

ALTER TABLE "ef_temp_UserNotifications" RENAME TO "UserNotifications";

DROP TABLE "TimeOffRequests";

ALTER TABLE "ef_temp_TimeOffRequests" RENAME TO "TimeOffRequests";

DROP TABLE "SwapRequests";

ALTER TABLE "ef_temp_SwapRequests" RENAME TO "SwapRequests";

DROP TABLE "ShiftAssignments";

ALTER TABLE "ef_temp_ShiftAssignments" RENAME TO "ShiftAssignments";

DROP TABLE "Companies";

ALTER TABLE "ef_temp_Companies" RENAME TO "Companies";

COMMIT;

PRAGMA foreign_keys = 1;

BEGIN TRANSACTION;
CREATE INDEX "IX_UserNotifications_UserId_CreatedAt" ON "UserNotifications" ("UserId", "CreatedAt");

CREATE UNIQUE INDEX "IX_ShiftAssignments_ShiftInstanceId_UserId" ON "ShiftAssignments" ("ShiftInstanceId", "UserId");

CREATE INDEX "IX_ShiftAssignments_UserId" ON "ShiftAssignments" ("UserId");

COMMIT;

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20250930114957_MultitenancyPhase1';

