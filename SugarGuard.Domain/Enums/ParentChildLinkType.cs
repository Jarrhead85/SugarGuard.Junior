namespace SugarGuard.Domain.Enums;

/// <summary>
/// Назначение связи пользователя и ребёнка.
/// </summary>
public enum ParentChildLinkType
{
    /// <summary>Обычная связь родителя и ребёнка.</summary>
    Regular = 0,

    /// <summary>Техническая self-link связь учётной записи детского устройства.</summary>
    SelfLinkChildDevice = 1
}
