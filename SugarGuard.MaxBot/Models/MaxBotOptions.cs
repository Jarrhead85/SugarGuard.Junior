namespace SugarGuard.MaxBot.Models;

/// <summary>Настройки MAX-бота, получаемые только из защищённой конфигурации сервера.</summary>
public sealed class MaxBotOptions
{
    public const string SectionName = "Max";

    public string? BotToken { get; init; }
    public string? WebhookUrl { get; init; }
    public string? WebhookSecret { get; init; }
    public string? PublicBotUrl { get; init; }
}
