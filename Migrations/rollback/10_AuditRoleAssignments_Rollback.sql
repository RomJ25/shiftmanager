BEGIN TRANSACTION;
DROP TABLE "RoleAssignmentAudits";

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20251003232434_AuditRoleAssignments';

COMMIT;

