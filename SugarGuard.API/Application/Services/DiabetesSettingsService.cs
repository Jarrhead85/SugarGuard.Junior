using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.API.DTOs;
using SugarGuard.Application.Audit;
using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Реализация сервиса настроек
/// </summary>
public sealed class DiabetesSettingsService : IDiabetesSettingsService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IAuditService _audit;

    public DiabetesSettingsService(
        IDbContextFactory<AppDbContext> dbFactory,
        IAuditService audit)
    {
        _dbFactory = dbFactory;
        _audit = audit;
    }

    /// <inheritdoc/>
    public async Task<DiabetesSettingsResponse?> GetAsync(
        Guid childId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var settings = await db.DiabetesSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ChildId == childId, cancellationToken);

        return settings is null ? null : MapToResponse(settings);
    }

    /// <inheritdoc/>
    public async Task<DiabetesSettingsResponse?> UpsertAsync(
        Guid childId,
        UpdateDiabetesSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        // Проверяем существование ребёнка
        var childExists = await db.Children
            .AsNoTracking()
            .AnyAsync(c => c.ChildId == childId, cancellationToken);

        if (!childExists)
            return null;

        var existing = await db.DiabetesSettings
            .FirstOrDefaultAsync(s => s.ChildId == childId, cancellationToken);

        DiabetesSettings entity;
        bool isCreated;

        if (existing is null)
        {
            entity = new DiabetesSettings
            {
                ChildId = childId,
                TargetRangeMin = request.TargetRangeMin,
                TargetRangeMax = request.TargetRangeMax,
                InsulinSensitivity = request.InsulinSensitivity,
                CarbInsulinRatio = request.CarbInsulinRatio,
                UpdatedAt = DateTime.UtcNow
            };
            db.DiabetesSettings.Add(entity);
            isCreated = true;
        }
        else
        {
            existing.TargetRangeMin = request.TargetRangeMin;
            existing.TargetRangeMax = request.TargetRangeMax;
            existing.InsulinSensitivity = request.InsulinSensitivity;
            existing.CarbInsulinRatio = request.CarbInsulinRatio;
            existing.UpdatedAt = DateTime.UtcNow;
            entity = existing;
            isCreated = false;
        }

        await db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            action: isCreated ? "diabetes_settings.created" : "diabetes_settings.updated",
            targetType: "DiabetesSettings",
            targetId: childId.ToString(),
            details:
                $"Target=[{entity.TargetRangeMin:F1};{entity.TargetRangeMax:F1}];"
                + $"IS={entity.InsulinSensitivity:F2};CR={entity.CarbInsulinRatio:F2}",
            cancellationToken: CancellationToken.None);

        return MapToResponse(entity);
    }

    private static DiabetesSettingsResponse MapToResponse(DiabetesSettings s) => new()
    {
        ChildId = s.ChildId,
        TargetRangeMin = s.TargetRangeMin,
        TargetRangeMax = s.TargetRangeMax,
        InsulinSensitivity = s.InsulinSensitivity,
        CarbInsulinRatio = s.CarbInsulinRatio,
        UpdatedAt = s.UpdatedAt
    };
}
