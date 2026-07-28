namespace SugarGuard.API.DTOs;

public enum TelegramOutboxDeliveryPart
{
    Text,
    Location
}

public sealed class TelegramOutboxAcknowledgementRequest
{
    public long TelegramUserId { get; init; }
}

/// <summary>
/// Сообщение, полученное Telegram-ботом из очереди доставки.
/// </summary>
public sealed class TelegramOutboxMessageResponse
{
    public Guid MessageId { get; init; }
    public long TelegramUserId { get; init; }
    public string MessageType { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public bool RequiresAcknowledgement { get; init; }
    public bool TextDelivered { get; init; }
    public bool LocationDelivered { get; init; }
}

/// <summary>
/// Результат отправки сообщения ботом.
/// </summary>
public sealed class TelegramOutboxDeliveryRequest
{
    public bool Delivered { get; init; }
    public string? Error { get; init; }
}
