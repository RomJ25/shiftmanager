# Database Migration Rollback Scripts

This directory contains SQL rollback scripts for all database migrations in the ShiftManager application.

## Overview

Each rollback script reverts a specific migration to the previous migration state. The scripts are numbered to match their corresponding migrations.

## Rollback Scripts

| Script | Description | Reverts From | Reverts To |
|--------|-------------|--------------|------------|
| `01_InitialCreate_Rollback.sql` | Remove all database tables | InitialCreate | Empty database |
| `02_AddUserNotifications_Rollback.sql` | Remove UserNotifications table | AddUserNotifications | InitialCreate |
| `03_AddNavigationProperties_Rollback.sql` | Remove navigation properties | AddNavigationProperties | AddUserNotifications |
| `04_MultitenancyPhase1_Rollback.sql` | Remove CompanyId and multi-tenancy | MultitenancyPhase1 | AddNavigationProperties |
| `05_AddCompanyIdToShiftTypes_Rollback.sql` | Remove CompanyId from ShiftTypes | AddCompanyIdToShiftTypes | MultitenancyPhase1 |
| `06_AddDirectorRole_Rollback.sql` | Remove Director role and table | AddDirectorRole | AddCompanyIdToShiftTypes |
| `07_AddUserJoinRequests_Rollback.sql` | Remove UserJoinRequests table | AddUserJoinRequests | AddDirectorRole |
| `08_UpdateJoinRequestPasswordTypes_Rollback.sql` | Revert password field types | UpdateJoinRequestPasswordTypes | AddUserJoinRequests |
| `09_FixShiftTypeKeys_Rollback.sql` | Revert ShiftType key changes | FixShiftTypeKeys | UpdateJoinRequestPasswordTypes |
| `10_AuditRoleAssignments_Rollback.sql` | Remove audit columns | AuditRoleAssignments | FixShiftTypeKeys |
| `11_AddTraineeRoleAndShadowing_Rollback.sql` | Remove Trainee role and shadowing | AddTraineeRoleAndShadowing | AuditRoleAssignments |

## Safe Rollback Procedure

### Prerequisites

1. **Backup the database** before any rollback operation
2. **Schedule maintenance window** - application should be stopped during rollback
3. **Test rollback on staging environment** first
4. **Verify data integrity** after rollback

### Step-by-Step Rollback

#### Option 1: Using EF Core CLI (Recommended)

```bash
# Rollback to a specific migration
dotnet ef database update <TARGET_MIGRATION_NAME> --context AppDbContext

# Example: Rollback to migration #10 (AuditRoleAssignments)
dotnet ef database update AuditRoleAssignments --context AppDbContext
```

#### Option 2: Using SQL Scripts (Manual)

⚠️ **IMPORTANT NOTES:**
- Some migrations contain non-transactional PRAGMA operations (see ISSUE-005)
- Affected migrations: #3, #8, #11
- If rollback fails mid-execution, manual database recovery may be required

**Procedure:**

1. **Backup database:**
   ```bash
   # For SQLite
   cp app.db app.db.backup.$(date +%Y%m%d_%H%M%S)
   ```

2. **Stop the application:**
   ```bash
   # Kill all running instances
   pkill -f ShiftManager
   ```

3. **Apply rollback script:**
   ```bash
   # Connect to SQLite database
   sqlite3 app.db < Migrations/rollback/11_AddTraineeRoleAndShadowing_Rollback.sql
   ```

4. **Update migration history:**
   ```sql
   -- Remove the migration entry from __EFMigrationsHistory
   DELETE FROM __EFMigrationsHistory
   WHERE MigrationId = '20251004215820_AddTraineeRoleAndShadowing';
   ```

5. **Verify database schema:**
   ```bash
   sqlite3 app.db ".schema"
   ```

6. **Restart application and test**

### Production Rollback Checklist

- [ ] Database backup completed and verified
- [ ] Staging environment tested with same rollback
- [ ] Maintenance window scheduled and communicated
- [ ] Application stopped (all instances)
- [ ] Rollback SQL script reviewed
- [ ] Database backup re-verified before execution
- [ ] Rollback script executed
- [ ] Migration history table updated
- [ ] Database schema verified
- [ ] Application restarted
- [ ] Smoke tests passed
- [ ] Monitoring alerts configured

### Emergency Rollback Recovery

If a rollback fails mid-execution:

1. **DO NOT PANIC** - the backup exists
2. **Stop the application immediately**
3. **Restore from backup:**
   ```bash
   cp app.db.backup.<timestamp> app.db
   ```
4. **Verify database integrity:**
   ```bash
   sqlite3 app.db "PRAGMA integrity_check;"
   ```
5. **Investigate the failure** before attempting rollback again
6. **Contact database administrator** if unsure

### Testing Rollback Scripts

Before production use, test each rollback script:

```bash
# 1. Create test database
cp app.db test.db

# 2. Apply rollback to test database
sqlite3 test.db < Migrations/rollback/11_AddTraineeRoleAndShadowing_Rollback.sql

# 3. Verify schema matches previous migration
dotnet ef migrations script AuditRoleAssignments AddTraineeRoleAndShadowing --context AppDbContext

# 4. Re-apply migration to test forward migration still works
dotnet ef database update AddTraineeRoleAndShadowing --context AppDbContext
```

## Non-Transactional PRAGMA Operations

⚠️ **WARNING:** Migrations #3, #8, and #11 contain PRAGMA operations that cannot be executed in transactions.

**Affected Migrations:**
- `20250928201142_AddNavigationProperties`
- `20250930222145_UpdateJoinRequestPasswordTypes`
- `20251004215820_AddTraineeRoleAndShadowing`

**Risk Mitigation:**
1. Always backup database before these rollbacks
2. Test rollback on staging environment first
3. Execute during maintenance window only
4. Have database administrator on standby
5. Keep backup until rollback verified successful

## See Also

- [ISSUE-002](../../tasks.md#issue-002-missing-explicit-migration-rollback-scripts) - Missing rollback scripts tracking issue
- [ISSUE-005](../../tasks.md#issue-005-non-transactional-migration-operations) - Non-transactional PRAGMA operations
- [Database Documentation](../../project.md#database-schema) - Full schema documentation
