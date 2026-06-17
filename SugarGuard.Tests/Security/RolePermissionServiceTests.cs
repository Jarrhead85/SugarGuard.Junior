using SugarGuard.Domain.Enums;
using SugarGuard.Domain.Security;
using SugarGuard.Infrastructure.Security;

namespace SugarGuard.Tests.Security;

/// <summary>
/// Тесты для проверки корректности выдачи разрешений по ролям.
/// Validates: Requirements 16.1, 16.2, 16.3
/// </summary>
public class RolePermissionServiceTests
{
    private readonly RolePermissionService _sut = new();

    // -----------------------------------------------------------------------
    // Req 16.1 — Каждая роль возвращает не-null коллекцию
    // -----------------------------------------------------------------------

    /// <summary>
    /// Проверяет, что для каждой роли <see cref="UserRole"/> метод возвращает
    /// не-null коллекцию. Для ролей с определёнными разрешениями коллекция
    /// должна быть непустой; для <see cref="UserRole.ChildDevice"/> допускается
    /// пустая коллекция (роль не имеет разрешений).
    /// </summary>
    [Theory]
    [InlineData(UserRole.Parent,         true)]
    [InlineData(UserRole.Doctor,         true)]
    [InlineData(UserRole.Admin,          true)]
    [InlineData(UserRole.SupportAdmin,   true)]
    [InlineData(UserRole.ChildDevice,    false)]
    [InlineData(UserRole.ServiceAccount, true)]
    public void GetPermissions_AllRoles_ReturnsNonNullNonEmptyCollection(
        UserRole role,
        bool expectNonEmpty)
    {
        // ARRANGE — экземпляр сервиса создан в конструкторе

        // ACT
        var result = _sut.GetPermissions(role);

        // ASSERT
        Assert.NotNull(result);

        if (expectNonEmpty)
            Assert.NotEmpty(result);
    }

    // -----------------------------------------------------------------------
    // Req 16.2 — Admin содержит все разрешения Parent
    // -----------------------------------------------------------------------

    /// <summary>
    /// Проверяет, что коллекция разрешений роли Admin включает все разрешения,
    /// которые есть у роли Parent.
    /// </summary>
    [Fact]
    public void GetPermissions_Admin_ContainsAllParentPermissions()
    {
        // ARRANGE
        var parentPermissions = _sut.GetPermissions(UserRole.Parent);
        var adminPermissions  = _sut.GetPermissions(UserRole.Admin);

        // ACT & ASSERT
        foreach (var permission in parentPermissions)
        {
            Assert.Contains(permission, adminPermissions);
        }
    }

    /// <summary>
    /// Проверяет, что коллекция разрешений роли SupportAdmin включает все
    /// разрешения, которые есть у роли Parent (SupportAdmin использует тот же
    /// набор разрешений, что и Admin).
    /// </summary>
    [Fact]
    public void GetPermissions_SupportAdmin_ContainsAllParentPermissions()
    {
        // ARRANGE
        var parentPermissions       = _sut.GetPermissions(UserRole.Parent);
        var supportAdminPermissions = _sut.GetPermissions(UserRole.SupportAdmin);

        // ACT & ASSERT
        foreach (var permission in parentPermissions)
        {
            Assert.Contains(permission, supportAdminPermissions);
        }
    }

    // -----------------------------------------------------------------------
    // Req 16.3 — Parent не содержит административных разрешений
    // -----------------------------------------------------------------------

    /// <summary>
    /// Проверяет, что коллекция разрешений роли Parent не содержит ни одного
    /// из разрешений, предназначенных исключительно для администраторов.
    /// </summary>
    [Theory]
    [InlineData(Permission.AdminUsersRead)]
    [InlineData(Permission.AdminUsersWrite)]
    [InlineData(Permission.AdminRolesWrite)]
    [InlineData(Permission.AuditRead)]
    [InlineData(Permission.SyncRead)]
    [InlineData(Permission.FaqWrite)]
    public void GetPermissions_Parent_DoesNotContainAdminOnlyPermissions(string adminOnlyPermission)
    {
        // ARRANGE
        var parentPermissions = _sut.GetPermissions(UserRole.Parent);

        // ACT & ASSERT
        Assert.DoesNotContain(adminOnlyPermission, parentPermissions);
    }

    /// <summary>
    /// Проверяет, что коллекция разрешений роли Doctor не содержит ни одного
    /// из разрешений, предназначенных исключительно для администраторов.
    /// </summary>
    [Theory]
    [InlineData(Permission.AdminUsersRead)]
    [InlineData(Permission.AdminUsersWrite)]
    [InlineData(Permission.AdminRolesWrite)]
    [InlineData(Permission.AuditRead)]
    [InlineData(Permission.SyncRead)]
    [InlineData(Permission.FaqWrite)]
    public void GetPermissions_Doctor_DoesNotContainAdminOnlyPermissions(string adminOnlyPermission)
    {
        // ARRANGE
        var doctorPermissions = _sut.GetPermissions(UserRole.Doctor);

        // ACT & ASSERT
        Assert.DoesNotContain(adminOnlyPermission, doctorPermissions);
    }
}
