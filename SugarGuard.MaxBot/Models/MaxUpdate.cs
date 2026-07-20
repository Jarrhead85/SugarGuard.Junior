using System.Text.Json.Serialization;

namespace SugarGuard.MaxBot.Models;

/// <summary>Событие, которое MAX доставляет на защищённый адрес бота.</summary>
public sealed class MaxUpdate
{
    [JsonPropertyName("update_type")] public string? UpdateType { get; init; }
    [JsonPropertyName("user")] public MaxUser? User { get; init; }
    [JsonPropertyName("message")] public MaxMessage? Message { get; init; }
}

/// <summary>Пользователь MAX.</summary>
public sealed class MaxUser
{
    [JsonPropertyName("user_id")] public long UserId { get; init; }
    [JsonPropertyName("username")] public string? Username { get; init; }
}

/// <summary>Сообщение MAX, необходимое для обработки команд.</summary>
public sealed class MaxMessage
{
    [JsonPropertyName("sender")] public MaxUser? Sender { get; init; }
    [JsonPropertyName("body")] public MaxMessageBody? Body { get; init; }
}

/// <summary>Текстовое содержимое сообщения MAX.</summary>
public sealed class MaxMessageBody
{
    [JsonPropertyName("text")] public string? Text { get; init; }
}
