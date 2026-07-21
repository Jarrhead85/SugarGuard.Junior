namespace SugarGuard.Domain.Enums;

public enum UserRole
{
    Parent = 0,
    Doctor = 1,
    Admin = 2,
    SupportAdmin = 3,
    ChildDevice = 4,
    ServiceAccount = 5,
    /// <summary>
    /// Учётная запись врача, ожидающая проверки документов администратором.
    /// Такая роль не даёт доступ к данным пациентов.
    /// </summary>
    DoctorPending = 6
}
