using System;

namespace ShiftManager.Models.Support;

public static class UserRoleExtensions
{
    public static bool IsManagerial(this UserRole role) =>
        role == UserRole.Manager || role == UserRole.Admin;

    public static bool IsManagerial(string? roleName) =>
        Enum.TryParse<UserRole>(roleName, out var role) && role.IsManagerial();
}
