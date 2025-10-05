BEGIN TRANSACTION;
DROP TABLE "UserJoinRequests";

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20250930221748_AddUserJoinRequests';

COMMIT;

