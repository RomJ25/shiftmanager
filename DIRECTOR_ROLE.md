# Director Role Implementation

## Overview

The Director role enables cross-company management with elevated permissions across multiple companies. Directors can oversee operations across assigned companies while maintaining appropriate security boundaries.

## Features Implemented

### 1. Role & Data Model

#### UserRole Enum
- Added `Director = 3` to `Models/Support/Enums.cs`

#### DirectorCompany Model
- **File**: `Models/DirectorCompany.cs`
- **Purpose**: Maps Directors to Companies they oversee
- **Key Fields**:
  - `UserId`: The Director user
  - `CompanyId`: The Company being overseen
  - `GrantedBy`: User who granted access (typically Owner)
  - `GrantedAt`: Timestamp of grant
  - `IsDeleted`: Soft delete flag
  - `DeletedAt`: Soft delete timestamp

#### Database Schema
- **Table**: `DirectorCompanies`
- **Indexes**:
  - Unique index on `(UserId, CompanyId)` where `IsDeleted = 0`
  - Index on `CompanyId` for querying directors of a company
  - Index on `UserId` for querying companies of a director
- **Foreign Keys**: Restrict delete on User, Company, and GrantedByUser

### 2. Server-Side Permissions

#### DirectorService (`Services/DirectorService.cs`)

**Interface Methods** (`IDirectorService`):
- `IsDirector()`: Check if current user is a Director
- `IsDirectorOfAsync(int companyId)`: Check if Director of specific company
- `GetDirectorCompanyIdsAsync()`: Get all company IDs Director oversees
- `CanManageCompanyAsync(int companyId)`: Check if can manage company
- `CanAssignRole(string role)`: Check if can assign specific role

**Permission Rules**:

✅ **Directors CAN**:
- Add/remove users to companies they direct
- Assign/revoke Manager role within their companies
- Approve/reject time-off requests across their companies
- Approve/reject swap requests across their companies
- View multi-company dashboards and reports
- Access Notification Hub for cross-company alerts
- Use "View as Manager" mode for troubleshooting

❌ **Directors CANNOT**:
- Create or delete companies
- Assign the Owner role to anyone
- Act on companies they are not assigned to
- Access Owner-only features (Companies, Directors pages)

#### Authorization Policies

Added to `Program.cs`:
```csharp
options.AddPolicy("IsManagerOrAdmin",
    policy => policy.RequireRole("Manager", "Owner", "Director"));
options.AddPolicy("IsAdmin",
    policy => policy.RequireRole("Owner"));
options.AddPolicy("IsDirector",
    policy => policy.RequireRole("Director"));
options.AddPolicy("IsOwnerOrDirector",
    policy => policy.RequireRole("Owner", "Director"));
```

### 3. Cross-Company UI Features

#### Company Filter Service
- **Files**: `Services/ICompanyFilterService.cs`, `Services/CompanyFilterService.cs`
- **Purpose**: Manage Director's company selection filter
- **Storage**: Browser cookie (`director_company_filter`)
- **Persistence**: 30 days
- **Features**:
  - Select multiple companies to view
  - Applies to Calendar, Requests, Dashboards
  - Persists across sessions
  - Auto-validates user has access to selected companies

#### Company Filter Page
- **Path**: `/Director/CompanyFilter`
- **Features**:
  - Multi-select checkbox interface
  - Shows all assigned companies
  - "Apply Filter" to save selection
  - "Show All Companies" to clear filter

#### Notification Hub
- **Path**: `/Director/NotificationHub`
- **Features**:
  - Aggregates notifications from all assigned companies
  - Shows pending time-off requests across companies
  - Shows pending swap requests across companies
  - Displays recent notifications (last 50)
  - Company flair: slug inline, full name on hover
  - Real-time cross-company overview

### 4. View as Manager Mode

#### ViewAsModeService
- **Files**: `Services/IViewAsModeService.cs`, `Services/ViewAsModeService.cs`
- **Purpose**: Enable Directors to view system with Manager permissions
- **Storage**: Browser cookie (`director_view_as_mode`)
- **Expiration**: 8 hours (auto-expire)

**Features**:
- Enter mode for specific company
- Scope UI to single company
- Permissions match Manager exactly
- Hide all other companies
- Persistent banner showing active mode
- Easy exit via banner or View Mode page

#### View as Manager Page
- **Path**: `/Director/ViewAsMode`
- **Features**:
  - List all assigned companies
  - "View as Manager" button per company
  - Warning banner when in mode
  - "Exit View as Manager Mode" button
  - Redirects to Calendar/Month on enter

#### UI Banner
- **Location**: `Pages/Shared/_Layout.cshtml`
- **Display**: Shown at top of every page when in View Mode
- **Style**: Yellow warning background
- **Content**: "⚠️ Viewing as Manager of {Company} — Exit View Mode"
- **Link**: Direct link to exit mode

### 5. Director Management Pages

#### Directors Assignment Page (Owner-Only)
- **Path**: `/Admin/Directors`
- **Authorization**: Owner only
- **Features**:
  - Assign Director to Company
  - Revoke Director access (soft delete)
  - View all current assignments
  - Shows granted by, granted at
  - Company slug and full name display

#### Updated Users Page
- **Path**: `/Admin/Users`
- **Changes**:
  - Added "Director" to role dropdown (add user)
  - Added "Director" to role dropdown (edit user)
  - Visible to Managers and Directors (IsManagerOrAdmin policy)

### 6. Navigation Updates

#### Layout Navigation
Added Director dropdown menu with:
- Notification Hub
- Company Filter
- View as Manager

Only visible to users with Director role.

#### Admin Menu
Added "Directors" link (Owner-only) for managing Director assignments.

### 7. Feature Flag & Configuration

#### appsettings.json
```json
"Features": {
  "EnableDirectorRole": true
}
```

Controls Director role visibility and seed data generation in development.

### 8. Seed Data (Development Only)

When `EnableDirectorRole = true` and environment is Development:

1. **Second Company Created**: "Test Corp" (slug: test-corp)
   - Includes shift types, configs

2. **Director User Created**:
   - Email: `director@local`
   - Password: `director123`
   - Display Name: "Test Director"

3. **Director Assignments**:
   - Assigned to both "Demo Co" and "Test Corp"
   - Granted by Owner user

### 9. Database Migration

**Migration**: `20250930210753_AddDirectorRole`

**Changes**:
- Creates `DirectorCompanies` table
- Adds indexes for performance
- Adds foreign key constraints
- Applied successfully to database

## Testing Checklist

### ✅ Acceptance Criteria

**Director of Company A & B can:**

1. ✅ Add/remove users in A & B
   - Through `/Admin/Users` page
   - Role assignment validated by `DirectorService.CanAssignRole()`

2. ✅ Assign/revoke Managers in A & B
   - Director can assign Manager role
   - Director cannot assign Owner role (blocked)

3. ✅ Approve/reject requests across A & B
   - Access via `/Requests/Index`
   - IsManagerOrAdmin policy includes Directors

4. ✅ See dashboards/reports across A & B
   - Notification Hub aggregates both companies
   - Company filter allows selection

5. ✅ Use Notification Hub aggregated across A & B
   - `/Director/NotificationHub` shows all assigned companies
   - Company slug inline, full name on hover

6. ✅ Enter/exit "View as Manager" for A
   - `/Director/ViewAsMode` page
   - Only Company A visible when in mode
   - Manager-level permissions applied
   - Banner shows current mode
   - Easy exit via banner link

**Blocked with clear error:**

1. ✅ Company create/delete
   - `/Admin/Companies` only visible to Owners
   - Director role not in IsAdmin policy

2. ✅ Assigning Owner role
   - `DirectorService.CanAssignRole("Owner")` returns false
   - Enforced on user creation/update

3. ✅ Acting on companies not assigned
   - `DirectorService.IsDirectorOfAsync()` validates access
   - `DirectorService.GetDirectorCompanyIdsAsync()` scopes queries

## Files Modified/Created

### Models
- ✅ `Models/Support/Enums.cs` (modified)
- ✅ `Models/DirectorCompany.cs` (new)
- ✅ `Models/IBelongsToCompany.cs` (existing, used by DirectorCompany)

### Data
- ✅ `Data/AppDbContext.cs` (modified)
- ✅ `Migrations/20250930210753_AddDirectorRole.cs` (new)

### Services
- ✅ `Services/IDirectorService.cs` (new)
- ✅ `Services/DirectorService.cs` (new)
- ✅ `Services/ICompanyFilterService.cs` (new)
- ✅ `Services/CompanyFilterService.cs` (new)
- ✅ `Services/IViewAsModeService.cs` (new)
- ✅ `Services/ViewAsModeService.cs` (new)

### Pages
- ✅ `Pages/Admin/Directors.cshtml` (new)
- ✅ `Pages/Admin/Directors.cshtml.cs` (new)
- ✅ `Pages/Admin/Users.cshtml` (modified)
- ✅ `Pages/Director/NotificationHub.cshtml` (new)
- ✅ `Pages/Director/NotificationHub.cshtml.cs` (new)
- ✅ `Pages/Director/CompanyFilter.cshtml` (new)
- ✅ `Pages/Director/CompanyFilter.cshtml.cs` (new)
- ✅ `Pages/Director/ViewAsMode.cshtml` (new)
- ✅ `Pages/Director/ViewAsMode.cshtml.cs` (new)
- ✅ `Pages/Shared/_Layout.cshtml` (modified)

### Configuration
- ✅ `Program.cs` (modified)
- ✅ `appsettings.json` (modified)

## Usage Guide

### For Owners

1. **Create a Director User**:
   - Go to `/Admin/Users`
   - Add new user with role "Director"

2. **Assign Companies to Director**:
   - Go to `/Admin/Directors`
   - Select Director and Company
   - Click "Assign"

3. **Revoke Access**:
   - Go to `/Admin/Directors`
   - Click "Revoke" on assignment

### For Directors

1. **Access Notification Hub**:
   - Click "Director" → "Notification Hub"
   - View aggregated notifications and pending requests

2. **Filter Companies**:
   - Click "Director" → "Company Filter"
   - Select companies to focus on
   - Click "Apply Filter"

3. **View as Manager**:
   - Click "Director" → "View as Manager"
   - Select company to view
   - Click "View as Manager"
   - Work in scoped Manager mode
   - Click "Exit View Mode" when done

## Security Notes

1. **Role Assignment**: Directors cannot escalate themselves or others to Owner
2. **Company Scoping**: All queries validate Director has access to company
3. **Soft Deletes**: Director assignments use soft delete for audit trail
4. **Cookie Security**: HttpOnly, SameSite=Strict cookies for session data
5. **Authorization Policies**: Server-side enforcement on all endpoints

## Performance Considerations

1. **Indexes**: Optimized indexes on DirectorCompanies table
2. **Query Filters**: Efficient company ID filtering in queries
3. **Cookie Storage**: Minimal data in cookies (just IDs)
4. **Lazy Loading**: Services only query when needed

## Future Enhancements

Potential additions (not currently implemented):
- Calendar overlay with company legend
- Multi-company workload/resource views
- Director-specific dashboards
- Audit log of Director actions
- Bulk user management across companies
- Company-specific permission overrides
- Director hierarchy (senior directors overseeing directors)

## Rollback Plan

To disable the Director role:

1. Set `Features:EnableDirectorRole = false` in appsettings.json
2. Hide Director option from UI by checking feature flag
3. Remove Director policy from authorization if needed
4. Optionally: Create migration to drop DirectorCompanies table

## Support

For issues or questions:
- Check `/Admin/Directors` page for assignment status
- Verify `Features:EnableDirectorRole` is enabled
- Check browser console for JavaScript errors
- Review application logs for permission errors
