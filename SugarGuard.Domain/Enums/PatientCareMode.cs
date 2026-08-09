namespace SugarGuard.Domain.Enums;

/// <summary>
/// Determines who manages a patient profile and which workflows clients display.
/// </summary>
public enum PatientCareMode
{
    /// <summary>Profile of a child managed by a parent or legal guardian.</summary>
    ChildWithGuardian = 0,

    /// <summary>Self-managed profile of an adult patient.</summary>
    SelfManaged = 1
}
