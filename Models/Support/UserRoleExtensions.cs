using Microsoft.Extensions.Localization;

namespace ShiftManager.Models.Support;

public static class UserRoleExtensions
{
    public static string GetDisplayName(this UserRole role, IStringLocalizer localizer)
    {
        return role switch
        {
            UserRole.Admin => localizer[$"Role_{nameof(UserRole.Admin)}"],
            UserRole.Manager => localizer[$"Role_{nameof(UserRole.Manager)}"],
            UserRole.Employee => localizer[$"Role_{nameof(UserRole.Employee)}"],
            _ => role.ToString()
        };
    }
}