// SugarGuard.Web/ViewModels/MeasurementVm.cs
namespace SugarGuard.Web.ViewModels;

/// <summary>UI-модель измерения глюкозы.</summary>
public sealed class MeasurementVm
{
    public Guid      MeasurementId   { get; init; }
    public Guid      ChildId         { get; init; }
    /// <summary>Значение глюкозы (ммоль/л).</summary>
    public decimal   GlucoseValue    { get; init; }
    public DateTime  MeasurementTime { get; init; }
    public DateTime  MeasuredAt      => MeasurementTime;
    /// <summary>UI-состояние: Normal, Warning, Danger, Critical.</summary>
    public string?   GlucoseUiState  { get; init; }
    public bool      IsCritical      { get; init; }
    /// <summary>cgm, manual, import.</summary>
    public string?   DataSource      { get; init; }
    public string?   ChildState      { get; init; }
    public string?   Notes           { get; init; }
}
