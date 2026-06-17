namespace SugarGuard.API.Exceptions;

/// <summary>
/// Исключение для ошибок валидации уровня глюкозы
/// </summary>
public class GlucoseValidationException : Exception
{
    public GlucoseValidationException() : base()
    {
    }

    public GlucoseValidationException(string message) : base(message)
    {
    }

    public GlucoseValidationException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}
