# Autonomous Testing Strategy & Defect Analysis
## ShiftManager Application

**Generated**: 2025-10-04
**Branch**: autonomous-testing-20251004
**Status**: Testing Infrastructure Setup Phase

---

## 1. Executive Summary

This document outlines the comprehensive testing strategy for the ShiftManager application following the recent implementation of company selection for shift creation and RBAC hardening. The application currently has **zero automated tests**, presenting significant quality assurance risk.

### Key Findings from Context Analysis

**Application Profile:**
- **Technology**: ASP.NET Core 8.0 Razor Pages, SQLite, EF Core 9.0
- **Domain**: Multi-tenant shift scheduling with role-based access control
- **Users**: Owner, Director, Manager, Employee roles
- **Recent Changes**: Company selection (commits 3b37915-451c122), RBAC hardening, Hebrew localization

**Critical Gaps Identified:**
1. ❌ No automated test suite exists
2. ❌ No CI/CD testing pipeline
3. ❌ Manual testing only for recent complex features
4. ⚠️ High-risk multi-tenancy with cross-company access
5. ⚠️ Complex authorization logic without regression protection
6. ⚠️ Concurrency handling recently patched (may have edge cases)

---

## 2. System Architecture Analysis

### 2.1 Core Domain Models

```
Company (Multi-tenant root)
├── AppUser (Employee, Manager, Owner)
├── ShiftType (Morning, Noon, Night, Middle)
├── ShiftInstance (Specific date + shift type + required staffing)
├── ShiftAssignment (User assigned to instance)
├── TimeOffRequest (Pending/Approved/Declined)
├── SwapRequest (User wants to swap shifts)
└── AppConfig (RestHours, WeeklyHoursCap)

DirectorCompany (Many-to-Many: Directors ↔ Companies)
RoleAssignmentAudit (Audit trail for role changes)
UserJoinRequest (Company signup requests)
```

### 2.2 Critical Business Rules

**Multi-Tenancy:**
- Global query filters scope all queries to user's company (via CompanyIdInterceptor)
- Directors can access multiple companies via DirectorCompany mappings
- Owners have unrestricted access (IgnoreQueryFilters)
- ⚠️ **Risk**: Tenant isolation breach could expose cross-company data

**Authorization Hierarchy:**
```
Owner (Full system access)
  ├── Can assign any role
  ├── Access all companies
  └── Bypass global query filters

Director (Cross-company manager)
  ├── Can assign Employee, Manager, Director (NOT Owner)
  ├── Access only assigned companies
  └── Use "View as Manager" mode

Manager (Single company manager)
  ├── Can assign Employee only
  ├── Access only own company
  └── Cannot change company assignments

Employee (Worker)
  └── View own schedule, request time off/swaps
```

**Shift Management:**
- Shift creation requires company selection (if user has access to multiple)
- Shift assignment validates user belongs to shift's company
- Concurrency control prevents conflicting updates
- Staff adjustment (±) requires current concurrency value (unless payload = 0)

### 2.3 Recent Implementation: Company Selection

**Files Modified (commits 3b37915 → 451c122):**
- `Pages/Calendar/Day.cshtml.cs` - Modal shift creation with company dropdown
- `Pages/Calendar/Week.cshtml.cs` - Same pattern as Day view
- `Pages/Calendar/Month.cshtml.cs` - Same pattern as Day view
- `wwwroot/js/site.js` - JavaScript modal with company filtering
- `Pages/Assignments/Manage.cshtml.cs` - Removed company selection, enforce shift's company

**Known Issues Fixed:**
1. ✅ Duplicate shift types in Day view (displayTypes vs types separation)
2. ✅ "Add Shift" button not opening modal in Day view
3. ✅ Concurrency error when payload.concurrency = 0
4. ✅ Unexpected redirect from Assignments page

**Potential Risks:**
- ⚠️ Week/Month views may have same issues as Day view (need verification)
- ⚠️ Server-side validation of company access not tested
- ⚠️ Cross-company assignment prevention not verified
- ⚠️ Concurrency logic change (skip if 0) may introduce race conditions
- ⚠️ JavaScript filtering logic not unit tested

---

## 3. Test Strategy

### 3.1 Testing Pyramid

```
         /\
        /  \  E2E Tests (10%)
       /----\   - Critical user flows
      / Unit \  - Cross-company scenarios
     /  Tests \ Integration Tests (30%)
    /   (60%)  \ - Page model handlers
   /------------\ - Authorization checks
  ----------------
   Manual Exploratory
```

### 3.2 Test Phases

#### Phase 1: Unit Tests ✅ (Current Phase)
**Scope**: Isolated component testing
**Tools**: xUnit, Moq, InMemory SQLite
**Coverage Target**: 60%+

**Test Suites:**
1. **DirectorService Tests**
   - ✅ IsDirector() returns true for Owner/Director, false for Manager/Employee
   - ✅ IsDirectorOfAsync() validates company access correctly
   - ✅ GetDirectorCompanyIdsAsync() returns correct mappings
   - ✅ CanAssignRole() enforces hierarchy (Director cannot assign Owner)
   - ✅ Owner bypasses DirectorCompanies lookup

2. **Authorization Tests**
   - ✅ IsManagerOrAdmin policy includes Owner, Manager, Director
   - ✅ IsAdmin policy only includes Owner
   - ✅ IsDirector policy includes Owner, Director
   - ✅ Unauthorized users get 403/redirect

3. **Company Selection Tests**
   - ✅ Owner sees all companies in dropdown
   - ✅ Director sees only assigned companies
   - ✅ Manager sees no dropdown (single company)
   - ✅ Server rejects unauthorized company assignments
   - ✅ Shift types filtered by selected company

4. **Concurrency Tests**
   - ✅ Adjust with matching concurrency succeeds
   - ✅ Adjust with mismatched concurrency fails
   - ✅ Adjust with concurrency=0 skips check
   - ✅ Modal create with concurrency=0 succeeds

5. **Multi-Tenancy Tests**
   - ✅ Global query filter scopes to user's company
   - ✅ IgnoreQueryFilters bypasses scoping
   - ✅ Cross-company assignment blocked
   - ✅ Director access validated per company

#### Phase 2: Integration Tests
**Scope**: Multi-component interactions
**Tools**: WebApplicationFactory, TestServer

**Test Suites:**
1. **Calendar Flow Tests**
   - Day/Week/Month views load correct shift types
   - Modal company dropdown shows accessible companies
   - Shift creation assigns to correct company
   - +/- buttons update staffing with concurrency

2. **Assignment Flow Tests**
   - Manage page loads users from shift's company only
   - Cross-company user assignment rejected
   - Access denied for unauthorized company shifts
   - Successful assignment updates database

3. **RBAC Flow Tests**
   - User creation with role assignment audit
   - Role change creates audit record
   - Director assignment grants company access
   - "View as Manager" mode scopes correctly

#### Phase 3: Edge Case & Security Tests
**Scope**: Boundary conditions and attack vectors

**Test Scenarios:**
1. **Privilege Escalation Attempts**
   - Director tries to assign Owner role → Blocked
   - Manager tries to create shift in other company → Blocked
   - Employee tries to access Admin pages → 403

2. **Data Isolation Breaches**
   - User A queries Company B data directly → Empty result
   - Director without access queries Company C → Empty result
   - SQL injection attempts in company filters → Sanitized

3. **Concurrency Race Conditions**
   - Two users adjust same shift simultaneously → One fails
   - Create + Adjust in rapid succession → Correct final state
   - Stale concurrency value causes rejection → Error message

4. **Input Validation**
   - Negative staffing requirements → Rejected
   - Invalid company ID in payload → 400 Bad Request
   - Missing required fields → Validation errors

#### Phase 4: Non-Functional Tests
**Scope**: Performance, accessibility, localization

**Test Scenarios:**
1. **Performance**
   - Calendar views load in < 500ms (100 shifts)
   - Company dropdown renders in < 100ms (50 companies)
   - Database query count (N+1 detection)

2. **Accessibility**
   - ARIA labels present on all interactive elements
   - Keyboard navigation works in modal
   - Screen reader compatibility

3. **Localization**
   - Hebrew mode shows no English text (excluding DB data)
   - RTL layout correct in all views
   - Date formatting matches locale

---

## 4. Defect & Risk Analysis

### 4.1 Critical Risks (P0 - Must Fix)

#### RISK-001: Cross-Company Data Leak via IgnoreQueryFilters
**Severity**: 🔴 CRITICAL
**Component**: `Pages/Calendar/Day.cshtml.cs:55-60` (and Week, Month)
**Description**: When loading shift types for dropdown, code uses `IgnoreQueryFilters()` for all roles. If authorization check fails or is bypassed, users could see shift types from unauthorized companies.

**Vulnerable Code:**
```csharp
types = await _db.ShiftTypes.IgnoreQueryFilters()
    .Where(st => accessibleCompanyIds.Contains(st.CompanyId))
    .ToListAsync();
```

**Attack Vector**:
1. Manipulate `accessibleCompanyIds` list via debug/reflection
2. Bypass `IsDirectorOfAsync()` check with timing attack
3. Directly call handler with forged company IDs

**Recommendation**: Add server-side validation AFTER query to verify each returned type belongs to accessible company.

**Test Case**:
```csharp
[Fact]
public async Task Manager_CannotSeeOtherCompanyShiftTypes()
{
    // Manager of Company A attempts to load Company B shift types
    // Expected: Empty list or only Company A types
}
```

---

#### RISK-002: Concurrency Check Bypass Allows Race Conditions
**Severity**: 🔴 CRITICAL
**Component**: `Pages/Calendar/Day.cshtml.cs:182-186`
**Description**: Recent fix skips concurrency check when `payload.concurrency == 0`. Malicious user could always send 0 to bypass optimistic locking, causing lost updates.

**Vulnerable Code:**
```csharp
if (payload.concurrency != 0 && inst.Concurrency != payload.concurrency)
{
    return BadRequest(new { message = "Concurrent update detected. Reload the page." });
}
```

**Attack Vector**:
1. User A loads shift (concurrency = 5)
2. User B adjusts shift (concurrency = 5 → 6)
3. User A sends adjustment with `concurrency: 0` (bypass)
4. User A's update overwrites User B's change → Lost update

**Recommendation**:
- Only skip concurrency check for NEW instances (`inst.Id == 0`)
- Or distinguish between "create" vs "adjust" operations explicitly
- Never allow `concurrency: 0` for existing instances

**Test Case:**
```csharp
[Fact]
public async Task ConcurrentAdjustWithZero_ShouldFail_ForExistingInstance()
{
    // Create instance with concurrency = 1
    // User sends adjust with concurrency = 0
    // Expected: BadRequest (must provide valid concurrency)
}
```

---

#### RISK-003: Missing Server-Side Validation of Company Access
**Severity**: 🟠 HIGH
**Component**: `Pages/Calendar/Day.cshtml.cs:168-180` (OnPostAdjustAsync)
**Description**: Server validates company access only if `payload.companyId.HasValue`. If client omits companyId, shift is created in targetCompanyId without validation.

**Vulnerable Code:**
```csharp
if (payload.companyId.HasValue)
{
    targetCompanyId = payload.companyId.Value;
    bool hasAccess = false;
    // ... validation ...
}
// Uses targetCompanyId even if validation was skipped
```

**Attack Vector**:
1. Manager sends request without `companyId` field
2. Code defaults to shift type's company
3. If shift type is from unauthorized company, access granted

**Recommendation**: Always validate final `targetCompanyId` against user's access, regardless of payload structure.

**Test Case:**
```csharp
[Fact]
public async Task AdjustWithoutCompanyId_StillEnforcesAccess()
{
    // Manager tries to adjust shift in unauthorized company
    // Payload omits companyId field
    // Expected: 403 Forbidden
}
```

---

### 4.2 High Priority Issues (P1 - Should Fix)

#### ISSUE-001: No Test Coverage for Recent Features
**Severity**: 🟠 HIGH
**Impact**: Regression risk for company selection, RBAC, concurrency
**Recommendation**: Create test suite (this document's purpose)

#### ISSUE-002: Duplicate Code Across Day/Week/Month Views
**Severity**: 🟡 MEDIUM
**Component**: `Pages/Calendar/Day.cshtml.cs`, `Week.cshtml.cs`, `Month.cshtml.cs`
**Description**: Same company selection logic duplicated 3 times
**Recommendation**: Extract to shared service or base class
**Benefit**: Single source of truth, easier testing

#### ISSUE-003: JavaScript Modal Logic Not Unit Tested
**Severity**: 🟡 MEDIUM
**Component**: `wwwroot/js/site.js` (openShiftModal, filterShiftTypesByCompany)
**Description**: Complex client-side filtering logic with no tests
**Recommendation**: Add Jest/Jasmine tests for JavaScript functions

---

### 4.3 Medium Priority Issues (P2 - Nice to Have)

#### ISSUE-004: Missing Input Validation
**Severity**: 🟡 MEDIUM
**Component**: Various page handlers
**Examples**:
- Negative staffing values not explicitly rejected
- Future dates not validated (e.g., shift 10 years ahead)
- SQL-like strings not sanitized (EF handles, but defense in depth)

#### ISSUE-005: No Performance Benchmarks
**Severity**: 🟢 LOW
**Description**: Calendar views with 100+ shifts may have performance issues
**Recommendation**: Add benchmark tests, establish SLA (< 500ms)

---

## 5. Test Implementation Plan

### 5.1 Test Project Setup

**Project Structure:**
```
ShiftManager.Tests/
├── ShiftManager.Tests.csproj
├── UnitTests/
│   ├── Services/
│   │   ├── DirectorServiceTests.cs
│   │   ├── CompanyFilterServiceTests.cs
│   │   └── LocalizationServiceTests.cs
│   ├── Authorization/
│   │   ├── AuthorizationPolicyTests.cs
│   │   └── RoleAssignmentTests.cs
│   └── Models/
│       ├── PasswordHasherTests.cs
│       └── ConflictCheckerTests.cs
├── IntegrationTests/
│   ├── Calendar/
│   │   ├── DayViewTests.cs
│   │   ├── WeekViewTests.cs
│   │   └── MonthViewTests.cs
│   ├── Assignments/
│   │   └── ManageAssignmentsTests.cs
│   └── Admin/
│       ├── UserManagementTests.cs
│       └── DirectorManagementTests.cs
├── EdgeCaseTests/
│   ├── SecurityTests.cs
│   ├── ConcurrencyTests.cs
│   └── MultiTenancyTests.cs
└── Fixtures/
    ├── TestWebApplicationFactory.cs
    ├── DatabaseFixture.cs
    └── TestDataSeeder.cs
```

**Dependencies:**
```xml
<PackageReference Include="xUnit" Version="2.9.0" />
<PackageReference Include="xUnit.runner.visualstudio" Version="2.8.2" />
<PackageReference Include="Moq" Version="4.20.70" />
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="9.0.0" />
<PackageReference Include="FluentAssertions" Version="6.12.0" />
```

### 5.2 Execution Order

1. ✅ **Phase 3 (Current)**: Create test project infrastructure
2. **Phase 4**: Implement DirectorService unit tests (highest risk)
3. **Phase 5**: Implement Authorization unit tests
4. **Phase 6**: Implement Calendar integration tests (company selection)
5. **Phase 7**: Implement Security/Concurrency edge case tests
6. **Phase 8**: Analyze coverage gaps, create defect report
7. **Phase 9**: Remediate P0 defects with TDD

### 5.3 Success Criteria

**Phase Completion:**
- ✅ Test project builds and runs
- ✅ At least 30 tests passing (Unit + Integration)
- ✅ All P0 defects have failing tests
- ✅ Coverage > 50% for critical paths (DirectorService, Calendar handlers)

**Quality Gates:**
- 🎯 Zero P0 defects unresolved
- 🎯 All RBAC rules have test coverage
- 🎯 Company selection validated across all roles
- 🎯 Concurrency logic verified with race condition tests

---

## 6. Coverage Gap Analysis

### 6.1 Current Coverage: 0%
**No automated tests exist.**

### 6.2 Target Coverage by Component

| Component | Current | Target | Priority |
|-----------|---------|--------|----------|
| DirectorService | 0% | 90% | 🔴 P0 |
| Calendar Pages (Day/Week/Month) | 0% | 70% | 🔴 P0 |
| Assignments/Manage | 0% | 80% | 🔴 P0 |
| Authorization Policies | 0% | 100% | 🔴 P0 |
| CompanyIdInterceptor | 0% | 80% | 🟠 P1 |
| LocalizationService | 0% | 60% | 🟡 P2 |
| Admin Pages | 0% | 50% | 🟡 P2 |

### 6.3 Critical Paths Requiring Coverage

1. **Company Selection Flow** (Day/Week/Month)
   - Owner: sees all companies → selects company → shift created in correct company ✅
   - Director: sees assigned companies only → selects → validates access ✅
   - Manager: no dropdown → shift defaults to own company ✅
   - Unauthorized attempt → 403 Forbidden ✅

2. **Cross-Company Access Control**
   - Director assigned to A & B can access both ✅
   - Director tries to access C → Access Denied ✅
   - Manager tries to access other company → Access Denied ✅

3. **Role Assignment RBAC**
   - Owner assigns Owner → Success ✅
   - Director assigns Owner → Blocked + Audit ✅
   - Manager assigns Manager → Blocked ✅
   - Audit record created on success ✅

4. **Concurrency Control**
   - Valid concurrency → Success ✅
   - Stale concurrency → Rejected ✅
   - Concurrency = 0 for new instance → Success ✅
   - Concurrency = 0 for existing → ⚠️ Currently allowed (RISK-002)

---

## 7. Remediation Strategy

### 7.1 TDD Approach

For each defect:
1. **Red**: Write failing test that reproduces the bug
2. **Green**: Fix the bug with minimal code change
3. **Refactor**: Improve code quality without breaking test
4. **Verify**: Run full test suite to ensure no regressions

### 7.2 Priority Queue

**Week 1 (P0 Defects):**
1. RISK-002: Fix concurrency bypass (2 hours)
   - Test: `ConcurrentAdjustWithZero_ShouldFail_ForExistingInstance`
   - Fix: Add `inst.Id == 0` condition to concurrency skip

2. RISK-001: Add post-query validation (3 hours)
   - Test: `Manager_CannotSeeOtherCompanyShiftTypes`
   - Fix: Validate returned shift types against accessible companies

3. RISK-003: Enforce company access validation (2 hours)
   - Test: `AdjustWithoutCompanyId_StillEnforcesAccess`
   - Fix: Always validate targetCompanyId

**Week 2 (P1 Issues):**
4. ISSUE-002: Refactor duplicate calendar code (4 hours)
   - Extract shared service
   - Update all three views
   - Verify integration tests pass

**Week 3 (Test Coverage):**
5. Achieve 60%+ coverage on critical paths (8 hours)
   - DirectorService: 90%+
   - Calendar handlers: 70%+
   - Authorization: 100%

---

## 8. Next Steps

### Immediate Actions (Next 2 Hours)

1. ✅ Create test strategy document (this file)
2. 🔄 Create ShiftManager.Tests project
3. 🔄 Set up DatabaseFixture and TestWebApplicationFactory
4. 🔄 Write first test: `DirectorService_IsDirector_ReturnsTrueForOwner`
5. 🔄 Write failing test for RISK-002 (concurrency bypass)

### This Sprint (Next 8 Hours)

- Complete Phase 4: DirectorService unit tests (15+ tests)
- Complete Phase 5: Authorization tests (10+ tests)
- Complete Phase 6: Calendar integration tests (12+ tests)
- Generate defect report with actual test results
- Begin P0 remediation

### Long-Term (Post-Sprint)

- CI/CD integration (GitHub Actions / Azure DevOps)
- Code coverage reporting (Coverlet + ReportGenerator)
- Mutation testing (Stryker.NET)
- Performance benchmarking (BenchmarkDotNet)
- E2E tests (Playwright / Selenium)

---

## 9. Appendix: Test Examples

### Example 1: DirectorService Unit Test
```csharp
public class DirectorServiceTests
{
    [Fact]
    public async Task IsDirectorOfAsync_ReturnsTrue_ForOwnerWithAnyCompany()
    {
        // Arrange
        var db = CreateInMemoryDatabase();
        var httpContext = CreateHttpContextWithRole(UserRole.Owner);
        var service = new DirectorService(db, CreateHttpContextAccessor(httpContext));

        // Act
        var result = await service.IsDirectorOfAsync(companyId: 999);

        // Assert
        Assert.True(result); // Owner has access to ALL companies
    }

    [Fact]
    public async Task IsDirectorOfAsync_ReturnsFalse_ForDirectorWithoutAssignment()
    {
        // Arrange
        var db = CreateInMemoryDatabase();
        SeedDirectorWithCompanies(db, userId: 1, companyIds: [1, 2]);
        var httpContext = CreateHttpContextWithRole(UserRole.Director, userId: 1);
        var service = new DirectorService(db, CreateHttpContextAccessor(httpContext));

        // Act
        var result = await service.IsDirectorOfAsync(companyId: 3);

        // Assert
        Assert.False(result); // Director only has access to companies 1 & 2
    }
}
```

### Example 2: Calendar Integration Test
```csharp
public class DayViewCompanySelectionTests : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task OnGetAsync_Owner_LoadsAllCompaniesForDropdown()
    {
        // Arrange
        var client = _factory.CreateClientWithRole(UserRole.Owner);

        // Act
        var response = await client.GetAsync("/Calendar/Day?year=2025&month=10&day=4");

        // Assert
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Demo Co", html);
        Assert.Contains("Test Corp", html);
        // ViewData["Companies"] should have 2 entries
    }

    [Fact]
    public async Task OnPostAdjustAsync_Manager_CannotCreateShiftInOtherCompany()
    {
        // Arrange
        var client = _factory.CreateClientWithRole(UserRole.Manager, companyId: 1);
        var payload = new { date = "2025-10-04", shiftTypeId = 99, delta = 1, companyId = 2 };

        // Act
        var response = await client.PostAsJsonAsync("/Calendar/Day?handler=Adjust", payload);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        // OR: Assert.Contains("You do not have permission", await response.Content.ReadAsStringAsync());
    }
}
```

---

**End of Test Strategy Document**
