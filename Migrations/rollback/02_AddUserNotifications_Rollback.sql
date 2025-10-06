BEGIN TRANSACTION;
DROP TABLE "UserNotifications";

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20250928195641_AddUserNotifications';

COMMIT;

