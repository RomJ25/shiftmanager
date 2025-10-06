BEGIN TRANSACTION;
DROP TABLE "Companies";

DROP TABLE "Configs";

DROP TABLE "ShiftAssignments";

DROP TABLE "ShiftInstances";

DROP TABLE "ShiftTypes";

DROP TABLE "SwapRequests";

DROP TABLE "TimeOffRequests";

DROP TABLE "Users";

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20250927202116_InitialCreate';

COMMIT;

