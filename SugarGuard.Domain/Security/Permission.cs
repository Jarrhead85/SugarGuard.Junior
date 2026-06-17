namespace SugarGuard.Domain.Security;

public static class Permission
{
    public const string ChildRead = "child:read";
    public const string ChildWrite = "child:write";
    public const string DashboardRead = "dashboard:read";
    public const string ExportRead = "export:read";
    public const string ExportWrite = "export:write";
    public const string SyncRead = "sync:read";
    public const string FaqRead = "faq:read";
    public const string FaqWrite = "faq:write";
    public const string AdminUsersRead = "admin:users:read";
    public const string AdminUsersWrite = "admin:users:write";
    public const string AdminRolesWrite = "admin:roles:write";
    public const string AuditRead = "audit:read";
}
