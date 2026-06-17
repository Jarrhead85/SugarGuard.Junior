namespace SugarGuard.API.Exceptions;

/// <summary>
/// Исключение для таймаутов при обращении к GigaChat API
/// </summary>
public class GigaChatTimeoutException : Exception
{
    public GigaChatTimeoutException() : base()
    {
    }

    public GigaChatTimeoutException(string message) : base(message)
    {
    }

    public GigaChatTimeoutException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}
