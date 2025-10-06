# ShiftManager

**Multi-tenant shift scheduling and workforce management system built with ASP.NET Core 8.0**

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

---

## Features

✨ **Multi-Tenant Architecture** - Fully isolated company data with automatic scoping
📅 **Shift Management** - Month/Week/Day calendar views with drag-and-drop assignments
👥 **Role-Based Access** - Owner, Director, Manager, Employee, and Trainee roles
🔄 **Shift Swaps** - Employee-initiated shift swap requests with manager approval
🌴 **Time-Off Requests** - PTO request workflow with conflict detection
🎓 **Trainee Shadowing** - Assign trainees to shadow experienced employees
🌍 **Localization** - English (en-US) and Hebrew (he-IL) support
🔔 **In-App Notifications** - Real-time notifications for shift changes and approvals

---

## Quick Start

### Prerequisites

- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Git (for cloning the repository)

### Installation (3 commands)

**Windows (PowerShell):**
```powershell
git clone <repository-url>
cd ShiftManager
.\setup.ps1
```

**Linux/macOS (Bash):**
```bash
git clone <repository-url>
cd ShiftManager
chmod +x setup.sh
./setup.sh
```

### Run the Application

```bash
dotnet run
```

Navigate to: **http://localhost:5000**

**Default login:**
- Email: `admin@local`
- Password: `admin123`

---

## Documentation

📖 **[Complete Documentation](project.md)** - Full technical documentation (2100+ lines)
🗺️ **[Next Week Plan](next%20week%20plan.md)** - Implementation plan for upcoming features
🔄 **[Migration Guide](MIGRATIONS.md)** - Database migration safety guidelines
📋 **[Task Tracker](tasks.md)** - Issues and enhancements tracker

---

## Tech Stack

| Component | Technology |
|-----------|-----------|
| **Framework** | ASP.NET Core 8.0 (Razor Pages) |
| **Database** | SQLite (via EF Core 9.0) |
| **Authentication** | Cookie-based (ASP.NET Core Identity) |
| **Frontend** | Razor Pages + Vanilla JS |
| **Localization** | ASP.NET Core Resource Files |
| **Testing** | xUnit + FluentAssertions + Moq |

---

## Project Structure

```
ShiftManager/
├── Data/                # Database context & multi-tenant interceptors
├── Migrations/          # EF Core migrations (11 migrations)
├── Models/              # Domain entities (12 tables)
├── Pages/               # Razor Pages (26 routes)
├── Services/            # Business logic (6 services)
├── Resources/           # Localization files (en-US, he-IL)
├── wwwroot/             # Static files (CSS, JS)
├── seed.db              # Clean seed database (committed)
└── app.db               # Local development database (ignored)
```

---

## Key Features

### Multi-Tenant Isolation
- Automatic `CompanyId` scoping via EF Core query filters
- Cross-company access for Director role
- Strict authorization checks in service layer

### Shift Management
- **Calendar Views:** Month, Week, Day
- **Staffing Adjustment:** Quick +/- buttons with concurrency control
- **Conflict Detection:** Prevent double-booking and time-off conflicts
- **Trainee Shadowing:** Assign trainees to learn from experienced employees

### Role-Based Access Control (RBAC)
- **Owner:** Global admin, manages all companies
- **Director:** Multi-company manager, cross-tenant access
- **Manager:** Company admin, manages users and shifts
- **Employee:** Views shifts, requests time-off and swaps
- **Trainee:** Shadows employees, limited permissions

### Localization
- **Supported Languages:** English (en-US), Hebrew (he-IL)
- **RTL Support:** Proper right-to-left layout for Hebrew
- **Culture-Aware Formatting:** Dates, times, and numbers

---

## Development

### Running Tests
```bash
dotnet test
```

**Current Coverage:** 15 unit tests (DirectorService only)
**Target Coverage:** 80% for service layer, 60% for presentation layer

### Database Management
```bash
# Create a new migration
dotnet ef migrations add <MigrationName>

# Apply migrations
dotnet ef database update

# Rollback to specific migration
dotnet ef database update <MigrationName>

# Reset database to clean state
rm app.db
cp seed.db app.db
```

### Hot Reload (Development)
```bash
dotnet watch
```

---

## Architecture

### Multi-Tenant Flow
```
User Login → CompanyId Resolution → Query Filters
  → Automatic WHERE CompanyId = @current
  → CompanyIdInterceptor injects CompanyId on SaveChanges
```

### Key Services
- **NotificationService:** In-app notifications for shift changes
- **DirectorService:** Multi-company access validation
- **TraineeService:** Shadowing assignment management
- **ConflictChecker:** Prevent scheduling conflicts
- **LocalizationService:** Culture-aware formatting

---

## Deployment

### Production Checklist
- [ ] Set `SEED_ADMIN_PASSWORD` environment variable
- [ ] Backup database before migrations
- [ ] Run migrations: `dotnet ef database update`
- [ ] Verify health check: `/health`
- [ ] Monitor logs for errors (first 24 hours)

### Health Check
```bash
curl https://your-domain.com/health
# Expected: "Healthy"
```

---

## Roadmap

### Completed ✅
- Multi-tenant architecture with company isolation
- Full RBAC with 5 roles (Owner, Director, Manager, Employee, Trainee)
- Calendar views (Month, Week, Day)
- Shift assignment with conflict detection
- Time-off and swap request workflows
- Trainee shadowing functionality
- Localization (English, Hebrew)
- In-app notification system

### Upcoming 🚀 (See [next week plan.md](next%20week%20plan.md))
1. **Export & Print Schedules** - Excel, PDF, print-friendly views
2. **Employee Profile Enhancements** - Avatar upload, skills, certifications
3. **Shift Reminder Notifications** - Automated 6h and 2h reminders
4. **Announcements Feed** - Top-down communication with attachments
5. **Shift Analytics & Reporting** - Workforce analytics and back-to-back shift detection

### Future Enhancements
- Email notifications (in addition to in-app)
- Mobile app (iOS/Android)
- Auto-scheduling algorithm (AI-based)
- Time tracking & attendance (clock in/out)
- External calendar integration (Google Calendar, Outlook)
- Reporting & analytics dashboard
- Multi-location/department support

---

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/my-feature`
3. Commit changes: `git commit -m 'Add my feature'`
4. Push to branch: `git push origin feature/my-feature`
5. Submit a pull request

**Coding Standards:**
- Follow existing code style (ASP.NET Core conventions)
- Write unit tests for new services (80% coverage target)
- Update documentation for new features
- Test migrations (up/down/up cycle)

---

## Security

**Reporting Vulnerabilities:**
Please report security vulnerabilities to [security@example.com](mailto:security@example.com)

**Security Features:**
- ✅ PBKDF2 password hashing (100k iterations, SHA256)
- ✅ CSRF protection (anti-forgery tokens)
- ✅ SQL injection protection (EF Core parameterized queries)
- ✅ XSS protection (Razor auto-escaping)
- ✅ Multi-tenant isolation (query filters + validation)
- ✅ Role-based authorization (policies + service layer checks)

**Known Gaps (see [tasks.md](tasks.md)):**
- ⚠️ No rate limiting on login attempts
- ⚠️ No security headers (X-Frame-Options, CSP, HSTS)
- ⚠️ Database stored unencrypted (use SQLCipher for production)

---

## License

[MIT License](LICENSE) - see LICENSE file for details

---

## Support

- **Documentation:** [project.md](project.md)
- **Issues:** [GitHub Issues](https://github.com/your-org/shiftmanager/issues)
- **Email:** support@example.com

---

**Built with ❤️ using ASP.NET Core 8.0**
