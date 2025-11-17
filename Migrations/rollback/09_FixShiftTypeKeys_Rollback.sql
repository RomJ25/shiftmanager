BEGIN TRANSACTION;

                UPDATE ShiftTypes
                SET Key = 'SHIFTTYPE_' || Key
                WHERE Key IN ('MORNING', 'NOON', 'NIGHT', 'MIDDLE', 'EVENING')
            

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20251003185234_FixShiftTypeKeys';

COMMIT;

