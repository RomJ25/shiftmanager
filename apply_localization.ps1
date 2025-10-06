# PowerShell script to apply localization to all pages
# This script replaces hardcoded English text with @Localizer["Key"] calls

$files = @(
    "Pages/Admin/Companies.cshtml",
    "Pages/Admin/Directors.cshtml",
    "Pages/Auth/Signup.cshtml",
    "Pages/Admin/Users.cshtml"
)

$replacements = @{
    # Companies Page
    "Companies" = "@Localizer[""Companies""]";
    "Add New Company" = "@Localizer[""AddNewCompany""]";
    "Company Details" = "@Localizer[""CompanyDetails""]";
    "Company Name" = "@Localizer[""CompanyName""]";
    "Slug" = "@Localizer[""Slug""]";
    "Manager Account" = "@Localizer[""ManagerAccount""]";
    "Manager Email" = "@Localizer[""ManagerEmail""]";
    "Manager Display Name" = "@Localizer[""ManagerDisplayName""]";
    "Manager Password" = "@Localizer[""ManagerPassword""]";
    "Create Company & Manager" = "@Localizer[""CreateCompanyManager""]";
    "Note" = "@Localizer[""Note""]";
    "Creating a company will:" = "@Localizer[""CreatingCompanyWill""]";
    "Create the company with the specified name and slug" = "@Localizer[""CreateCompanyWithDetails""]";
    "Create a manager user account for this company" = "@Localizer[""CreateManagerAccount""]";
    "Generate default shift types (Morning, Noon, Night, Middle)" = "@Localizer[""GenerateDefaultShiftTypes""]";
    "Set up default configuration (rest hours, weekly cap)" = "@Localizer[""SetupDefaultConfig""]";
    "All Companies" = "@Localizer[""AllCompanies""]";
    "No companies yet. Create one above to get started." = "@Localizer[""NoCompaniesYet""]";

    # Directors Page
    "Director Assignments" = "@Localizer[""DirectorAssignments""]";
    "Assign Director to Company" = "@Localizer[""AssignDirectorToCompany""]";
    "-- Select Director --" = "@Localizer[""SelectDirector""]";
    "-- Select Company --" = "@Localizer[""SelectCompany""]";
    "Current Director Assignments" = "@Localizer[""CurrentDirectorAssignments""]";
    "Granted By" = "@Localizer[""GrantedBy""]";
    "Granted At" = "@Localizer[""GrantedAt""]";
    "Revoke" = "@Localizer[""Revoke""]";
    "No director assignments yet. Assign directors to companies above." = "@Localizer[""NoDirectorAssignments""]";
    "About Director Role:" = "@Localizer[""AboutDirectorRole""]";
    "Directors can oversee multiple companies and have the following permissions within their assigned companies:" = "@Localizer[""DirectorsCanOversee""]";
    "Add/remove users (Employee and Manager roles only)" = "@Localizer[""DirectorPerm1""]";
    "Assign/revoke Manager role" = "@Localizer[""DirectorPerm2""]";
    "Approve/reject time-off and swap requests" = "@Localizer[""DirectorPerm3""]";
    "View multi-company dashboards and reports" = "@Localizer[""DirectorPerm4""]";
    "Directors cannot create/delete companies or assign the Owner role." = "@Localizer[""DirectorsCannot""]";

    # Signup Page
    "Request Access" = "@Localizer[""RequestAccess""]";
    "Request Submitted" = "@Localizer[""RequestSubmitted""]";
    "Password (min. 6 characters)" = "@Localizer[""PasswordMinCharacters""]";
    "Requested Role" = "@Localizer[""RequestedRole""]";
    "Your request will need to be approved by authorized personnel." = "@Localizer[""RequestNeedsApproval""]";
    "Submit Request" = "@Localizer[""SubmitRequest""]";
    "Already have an account?" = "@Localizer[""AlreadyHaveAccount""]";
    "Login here" = "@Localizer[""LoginHere""]";

    # Users Page
    "Join Requests" = "@Localizer[""JoinRequests""]";
    "Submitted" = "@Localizer[""Submitted""]";
    "Rejected" = "@Localizer[""Rejected""]";
    "Clear Filters" = "@Localizer[""ClearFilters""]";
    "Approve" = "@Localizer[""Approve""]";
    "Reject" = "@Localizer[""Reject""]";
    "No join requests found with the current filters." = "@Localizer[""NoJoinRequestsFound""]";
    "Existing Users" = "@Localizer[""ExistingUsers""]";
    "Clear User Filters" = "@Localizer[""ClearUserFilters""]";
    "Password" = "@Localizer[""Password""]";
    "User Deletion Warning:" = "@Localizer[""UserDeletionWarning""]";
    "Deleting a user will permanently remove:" = "@Localizer[""DeletingUserWillRemove""]";
    "All their shift assignments (shifts will become empty)" = "@Localizer[""AllTheirShiftAssignments""]";
    "All their time-off requests (approved, pending, and declined)" = "@Localizer[""AllTheirTimeOffRequests""]";
    "All their swap requests (both from and to the user)" = "@Localizer[""AllTheirSwapRequests""]";
    "The user account itself" = "@Localizer[""TheUserAccountItself""]";
    "This action cannot be undone. Use with extreme caution." = "@Localizer[""ActionCannotBeUndone""]";
    "Add User" = "@Localizer[""AddUser""]";
    "Set" = "@Localizer[""Set""]";
}

Write-Host "Localization script created but not executed automatically"
Write-Host "Manual page updates required due to context-sensitive replacements"
