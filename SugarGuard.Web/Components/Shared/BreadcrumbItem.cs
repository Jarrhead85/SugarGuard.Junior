namespace SugarGuard.Web.Components.Shared;

/// <summary>
/// Элемент навигационной цепочки breadcrumb
/// </summary>
public sealed record BreadcrumbItem(string Label, string? Href = null);
