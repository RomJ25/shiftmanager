-- Check Companies
SELECT 'Companies:' as Info;
SELECT Id, Name, Slug FROM Companies;

-- Check User b@b
SELECT 'User b@b:' as Info;
SELECT Id, Email, DisplayName, Role, CompanyId, IsActive FROM Users WHERE Email = 'b@b';

-- Check Shift Instances
SELECT 'Shift Instances:' as Info;
SELECT si.Id, si.CompanyId, si.WorkDate, si.StaffingRequired, st.Key as ShiftTypeKey
FROM ShiftInstances si
JOIN ShiftTypes st ON si.ShiftTypeId = st.Id
WHERE si.WorkDate >= '2025-10-01';

-- Check Shift Assignments
SELECT 'Shift Assignments:' as Info;
SELECT sa.Id, sa.UserId, sa.CompanyId, sa.ShiftInstanceId, u.Email, si.WorkDate
FROM ShiftAssignments sa
JOIN Users u ON sa.UserId = u.Id
JOIN ShiftInstances si ON sa.ShiftInstanceId = si.Id
WHERE u.Email = 'b@b';
