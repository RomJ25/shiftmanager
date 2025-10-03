-- Rollback SQL for MultitenancyPhase1 Migration
-- This script safely reverts the multitenancy Phase 1 changes
-- Execute this script ONLY if you need to rollback without using EF migrations

-- WARNING: This will remove all CompanyId data and Company metadata (Slug, DisplayName, SettingsJson)
-- Make sure you have a database backup before executing this script!

BEGIN TRANSACTION;

-- Step 1: Drop the new composite indexes
DROP INDEX IF EXISTS IX_UserNotifications_CompanyId_UserId_CreatedAt;
DROP INDEX IF EXISTS IX_TimeOffRequests_CompanyId_UserId_StartDate;
DROP INDEX IF EXISTS IX_SwapRequests_CompanyId_Status_CreatedAt;
DROP INDEX IF EXISTS IX_ShiftAssignments_CompanyId_ShiftInstanceId_UserId;
DROP INDEX IF EXISTS IX_Companies_Slug;

-- Step 2: Drop additional indexes that were created
DROP INDEX IF EXISTS IX_UserNotifications_UserId;
DROP INDEX IF EXISTS IX_ShiftAssignments_ShiftInstanceId;

-- Step 3: Remove CompanyId columns from tenant-scoped entities
-- Note: SQLite doesn't support DROP COLUMN directly in older versions
-- We need to recreate tables without the CompanyId column

-- Backup and recreate UserNotifications
CREATE TABLE UserNotifications_Backup AS SELECT * FROM UserNotifications;

DROP TABLE UserNotifications;

CREATE TABLE UserNotifications (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER NOT NULL,
    Type INTEGER NOT NULL,
    Title TEXT NOT NULL,
    Message TEXT NOT NULL,
    IsRead INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    RelatedEntityId INTEGER,
    RelatedEntityType TEXT,
    FOREIGN KEY (UserId) REFERENCES Users(Id)
);

INSERT INTO UserNotifications (Id, UserId, Type, Title, Message, IsRead, CreatedAt, RelatedEntityId, RelatedEntityType)
SELECT Id, UserId, Type, Title, Message, IsRead, CreatedAt, RelatedEntityId, RelatedEntityType
FROM UserNotifications_Backup;

DROP TABLE UserNotifications_Backup;

-- Backup and recreate TimeOffRequests
CREATE TABLE TimeOffRequests_Backup AS SELECT * FROM TimeOffRequests;

DROP TABLE TimeOffRequests;

CREATE TABLE TimeOffRequests (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER NOT NULL,
    StartDate TEXT NOT NULL,
    EndDate TEXT NOT NULL,
    Reason TEXT,
    Status INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    FOREIGN KEY (UserId) REFERENCES Users(Id)
);

INSERT INTO TimeOffRequests (Id, UserId, StartDate, EndDate, Reason, Status, CreatedAt)
SELECT Id, UserId, StartDate, EndDate, Reason, Status, CreatedAt
FROM TimeOffRequests_Backup;

DROP TABLE TimeOffRequests_Backup;

-- Backup and recreate SwapRequests
CREATE TABLE SwapRequests_Backup AS SELECT * FROM SwapRequests;

DROP TABLE SwapRequests;

CREATE TABLE SwapRequests (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    FromAssignmentId INTEGER NOT NULL,
    ToUserId INTEGER NOT NULL,
    Status INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL
);

INSERT INTO SwapRequests (Id, FromAssignmentId, ToUserId, Status, CreatedAt)
SELECT Id, FromAssignmentId, ToUserId, Status, CreatedAt
FROM SwapRequests_Backup;

DROP TABLE SwapRequests_Backup;

-- Backup and recreate ShiftAssignments
CREATE TABLE ShiftAssignments_Backup AS SELECT * FROM ShiftAssignments;

DROP TABLE ShiftAssignments;

CREATE TABLE ShiftAssignments (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ShiftInstanceId INTEGER NOT NULL,
    UserId INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    FOREIGN KEY (ShiftInstanceId) REFERENCES ShiftInstances(Id),
    FOREIGN KEY (UserId) REFERENCES Users(Id)
);

INSERT INTO ShiftAssignments (Id, ShiftInstanceId, UserId, CreatedAt)
SELECT Id, ShiftInstanceId, UserId, CreatedAt
FROM ShiftAssignments_Backup;

DROP TABLE ShiftAssignments_Backup;

-- Step 4: Recreate original indexes
CREATE INDEX IX_UserNotifications_UserId_CreatedAt ON UserNotifications(UserId, CreatedAt);
CREATE UNIQUE INDEX IX_ShiftAssignments_ShiftInstanceId_UserId ON ShiftAssignments(ShiftInstanceId, UserId);

-- Step 5: Remove Company metadata columns (Slug, DisplayName, SettingsJson)
-- Backup and recreate Companies table
CREATE TABLE Companies_Backup AS SELECT * FROM Companies;

DROP TABLE Companies;

CREATE TABLE Companies (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL
);

INSERT INTO Companies (Id, Name)
SELECT Id, Name
FROM Companies_Backup;

DROP TABLE Companies_Backup;

COMMIT;

-- Verification queries (run these after the rollback)
-- Check that CompanyId columns are removed:
-- PRAGMA table_info(UserNotifications);
-- PRAGMA table_info(TimeOffRequests);
-- PRAGMA table_info(SwapRequests);
-- PRAGMA table_info(ShiftAssignments);
-- PRAGMA table_info(Companies);