# Trainee Role - Technical Design Document

## Goal

Design a Trainee role that allows shadowing of a scheduled employee on a specific shift inside a company. The trainee:

- is attached to a specific employee's shift (shadowing),
- is not counted toward staffing/coverage,
- appears on both users' schedules and notifications,
- has limited self-service actions (time off request only).

## Scope & Non-Goals

**In scope:** role model, permissions, assignment rules, UX flows, data/schema, notifications, auditing, analytics, migration/rollout.

**Out of scope:** any business process for selecting which trainee shadows whom; managers handle this offline.

## Entities & Definitions

- **Company:** container for employees, roles, shifts.
- **Employee:** standard worker who can be assigned to shifts.
- **Trainee (new role):** a user under a company who can shadow an employee's shift.
- **Shadowing Assignment:** relation linking {shift, primary employee, trainee}.
- **Manager / Director / Owner:** elevated roles.

## Core Rules (from requirements)

- **Who can assign a trainee:** Only Manager, Director, Owner
- **When assignment is allowed:** Only if a shift already has an employee assigned; the trainee is attached by clicking that employee inside shift assignment UI.
- **Staffing count:** Trainee does not increase assigned/coverage counts.
- **Trainee capabilities:**
  - Cannot switch shifts
  - Can request time off. If approved overlaps shadowing, the shadowing is canceled for the overlapping time and both parties are notified
- **Visibility:**
  - Trainee sees shadowed shifts in My Shifts and Notifications, same as regular shifts plus a "Shift with: {Employee Name}" tag
  - The primary Employee also sees that a trainee is attached to their shifts and receives notifications on add/remove/changes
- **One trainee per shift maximum**

---

## 1. Data Model & Schema

### 1.1 Enum Update

```csharp
public enum UserRole
{
    Owner = 0,
    Manager = 1,
    Employee = 2,
    Director = 3,
    Trainee = 4  // NEW
}
```

### 1.2 ShiftAssignment Enhancement

Add nullable `TraineeUserId` column to existing `ShiftAssignment` table:

```csharp
public class ShiftAssignment
{
    // ... existing fields ...
    public int? TraineeUserId { get; set; }  // NEW - nullable FK to AppUser
    public AppUser? Trainee { get; set; }     // NEW - navigation property
}
```

**Rationale:** Single trainee per shift, simpler than separate table, natural extension of existing model.

### 1.3 Database Constraints

- Foreign key: `TraineeUserId` → `AppUser.Id`
- Check constraint: Trainee user must have `UserRole.Trainee`
- Index on `TraineeUserId` for query performance
- Cascade behavior: Set NULL on trainee user deletion

---

## 2. Permissions & Authorization

### 2.1 Trainee Role Permissions Matrix

| Action | Owner | Director | Manager | Employee | Trainee |
|--------|-------|----------|---------|----------|---------|
| View own shadowed shifts | N/A | N/A | N/A | N/A | ✅ |
| Request time off | ✅ | ✅ | ✅ | ✅ | ✅ |
| Request shift swap | ✅ | ✅ | ✅ | ✅ | ❌ |
| Create/edit shifts | ✅ | ✅ | ✅ | ❌ | ❌ |
| Assign trainee to shift | ✅ | ✅ | ✅ | ❌ | ❌ |
| Approve requests | ✅ | ✅ | ✅ | ❌ | ❌ |
| Manage users | ✅ | ✅ | ❌ | ❌ | ❌ |

### 2.2 Authorization Rules

**TraineeUserId Assignment Rules:**
1. Only Manager/Director/Owner can set `TraineeUserId`
2. `TraineeUserId` can only reference users with `UserRole.Trainee`
3. `TraineeUserId` can only be set if `UserId` is already assigned (primary employee exists)
4. One trainee per shift maximum (enforced at application layer)

**Page Access:**
- `/My/Index` - ✅ Shows shadowed shifts only
- `/My/Requests` - ✅ Time off requests only
- `/Requests/Swaps/*` - ❌ Blocked
- `/Assignments/Manage` - ❌ Blocked
- `/Calendar/*` - ✅ Read-only, shows only shadowed shifts
- `/Admin/*` - ❌ Blocked
- `/Director/*` - ❌ Blocked

---

## 3. UX Design

### 3.1 Manager Workflow: Assign Trainee

**Entry Point:** `Assignments/Manage` page

**Flow:**
1. Manager assigns shift to Employee (existing flow)
2. In shift assignment row, employee name becomes clickable/has action button
3. Click employee → dropdown/modal: "Attach Trainee to this shift"
4. Dropdown shows all users with `UserRole.Trainee` in company
5. Select trainee → Confirm
6. UI updates to show: `{Employee Name} + {Trainee Name}` with visual indicator (icon/badge)

**Visual Indicators:**
- Icon next to employee name: 👤+ or [T] badge
- Tooltip on hover: "Primary: {Employee}, Trainee: {Trainee}"
- Click trainee badge → Remove trainee option

### 3.2 Trainee View: My Shifts

**`/My/Index` page:**
- Shows all shifts where `TraineeUserId == CurrentUser.Id`
- Each shift displays:
  ```
  [Shift Date/Time]
  Shadowing: {Primary Employee Name}
  {Shift Type} - {Duration}
  ```
- Visual distinction: different background color or border style
- No edit/swap buttons (read-only)

### 3.3 Employee View: My Shifts with Trainee

**`/My/Index` page:**
- Existing shifts show normally
- If shift has trainee (`TraineeUserId != null`):
  ```
  [Shift Date/Time]
  {Shift Type} - {Duration}
  Trainee: {Trainee Name}
  ```
- Badge/icon indicator

### 3.4 Calendar Views

**Day/Week/Month calendars:**
- Trainee shifts shown with visual distinction (dotted border, different color, icon)
- Staffing count displays: "2 assigned (1 trainee)" or "2 / 3 (+ 1 trainee)"
- Click shift → shows both primary and trainee names

---

## 4. Notifications & Events

### 4.1 New Notification Types

Add to `NotificationType` enum:
```csharp
TraineeShadowingAdded = 6,      // "You are shadowing {Employee} on {Date}"
TraineeShadowingRemoved = 7,    // "Shadowing assignment removed for {Date}"
EmployeeTraineeAdded = 8,       // "{Trainee} will shadow your shift on {Date}"
EmployeeTraineeRemoved = 9,     // "{Trainee} no longer shadowing your shift on {Date}"
TraineeShadowingCanceledTimeOff = 10,  // "Shadowing canceled due to approved time off"
TraineeShadowingCanceledRoleChange = 11  // "Your shadowing assignments were canceled due to role change"
```

### 4.2 Notification Events

| Event | Recipient(s) | Notification Type | Message |
|-------|--------------|-------------------|---------|
| Trainee assigned to shift | Trainee | TraineeShadowingAdded | "You are now shadowing {Employee} on {ShiftDate}" |
| Trainee assigned to shift | Employee | EmployeeTraineeAdded | "{Trainee} will shadow your shift on {ShiftDate}" |
| Trainee removed from shift | Trainee | TraineeShadowingRemoved | "Your shadowing assignment for {ShiftDate} has been removed" |
| Trainee removed from shift | Employee | EmployeeTraineeRemoved | "{Trainee} is no longer shadowing your shift on {ShiftDate}" |
| Trainee time off approved → overlap | Trainee | TraineeShadowingCanceledTimeOff | "Your shadowing on {ShiftDate} was canceled due to approved time off" |
| Trainee time off approved → overlap | Employee | EmployeeTraineeRemoved | "{Trainee}'s shadowing was canceled due to approved time off" |
| Employee shift deleted | Trainee | TraineeShadowingRemoved | "Shadowing assignment canceled (shift deleted)" |
| Employee removed from shift | Trainee | TraineeShadowingRemoved | "Shadowing assignment canceled (employee removed)" |
| Trainee role changed | Trainee | TraineeShadowingCanceledRoleChange | "Your shadowing assignments were canceled due to role change" |
| Trainee role changed | Employee | EmployeeTraineeRemoved | "{Trainee}'s shadowing was canceled due to role change" |

---

## 5. Business Logic & Validation

### 5.1 Assignment Validation

```csharp
// When assigning trainee to shift
- Shift must exist
- Shift must have primary employee (UserId != null)
- Trainee must be UserRole.Trainee
- Trainee must belong to same company
- Trainee cannot already be on this shift as primary
- Shift cannot already have a trainee (max 1)
- No time conflict for trainee (can't shadow overlapping shifts)
```

### 5.2 Time Off Request Handling

**When trainee submits time off request:**
- Standard validation applies

**When trainee's time off is APPROVED:**
1. Find all `ShiftAssignment` where `TraineeUserId == Trainee.Id` AND shift time overlaps approved time off
2. For each overlapping assignment:
   - Set `TraineeUserId = null`
   - Send notifications to both trainee and employee
   - Create audit log entry
3. Complete approval

### 5.3 Cascade Delete/Update Rules

**If Employee (UserId) is removed from shift:**
- Automatically set `TraineeUserId = null`
- Notify trainee

**If Shift is deleted:**
- Standard cascade deletes `ShiftAssignment`
- Notify both employee and trainee before deletion

**If Trainee user is deleted:**
- FK constraint set NULL on `ShiftAssignment.TraineeUserId`
- No notification needed (user deleted)

**If Trainee role is changed to any other role (Employee, Manager, etc.):**
1. Find all `ShiftAssignment` where `TraineeUserId == ChangedUser.Id`
2. For each shadowing assignment:
   - Set `TraineeUserId = null`
   - Send notification to the primary employee
   - Send notification to the (former) trainee
   - Create audit log entry with reason: "RoleChanged"
3. Complete role change

---

## 6. Edge Cases & Conflict Resolution

### 6.1 Scheduling Conflicts

**Q: Can trainee shadow multiple non-overlapping shifts?**
A: Yes, but validate no time overlap.

**Q: Can trainee shadow shifts at different companies (if Director)?**
A: No, trainee role is company-specific. If user has trainee role at Company A, they can only shadow shifts at Company A.

**Implementation:** Add validation in assignment logic:
```csharp
if (trainee.CompanyId != shift.CompanyId) throw ValidationException
```

### 6.2 Role Changes

**Scenario: Trainee promoted to Employee/Manager/Other**

**Before role change:**
1. Query all active shadowing assignments (`TraineeUserId == user.Id` AND shift date >= today)
2. If any exist, show confirmation dialog:
   - "This user is currently shadowing X shifts. Changing their role will cancel all shadowing assignments. Continue?"
3. If confirmed:
   - Remove from all shadowing assignments (`TraineeUserId = null`)
   - Notify affected primary employees
   - Notify the user being changed
   - Create audit entries
4. Complete role change

**After role change:**
- User can no longer be assigned as trainee
- Historical shadowing data remains in audit logs

**Scenario: Employee demoted to Trainee**
- Check if they have active shifts as primary employee
- If yes, block demotion with error: "Cannot change to Trainee role while assigned to shifts. Please reassign or remove from shifts first."
- If they have trainees shadowing them, those assignments must be removed first

### 6.3 Company/Tenant Isolation

- All queries must filter by `CompanyId` (existing tenant resolver)
- Trainee dropdown only shows trainees from current company
- Notifications scoped to company

### 6.4 Partial Time Off Overlap

**Example:**
- Trainee shadowing shifts on Mon, Tue, Wed
- Time off approved for Tue only
- Result: Cancel Tue shadowing only, keep Mon & Wed

**Implementation:** Precise datetime overlap calculation in time off approval logic.

---

## 7. Staffing & Analytics

### 7.1 Staffing Count Display

**Current:** "2 / 3 assigned"

**With Trainees:** "2 / 3 assigned (+1 trainee)"

- Trainees **never** count toward numerator or denominator
- Visual indicator separates trainee count
- Color coding: Green if met without trainees, Yellow if met only with trainees (but this shouldn't happen since trainees don't count)

### 7.2 Reporting & Analytics

**Hour Tracking:**
- Log trainee hours separately (add `IsTrainee` flag or check role)
- Reports should show: "Employee Hours: X, Trainee Hours: Y"
- Labor cost calculations: Exclude trainee hours OR apply different rate

**Audit Trail:**
- Track who assigned trainee to shift
- Track when/why trainee removed (manual, time off, shift deleted)

---

## 8. Security & Audit

### 8.1 Authorization Checks

Every controller action must check:
```csharp
// Example for assignment endpoint
if (currentUser.Role != UserRole.Manager &&
    currentUser.Role != UserRole.Director &&
    currentUser.Role != UserRole.Owner)
{
    return Forbid();
}
```

### 8.2 Audit Logging

Extend `RoleAssignmentAudit` or create new audit type:

```csharp
public class ShadowingAssignmentAudit
{
    public int Id { get; set; }
    public int ShiftAssignmentId { get; set; }
    public int TraineeUserId { get; set; }
    public int AssignedByUserId { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? RemovedAt { get; set; }
    public string? RemovalReason { get; set; } // "Manual", "TimeOff", "ShiftDeleted", "RoleChanged", etc.
}
```

---

## 9. Migration & Rollout

### 9.1 Database Migration

**Migration Steps:**
1. Add `Trainee = 4` to enum (code change)
2. Add column: `ALTER TABLE ShiftAssignment ADD TraineeUserId INT NULL`
3. Add FK constraint: `FOREIGN KEY (TraineeUserId) REFERENCES AppUser(Id) ON DELETE SET NULL`
4. Add index: `CREATE INDEX IX_ShiftAssignment_TraineeUserId ON ShiftAssignment(TraineeUserId)`
5. Existing data: All `TraineeUserId` values default to NULL (no trainees assigned)

**Migration Class:**
```csharp
public class AddTraineeRole : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "TraineeUserId",
            table: "ShiftAssignment",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_ShiftAssignment_TraineeUserId",
            table: "ShiftAssignment",
            column: "TraineeUserId");

        migrationBuilder.AddForeignKey(
            name: "FK_ShiftAssignment_AppUser_TraineeUserId",
            table: "ShiftAssignment",
            column: "TraineeUserId",
            principalTable: "AppUser",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }
}
```

### 9.2 Rollout Plan

**Phase 1: Data Model (No User Impact)**
- Add enum value
- Run database migration
- Deploy changes
- Validate no errors

**Phase 2: Backend Logic (Feature Flagged)**
- Add validation logic
- Add notification types
- Add authorization checks
- Deploy behind feature flag

**Phase 3: UI (Gradual Rollout)**
- Add trainee assignment UI (Assignments/Manage)
- Add trainee view (My/Index)
- Add visual indicators (calendars)
- Enable feature flag for pilot companies

**Phase 4: Full Launch**
- Monitor metrics (assignment count, errors)
- Enable for all companies
- Update documentation

---

## 10. Testing Strategy

### 10.1 Unit Tests
- `ShiftAssignment` model validation
- Trainee assignment business logic
- Time off overlap calculation
- Permission checks
- Role change cancellation logic

### 10.2 Integration Tests
- Assign trainee to shift (happy path)
- Attempt assign without primary employee (should fail)
- Attempt assign second trainee to shift (should fail)
- Time off approval cancels shadowing
- Employee removal cascades to trainee
- Cross-company assignment blocked
- Role change cancels all shadowing

### 10.3 UI Tests
- Manager can assign trainee via UI
- Trainee sees shadowed shifts in My/Index
- Employee sees trainee badge on shifts
- Calendar views show trainee indicator
- Notifications delivered correctly
- Role change confirmation dialog

---

## Implementation Steps

### Step 1: Data Model & Migration
1. Update `Models/Support/Enums.cs` - Add `Trainee = 4`
2. Update `Models/ShiftAssignment.cs` - Add `TraineeUserId` and navigation property
3. Create migration: `dotnet ef migrations add AddTraineeRole`
4. Update `AppDbContext` if needed for constraints
5. Review and test migration

### Step 2: Business Logic & Validation
6. Create `Services/ITraineeService.cs` interface
7. Implement `Services/TraineeService.cs`:
   - `AssignTraineeToShift(shiftAssignmentId, traineeUserId, assignedByUserId)`
   - `RemoveTraineeFromShift(shiftAssignmentId, reason)`
   - `ValidateTraineeAssignment(shiftAssignmentId, traineeUserId)`
   - `GetTraineeShadowedShifts(traineeUserId, dateRange)`
   - `CancelAllShadowingAssignments(userId, reason, changedByUserId)`
8. Update `Services/NotificationService.cs` - Add new notification types
9. Update time off approval logic to detect and cancel overlapping shadowing
10. Update `Pages/Admin/Users.cshtml.cs` (role assignment logic):
    - Add pre-change validation when changing FROM Trainee role
    - Add `CancelAllShadowingAssignments(userId, reason)` method call
    - Add confirmation dialog in UI

### Step 3: Authorization & Permissions
11. Update authorization middleware/filters to handle Trainee role
12. Add authorization checks in all relevant controllers:
    - Block trainees from swap requests
    - Block trainees from assignment management
    - Allow trainees to view own shifts and request time off

### Step 4: UI - Assignment Interface
13. Update `Pages/Assignments/Manage.cshtml.cs`:
    - Add handler for assigning trainee
    - Add handler for removing trainee
14. Update `Pages/Assignments/Manage.cshtml`:
    - Add UI for "Attach Trainee" button on assigned employees
    - Add dropdown with company trainees
    - Add visual indicator when trainee attached
    - Add "Remove Trainee" option

### Step 5: UI - Trainee Views
15. Update `Pages/My/Index.cshtml.cs`:
    - Modify query to include shadowed shifts for trainees
16. Update `Pages/My/Index.cshtml`:
    - Add visual distinction for shadowed shifts
    - Show "Shadowing: {Employee Name}"

### Step 6: UI - Employee Views
17. Update `Pages/My/Index.cshtml`:
    - Show trainee badge on shifts where current user is primary employee

### Step 7: UI - Calendar Views
18. Update `Pages/Calendar/Day.cshtml`, `Week.cshtml`, `Month.cshtml`:
    - Add trainee indicator on shifts
    - Update staffing count display format
    - Show trainee info in shift details

### Step 8: Notifications
19. Update notification creation logic for all shadowing events
20. Add localization strings for new notification types
21. Test notification delivery

### Step 9: Audit & Logging
22. Create `Models/ShadowingAssignmentAudit.cs`
23. Add audit logging to TraineeService methods
24. Add migration for audit table

### Step 10: Testing
25. Write unit tests for TraineeService
26. Write integration tests for assignment flows
27. Write tests for time off overlap logic
28. Write tests for role change cancellation
29. Manual UI testing

### Step 11: Documentation & Rollout
30. Update user documentation
31. Create migration guide for existing users
32. Deploy to staging
33. Pilot with test company
34. Deploy to production

---

**Total Estimated Steps: 34 distinct implementation tasks**
