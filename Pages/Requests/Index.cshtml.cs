public async Task<IActionResult> OnPostApproveSwapAsync(int id)
{
    var companyId = _companyScope.GetCurrentCompanyId(User);
    var swap = await _companyScope.GetCompanySwapRequestAsync(id, companyId);
    if (swap == null) return Forbid();

    var assignment = await _companyScope.GetCompanyShiftAssignmentAsync(swap.FromAssignmentId, companyId);
    if (assignment == null)
    {
        swap.Status = RequestStatus.Declined;
        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    var instance = await _db.ShiftInstances
        .SingleOrDefaultAsync(i => i.Id == assignment.ShiftInstanceId && i.CompanyId == companyId);
    if (instance == null)
    {
        swap.Status = RequestStatus.Declined;
        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    var shiftType = await _db.ShiftTypes.FindAsync(instance.ShiftTypeId);
    if (shiftType == null)
    {
        swap.Status = RequestStatus.Declined;
        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    // Ensure recipient is selected
    if (!swap.ToUserId.HasValue)
    {
        Error = "Cannot approve an open swap without selecting a recipient.";
        await OnGetAsync();
        return Page();
    }

    var targetUserId = swap.ToUserId.Value;

    var conflict = await _checker.CanAssignAsync(targetUserId, instance);
    if (!conflict.Allowed)
    {
        Error = "Cannot approve swap: " + string.Join(" ", conflict.Reasons);
        await OnGetAsync();
        return Page();
    }

    await using var trx = await _db.Database.BeginTransactionAsync();

    // Get original user for notification
    var originalUserId = assignment.UserId;

    // Reassign
    assignment.UserId = targetUserId;
    swap.Status = RequestStatus.Approved;

    await _db.SaveChangesAsync();
    await trx.CommitAsync();

    // Send notification to original user
    var shiftInfo = $"{shiftType.Name} on {instance.WorkDate:MMM dd, yyyy} " +
                    $"({shiftType.Start:HH:mm} - {shiftType.End:HH:mm})";
    await _notificationService.CreateSwapRequestNotificationAsync(
        originalUserId, RequestStatus.Approved, shiftInfo, swap.Id);

    return RedirectToPage();
}

public async Task<IActionResult> OnPostDeclineSwapAsync(int id)
{
    var companyId = _companyScope.GetCurrentCompanyId(User);
    var swapData = await (from s in _db.SwapRequests
                          where s.Id == id
                          join assign in _db.ShiftAssignments on s.FromAssignmentId equals assign.Id
                          join fromUser in _db.Users on assign.UserId equals fromUser.Id
                          select new { Swap = s, FromUser = fromUser })
                         .FirstOrDefaultAsync();
    if (swapData == null) return RedirectToPage();
    if (swapData.FromUser.CompanyId != companyId) return Forbid();

    var s = swapData.Swap;
    s.Status = RequestStatus.Declined;
    await _db.SaveChangesAsync();

    return RedirectToPage();
}
