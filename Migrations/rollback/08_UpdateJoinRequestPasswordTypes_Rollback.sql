BEGIN TRANSACTION;
CREATE TABLE "ef_temp_UserJoinRequests" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_UserJoinRequests" PRIMARY KEY AUTOINCREMENT,
    "CompanyId" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "CreatedUserId" INTEGER NULL,
    "DisplayName" TEXT NOT NULL,
    "Email" TEXT NOT NULL,
    "PasswordHash" TEXT NOT NULL,
    "PasswordSalt" TEXT NOT NULL,
    "RejectionReason" TEXT NULL,
    "RequestedRole" INTEGER NOT NULL,
    "ReviewedAt" TEXT NULL,
    "ReviewedBy" INTEGER NULL,
    "Status" INTEGER NOT NULL,
    CONSTRAINT "FK_UserJoinRequests_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_UserJoinRequests_Users_CreatedUserId" FOREIGN KEY ("CreatedUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_UserJoinRequests_Users_ReviewedBy" FOREIGN KEY ("ReviewedBy") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

INSERT INTO "ef_temp_UserJoinRequests" ("Id", "CompanyId", "CreatedAt", "CreatedUserId", "DisplayName", "Email", "PasswordHash", "PasswordSalt", "RejectionReason", "RequestedRole", "ReviewedAt", "ReviewedBy", "Status")
SELECT "Id", "CompanyId", "CreatedAt", "CreatedUserId", "DisplayName", "Email", "PasswordHash", "PasswordSalt", "RejectionReason", "RequestedRole", "ReviewedAt", "ReviewedBy", "Status"
FROM "UserJoinRequests";

COMMIT;

PRAGMA foreign_keys = 0;

BEGIN TRANSACTION;
DROP TABLE "UserJoinRequests";

ALTER TABLE "ef_temp_UserJoinRequests" RENAME TO "UserJoinRequests";

COMMIT;

PRAGMA foreign_keys = 1;

BEGIN TRANSACTION;
CREATE INDEX "IX_UserJoinRequests_CompanyId_Status_CreatedAt" ON "UserJoinRequests" ("CompanyId", "Status", "CreatedAt");

CREATE INDEX "IX_UserJoinRequests_CreatedUserId" ON "UserJoinRequests" ("CreatedUserId");

CREATE INDEX "IX_UserJoinRequests_Email_CompanyId_Status" ON "UserJoinRequests" ("Email", "CompanyId", "Status");

CREATE INDEX "IX_UserJoinRequests_ReviewedBy" ON "UserJoinRequests" ("ReviewedBy");

COMMIT;

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20250930222145_UpdateJoinRequestPasswordTypes';

