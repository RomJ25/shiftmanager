using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Security.Claims;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.Models.Support;
using ShiftManager.Services;
using FluentAssertions;

namespace ShiftManager.Tests.UnitTests.Services;

public class DirectorServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly DirectorService _service;

    public DirectorServiceTests()
    {
        // Create InMemory database
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _service = new DirectorService(_db, _httpContextAccessorMock.Object);
    }

    private void SetupUser(int userId, UserRole role)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
    }

    [Fact]
    public void IsDirector_ReturnsTrue_ForOwner()
    {
        // Arrange
        SetupUser(1, UserRole.Owner);

        // Act
        var result = _service.IsDirector();

        // Assert
        result.Should().BeTrue("Owner has Director permissions");
    }

    [Fact]
    public void IsDirector_ReturnsTrue_ForDirector()
    {
        // Arrange
        SetupUser(1, UserRole.Director);

        // Act
        var result = _service.IsDirector();

        // Assert
        result.Should().BeTrue("Director role has Director permissions");
    }

    [Fact]
    public void IsDirector_ReturnsFalse_ForManager()
    {
        // Arrange
        SetupUser(1, UserRole.Manager);

        // Act
        var result = _service.IsDirector();

        // Assert
        result.Should().BeFalse("Manager does not have Director permissions");
    }

    [Fact]
    public void IsDirector_ReturnsFalse_ForEmployee()
    {
        // Arrange
        SetupUser(1, UserRole.Employee);

        // Act
        var result = _service.IsDirector();

        // Assert
        result.Should().BeFalse("Employee does not have Director permissions");
    }

    [Fact]
    public async Task IsDirectorOfAsync_ReturnsTrue_ForOwnerWithAnyCompany()
    {
        // Arrange
        SetupUser(1, UserRole.Owner);

        // Act - Owner should have access to ANY company, even one that doesn't exist
        var result = await _service.IsDirectorOfAsync(companyId: 999);

        // Assert
        result.Should().BeTrue("Owner has access to all companies");
    }

    [Fact]
    public async Task IsDirectorOfAsync_ReturnsTrue_ForDirectorWithAssignment()
    {
        // Arrange
        var company = new Company { Id = 1, Name = "Test Co" };
        var director = new AppUser { Id = 10, CompanyId = 1, Role = UserRole.Director, Email = "director@test", DisplayName = "Test Director", IsActive = true };
        var directorAssignment = new DirectorCompany
        {
            Id = 1,
            UserId = 10,
            CompanyId = 1,
            GrantedBy = 1,
            GrantedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        _db.Companies.Add(company);
        _db.Users.Add(director);
        _db.DirectorCompanies.Add(directorAssignment);
        await _db.SaveChangesAsync();

        SetupUser(10, UserRole.Director);

        // Act
        var result = await _service.IsDirectorOfAsync(companyId: 1);

        // Assert
        result.Should().BeTrue("Director has assignment to company 1");
    }

    [Fact]
    public async Task IsDirectorOfAsync_ReturnsFalse_ForDirectorWithoutAssignment()
    {
        // Arrange
        var company1 = new Company { Id = 1, Name = "Company A" };
        var company2 = new Company { Id = 2, Name = "Company B" };
        var director = new AppUser { Id = 10, CompanyId = 1, Role = UserRole.Director, Email = "director@test", DisplayName = "Test Director", IsActive = true };
        var directorAssignment = new DirectorCompany
        {
            Id = 1,
            UserId = 10,
            CompanyId = 1,  // Assigned to company 1 only
            GrantedBy = 1,
            GrantedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        _db.Companies.AddRange(company1, company2);
        _db.Users.Add(director);
        _db.DirectorCompanies.Add(directorAssignment);
        await _db.SaveChangesAsync();

        SetupUser(10, UserRole.Director);

        // Act - Try to access company 2
        var result = await _service.IsDirectorOfAsync(companyId: 2);

        // Assert
        result.Should().BeFalse("Director is NOT assigned to company 2");
    }

    [Fact]
    public async Task IsDirectorOfAsync_ReturnsFalse_ForManagerRole()
    {
        // Arrange
        SetupUser(1, UserRole.Manager);

        // Act
        var result = await _service.IsDirectorOfAsync(companyId: 1);

        // Assert
        result.Should().BeFalse("Manager role does not have Director permissions");
    }

    [Fact]
    public void CanAssignRole_Owner_CanAssignAnyRole()
    {
        // Arrange
        SetupUser(1, UserRole.Owner);

        // Act & Assert
        _service.CanAssignRole(UserRole.Owner).Should().BeTrue();
        _service.CanAssignRole(UserRole.Director).Should().BeTrue();
        _service.CanAssignRole(UserRole.Manager).Should().BeTrue();
        _service.CanAssignRole(UserRole.Employee).Should().BeTrue();
    }

    [Fact]
    public void CanAssignRole_Director_CannotAssignOwner()
    {
        // Arrange
        SetupUser(1, UserRole.Director);

        // Act
        var canAssignOwner = _service.CanAssignRole(UserRole.Owner);

        // Assert
        canAssignOwner.Should().BeFalse("Director CANNOT assign Owner role - security critical!");
    }

    [Fact]
    public void CanAssignRole_Director_CanAssignDirectorManagerEmployee()
    {
        // Arrange
        SetupUser(1, UserRole.Director);

        // Act & Assert
        _service.CanAssignRole(UserRole.Director).Should().BeTrue();
        _service.CanAssignRole(UserRole.Manager).Should().BeTrue();
        _service.CanAssignRole(UserRole.Employee).Should().BeTrue();
    }

    [Fact]
    public void CanAssignRole_Manager_CanOnlyAssignEmployee()
    {
        // Arrange
        SetupUser(1, UserRole.Manager);

        // Act & Assert
        _service.CanAssignRole(UserRole.Owner).Should().BeFalse();
        _service.CanAssignRole(UserRole.Director).Should().BeFalse();
        _service.CanAssignRole(UserRole.Manager).Should().BeFalse();
        _service.CanAssignRole(UserRole.Employee).Should().BeTrue("Manager can assign Employee");
    }

    [Fact]
    public void CanAssignRole_Employee_CannotAssignAnyRole()
    {
        // Arrange
        SetupUser(1, UserRole.Employee);

        // Act & Assert
        _service.CanAssignRole(UserRole.Owner).Should().BeFalse();
        _service.CanAssignRole(UserRole.Director).Should().BeFalse();
        _service.CanAssignRole(UserRole.Manager).Should().BeFalse();
        _service.CanAssignRole(UserRole.Employee).Should().BeFalse("Employee cannot assign any role");
    }

    [Fact]
    public async Task GetDirectorCompanyIdsAsync_ReturnsAssignedCompanies()
    {
        // Arrange
        var director = new AppUser { Id = 10, CompanyId = 1, Role = UserRole.Director, Email = "director@test", DisplayName = "Test Director", IsActive = true };
        var assignment1 = new DirectorCompany { Id = 1, UserId = 10, CompanyId = 1, GrantedBy = 1, GrantedAt = DateTime.UtcNow, IsDeleted = false };
        var assignment2 = new DirectorCompany { Id = 2, UserId = 10, CompanyId = 2, GrantedBy = 1, GrantedAt = DateTime.UtcNow, IsDeleted = false };

        _db.Users.Add(director);
        _db.DirectorCompanies.AddRange(assignment1, assignment2);
        await _db.SaveChangesAsync();

        SetupUser(10, UserRole.Director);

        // Act
        var companyIds = await _service.GetDirectorCompanyIdsAsync();

        // Assert
        companyIds.Should().HaveCount(2);
        companyIds.Should().Contain(new[] { 1, 2 });
    }

    [Fact]
    public async Task GetDirectorCompanyIdsAsync_ExcludesDeletedAssignments()
    {
        // Arrange
        var director = new AppUser { Id = 10, CompanyId = 1, Role = UserRole.Director, Email = "director@test", DisplayName = "Test Director", IsActive = true };
        var activeAssignment = new DirectorCompany { Id = 1, UserId = 10, CompanyId = 1, GrantedBy = 1, GrantedAt = DateTime.UtcNow, IsDeleted = false };
        var deletedAssignment = new DirectorCompany { Id = 2, UserId = 10, CompanyId = 2, GrantedBy = 1, GrantedAt = DateTime.UtcNow, IsDeleted = true, DeletedAt = DateTime.UtcNow };

        _db.Users.Add(director);
        _db.DirectorCompanies.AddRange(activeAssignment, deletedAssignment);
        await _db.SaveChangesAsync();

        SetupUser(10, UserRole.Director);

        // Act
        var companyIds = await _service.GetDirectorCompanyIdsAsync();

        // Assert
        companyIds.Should().HaveCount(1);
        companyIds.Should().Contain(1);
        companyIds.Should().NotContain(2, "Deleted assignments should be excluded");
    }

    public void Dispose()
    {
        _db?.Dispose();
    }
}
