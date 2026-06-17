using System;

namespace SugarGuard.Web.ViewModels;

/// <summary>
/// ViewModel элемента ленты событий родительского дашборда.
/// </summary>
public sealed class TimelineItemVm
{
	/// <summary>
	/// Идентификатор события.
	/// </summary>
	public Guid EventId { get; init; }

	/// <summary>
	/// Тип события.
	/// Возможные значения: Measurement, SnackConsumed, CriticalAlert, DoctorNote.
	/// </summary>
	public string EventType { get; init; } = string.Empty;

	/// <summary>
	/// UTC-время, когда произошло событие.
	/// </summary>
	public DateTime OccurredAt { get; init; }

	/// <summary>
	/// Значение глюкозы, если событие связано с измерением или критическим алертом.
	/// </summary>
	public decimal? GlucoseValue { get; init; }

	/// <summary>
	/// UI-состояние глюкозы для отображения в интерфейсе.
	/// Например: Normal, Warning, Danger.
	/// </summary>
	public string? GlucoseUiState { get; init; }

	/// <summary>
	/// Источник данных измерения.
	/// Например: manual, mobileapp, cgm.
	/// </summary>
	public string? DataSource { get; init; }

	/// <summary>
	/// Название перекуса, если событие относится к потреблению перекуса.
	/// </summary>
	public string? SnackName { get; init; }

	/// <summary>
	/// Количество хлебных единиц, если событие относится к перекусу.
	/// </summary>
	public decimal? BreadUnits { get; init; }

	/// <summary>
	/// Дополнительные заметки, комментарий или текст алерта.
	/// </summary>
	public string? Notes { get; init; }

	/// <summary>
	/// Признак важного события.
	/// </summary>
	public bool IsImportant { get; init; }
}