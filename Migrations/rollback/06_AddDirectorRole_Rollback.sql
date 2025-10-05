BEGIN TRANSACTION;
DROP TABLE "DirectorCompanies";

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20250930210753_AddDirectorRole';

COMMIT;

