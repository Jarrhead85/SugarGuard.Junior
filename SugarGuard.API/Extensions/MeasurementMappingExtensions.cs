using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.Domain.Entities;
using SugarGuard.Application.Glucose;

namespace SugarGuard.API.Extensions;

/// <summary>
/// Extension methods for mapping Measurement entities to response DTOs
/// </summary>
internal static class MeasurementMappingExtensions
{
    /// <summary>
    /// Maps a Measurement entity to MeasurementResponse with glucose status information
    /// </summary>
    public static MeasurementResponse ToResponse(
        this Measurement m,
        IGlucoseStatusService statusSvc,
        IGlucoseUiStateService uiSvc) => new()
    {
        MeasurementId = m.MeasurementId,
        ChildId = m.ChildId,
        GlucoseValue = m.GlucoseValue,
        MeasurementTime = m.MeasurementTime,
        ChildState = m.ChildState,
        Notes = m.Notes,
        DataSource = m.DataSource,
        RecommendationId = m.RecommendationId,
        CreatedAt = m.CreatedAt,
        GlucoseStatus = statusSvc.GetGlucoseStatus(m.GlucoseValue),
        IsCritical = statusSvc.IsCritical(m.GlucoseValue),
        GlucoseUiState = uiSvc.Resolve(m.GlucoseValue).ToString()
    };
}
