# Trainee Role Implementation - Progress Report

**Status:** Core Backend Complete (Phase 1/3) ✅
**Last Updated:** 2025-10-05
**Build Status:** ✅ Passing

---

## ✅ COMPLETED - Phase 1: Core Backend & Data Model

### 1. Data Model & Database ✅
- [x] Added `Trainee = 4` to `UserRole` enum (`Models/Support/Enums.cs:9`)
- [x] Added 6 new notification types to `NotificationType` enum (`Models/Support/Enums.cs:27-32`)
  - `TraineeShadowingAdded`
  - `TraineeShadowingRemoved`
  - `EmployeeTraineeAdded`
  - `EmployeeTraineeRemoved`
  - `TraineeShadowingCanceledTimeOff`
  - `TraineeShadowingCanceledRoleChange`
- [x] Updated `ShiftAssignment` model with trainee support (`Models/ShiftAssignment.cs:16-17`)
  - Added `TraineeUserId` (nullable int)
  - Added `Trainee` navigation property
- [x] Created and applied migration: `20251004215820_AddTraineeRoleAndShadowing`
- [x] Database updated successfully

###  2. Business Logic & Services ✅
- [x] Created `ITraineeService` interface (`Services/ITraineeService.cs`)
  - `AssignTraineeToShiftAsync()`
  - `RemoveTraineeFromShiftAsync()`
  - `ValidateTraineeAssignmentAsync()`
  - `GetTraineeShadowedShiftsAsync()`
  - `CancelAllShadowingAssignmentsAsync()`
  - `CancelShadowingForTimeOffAsync()`
  - `GetCompanyTraineesAsync()`
- [x] Implemented `TraineeService` class (`Services/TraineeService.cs`)
  - Full validation logic
  - Time conflict checking
  - Automatic notifications for all shadowing events
  - Logging and error handling
- [x] Registered service in DI container (`Program.cs:87`)

### 3. Integration with Existing Features ✅
- [x] **Time Off Approval** (`Pages/Requests/Index.cshtml.cs:92-99`)
  - When trainee's time off is approved, overlapping shadowing assignments are automatically canceled
  - Both trainee and primary employee are notified
- [x] **Role Change Validation** (`Pages/Admin/Users.cshtml.cs:314-362`)
  - **Trainee → Other Role:** Automatically cancels all active shadowing assignments with notifications
  - **Other Role → Trainee:** Blocks if user has active shifts as primary employee or trainees shadowing them
  - Added `Trainee` to assignable roles list

### 4. Validation & Business Rules ✅
- [x] One trainee per shift maximum
- [x] Trainee must have `UserRole.Trainee`
- [x] Trainee must belong to same company as shift
- [x] Cannot assign if no primary employee on shift
- [x] Time conflict detection (trainee can't shadow overlapping shifts)
- [x] Role change safeguards

---

## 🚧 IN PROGRESS - Phase 2: UI & User Experience

### 5. Authorization & Access Control
**Status:** Pending
**Files to update:**
- Need to update authorization policies for Trainee role
- Block Trainees from:
  - `/Requests/Swaps/*` pages
  - `/Assignments/Manage` (manager functions)
  - `/Admin/*` pages
- Allow Trainees to:
  - View `/My/Index` (their shadowed shifts)
  - Create `/Requests/TimeOff/*` requests
  - View `/Calendar/*` views (read-only)

### 6. Assignment Management UI
**Status:** Pending
**Files to update:**
- `Pages/Assignments/Manage.cshtml.cs`
  - Add `ITraineeService` dependency
  - Add handler: `OnPostAssignTrainee(int shiftAssignmentId, int traineeUserId)`
  - Add handler: `OnPostRemoveTrainee(int shiftAssignmentId)`
  - Load list of trainees for company
- `Pages/Assignments/Manage.cshtml`
  - Add "Attach Trainee" button/dropdown next to assigned employees
  - Show trainee badge/icon when trainee is assigned
  - Add "Remove Trainee" action

**UI Flow:**
1. Manager assigns shift to Employee (existing)
2. Click employee name → "Attach Trainee" dropdown appears
3. Select trainee from dropdown → Submit
4. UI updates to show employee + trainee indicator

### 7. Trainee "My Shifts" View
**Status:** Pending
**Files to update:**
- `Pages/My/Index.cshtml.cs`
  - Add logic to load shadowed shifts for trainees:
    ```csharp
    if (currentUser.Role == UserRole.Trainee)
    {
        var shadowed = await _db.ShiftAssignments
            .Include(sa => sa.ShiftInstance).ThenInclude(si => si.ShiftType)
            .Include(sa => sa.User)
            .Where(sa => sa.TraineeUserId == currentUserId && ...)
            .ToListAsync();
    }
    ```
- `Pages/My/Index.cshtml`
  - Display shadowed shifts with visual distinction
  - Show "Shadowing: {Employee Name}"
  - No edit/swap buttons for shadowed shifts

### 8. Employee View (Shifts with Trainees)
**Status:** Pending
**Files to update:**
- `Pages/My/Index.cshtml.cs`
  - Include trainee data when loading employee's shifts:
    ```csharp
    .Include(sa => sa.Trainee)
    ```
- `Pages/My/Index.cshtml`
  - Show trainee badge on shifts where `TraineeUserId != null`
  - Display: "Trainee: {Trainee Name}"

### 9. Calendar Views with Trainee Indicators
**Status:** Pending
**Files to update:**
- `Pages/Calendar/Day.cshtml.cs` / `.cshtml`
- `Pages/Calendar/Week.cshtml.cs` / `.cshtml`
- `Pages/Calendar/Month.cshtml.cs` / `.cshtml`

**Changes needed:**
- Include trainee data in queries:
  ```csharp
  .Include(sa => sa.Trainee)
  ```
- Update staffing count display:
  - Current: "2 / 3 assigned"
  - New: "2 / 3 assigned (+1 trainee)"
- Add visual indicators:
  - Icon/badge for shifts with trainees
  - Dotted border or different color for trainee-shadowed shifts
  - Tooltip showing trainee name

### 10. Localization Strings
**Status:** Pending
**Files to update:**
- `Resources/SharedResources.resx` (English)
- `Resources/SharedResources.he.resx` (Hebrew)

**New strings needed:**
```
Trainee
TraineeRole
ShadowingShift
AttachTrainee
RemoveTrainee
ShadowingEmployee
TraineeShadowingAdded
TraineeShadowingRemoved
... (all notification types)
```

---

## 📋 TODO - Phase 3: Testing & Polish

### 11. Testing
- [ ] **Unit Tests**
  - TraineeService validation logic
  - Time conflict detection
  - Role change scenarios
- [ ] **Integration Tests**
  - Assign trainee to shift (happy path)
  - Prevent assigning second trainee to same shift
  - Time off approval cancels shadowing
  - Role change cancels shadowing
  - Cross-company assignment blocked
- [ ] **Manual UI Testing**
  - Manager can assign/remove trainee via UI
  - Trainee sees shadowed shifts in My/Index
  - Employee sees trainee indicator
  - Calendar views show trainee correctly
  - Notifications work correctly

### 12. Optional Enhancements
- [ ] **Audit Logging** (`Models/ShadowingAssignmentAudit.cs`)
  - Track who assigned trainee, when, and why removed
  - Create separate audit table
  - Migration for audit table
- [ ] **Reporting & Analytics**
  - Trainee hours tracking
  - Separate from employee hours in reports
- [ ] **Advanced Features** (Future)
  - Trainee progress tracking
  - Certification/completion tracking
  - Multi-trainee support (if business rules change)

---

## 🔧 How to Continue Implementation

### Next Steps (Recommended Order):

1. **Update Authorization** (Quick - 15 min)
   - Update authorization policies to handle Trainee role
   - Test that Trainees can't access restricted pages

2. **Assignments UI** (Medium - 1-2 hours)
   - Update `Assignments/Manage` page model and view
   - Add trainee assignment handlers
   - Test assigning/removing trainees

3. **My/Index Views** (Medium - 1 hour)
   - Update page models to load trainee/shadowing data
   - Update views to display trainee indicators
   - Test from both trainee and employee perspectives

4. **Calendar Views** (Medium - 1-2 hours)
   - Update all 3 calendar views (Day/Week/Month)
   - Add trainee indicators and visual distinctions
   - Test trainee count display

5. **Localization** (Quick - 30 min)
   - Add all new strings to resource files
   - Test in both English and Hebrew

6. **Testing** (Medium - 2-3 hours)
   - Write unit/integration tests
   - Manual end-to-end testing
   - Fix any bugs discovered

---

## 📊 Implementation Statistics

- **Files Modified:** 9
- **Files Created:** 3
- **Lines of Code Added:** ~800
- **Database Migrations:** 1
- **New Services:** 1
- **New Notification Types:** 6
- **Build Status:** ✅ Passing (0 errors, 2 warnings)

---

## 🚀 Deployment Checklist (When Ready)

- [ ] All tests passing
- [ ] Database migration reviewed
- [ ] Localization complete (EN + HE)
- [ ] User documentation updated
- [ ] Rollout plan reviewed (per `TRAINEE_ROLE_DESIGN.md` Section 9.2)
- [ ] Backup database before deployment
- [ ] Run migration in production
- [ ] Monitor logs for errors
- [ ] Verify trainee assignment works in production

---

## 📝 Notes & Decisions

1. **TraineeUserId is nullable:** This allows shifts to exist without trainees. The FK is set to `ON DELETE SET NULL`.

2. **One trainee per shift:** Business rule enforced at application layer. If this changes, the database schema supports multiple trainees (would need to create a junction table).

3. **Role change behavior:** When changing FROM Trainee role, all shadowing assignments are automatically canceled with notifications. When changing TO Trainee role, validation ensures user has no active shifts.

4. **Time conflict logic:** Uses ShiftType Start/End times and ShiftInstance WorkDate to detect overlaps. Does not handle overnight shifts specially (assumes shift wrapping handled elsewhere).

5. **Notifications:** All 6 trainee-related notification types are sent automatically by TraineeService methods. No manual notification sending needed in UI code.

---

**For questions or issues, refer to:**
- Full design: `TRAINEE_ROLE_DESIGN.md`
- This progress report: `TRAINEE_ROLE_IMPLEMENTATION_PROGRESS.md`
