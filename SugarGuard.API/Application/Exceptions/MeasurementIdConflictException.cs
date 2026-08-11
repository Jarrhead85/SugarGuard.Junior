namespace SugarGuard.API.Application.Exceptions;

/// <summary>
/// Raised when a client-supplied idempotency identifier is already owned by
/// another child. The exception deliberately carries no foreign record data.
/// </summary>
public sealed class MeasurementIdConflictException : Exception
{
    public MeasurementIdConflictException()
        : base("The measurement id cannot be used for this child.")
    {
    }
}
