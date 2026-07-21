namespace SugarGuard.Domain.Enums;

/// <summary>
/// Состояние проверки документов кандидата в врачи.
/// </summary>
public enum DoctorVerificationStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3
}
