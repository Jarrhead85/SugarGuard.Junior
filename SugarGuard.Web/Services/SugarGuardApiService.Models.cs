using SugarGuard.Domain.Enums;
using SugarGuard.Web.ViewModels;

namespace SugarGuard.Web.Services;

// VIEW MODELS
/// <summary>
/// Сводка дашборда для текущего ребёнка
/// </summary>
public sealed class DashboardSummaryVm
{
    public Guid      ChildId               { get; init; }
    public decimal?  LatestGlucose         { get; init; }
    public DateTime? LatestMeasurementTime  { get; init; }
    public string?   LatestGlucoseStatus   { get; init; }
    public string?   LatestGlucoseUiState  { get; init; }
    public int       TotalMeasurements     { get; init; }
    public int       CriticalEpisodes      { get; init; }
    public int       RecommendationsCount  { get; init; }
    public int       PendingExportJobs     { get; init; }
    public int       PendingSyncConflicts  { get; init; }

    /// <summary>
    /// Демо-заглушка для режима Ui:DemoModeEnabled
    /// </summary>
    public static DashboardSummaryVm Sample { get; } = new()
    {
        ChildId               = Guid.Empty,
        LatestGlucose         = 5.6m,
        LatestGlucoseStatus   = "Normal",
        LatestGlucoseUiState  = "Normal",
        TotalMeasurements     = 42,
        CriticalEpisodes      = 1,
        RecommendationsCount  = 3,
        PendingExportJobs     = 0,
        PendingSyncConflicts  = 0,
        LatestMeasurementTime = DateTime.UtcNow.AddMinutes(-15)
    };
}

/// <summary>
/// Профиль ребёнка
/// </summary>
public sealed class ChildProfileVm
{
    public Guid     ChildId      { get; init; }
    public string   FirstName    { get; init; } = string.Empty;
    public string   LastName     { get; init; } = string.Empty;
    public DateOnly DateOfBirth  { get; init; }
    public string   DiabetesType { get; init; } = string.Empty;
    public string?  PhotoUrl     { get; init; }
    public string   FullName     => $"{FirstName} {LastName}".Trim();
}

/// <summary>
/// Событие таймлайна (измерение, перекус, критическое событие, заметка)
/// </summary>
public sealed class TimelineEventDto
{
    public Guid             EventId        { get; init; }
    public TimelineEventType EventType     { get; init; }
    public DateTime          OccurredAt   { get; init; }
    public string?           Title        { get; init; }
    public string?           Description  { get; init; }
    public decimal?          GlucoseValue { get; init; }
    public string?           GlucoseUiState { get; init; }
    public bool              IsImportant  { get; init; }
    public string?           DataSource   { get; init; }
    public string?           SnackName    { get; init; }
    public decimal?          BreadUnits   { get; init; }
    public string?           Notes        { get; init; }
}

/// <summary>
/// Тип события в таймлайне
/// </summary>
public enum TimelineEventType
{
    GlucoseMeasurement,
    Meal,
    Alert,
    Note,
    Sync
}

/// <summary>
/// Настройки диабета ребёнка
/// </summary>
public sealed class DiabetesSettingsVm
{
    public decimal  TargetGlucoseMin   { get; init; }
    public decimal  TargetGlucoseMax   { get; init; }
    public decimal  InsulinToCarbRatio { get; init; }
}

/// <summary>
/// Задание на экспорт
/// </summary>
public sealed class ExportJobVm
{
    public Guid      ExportJobId  { get; init; }
    public Guid      ChildId      { get; init; }
    public DateTime  PeriodFrom   { get; init; }
    public DateTime  PeriodTo     { get; init; }
    public string    Format       { get; init; } = string.Empty;
    public string    Status       { get; init; } = string.Empty;
    public string?   DownloadUrl  { get; init; }
    public DateTime  CreatedAt    { get; init; }
    public DateTime? CompletedAt  { get; init; }
}

/// <summary>
/// Статья FAQ
/// </summary>
public sealed class FaqVm
{
    public Guid      FaqId     { get; init; }
    public string    Title     { get; init; } = string.Empty;
    public string    Content   { get; init; } = string.Empty;
    public string    Category  { get; init; } = string.Empty;
    public int       SortOrder { get; init; }
    public DateTime  UpdatedAt { get; init; }
}

/// <summary>
/// Краткое резюме пациента для врача
/// </summary>
public sealed class DoctorPatientSummaryVm
{
    public DoctorPatientSummaryVm(
        Guid childId, string firstName, string lastName,
        string diabetesType, DateOnly dateOfBirth,
        decimal? latestGlucose, DateTime? latestMeasurementTime,
        string? latestGlucoseUiState,
        double timeInTargetRange,
        int criticalEventsLast7Days, int measurementsLast7Days)
    {
        ChildId                = childId;
        FirstName              = firstName;
        LastName               = lastName;
        DiabetesType           = diabetesType;
        DateOfBirth            = dateOfBirth;
        LatestGlucose          = latestGlucose;
        LatestMeasurementTime  = latestMeasurementTime;
        LatestGlucoseUiState   = latestGlucoseUiState;
        TimeInTargetRange      = timeInTargetRange;
        CriticalEventsLast7Days = criticalEventsLast7Days;
        MeasurementsLast7Days  = measurementsLast7Days;
    }

    public Guid      ChildId                { get; }
    public string    FirstName              { get; }
    public string    LastName               { get; }
    public string    DiabetesType           { get; }
    public DateOnly  DateOfBirth            { get; }
    public decimal?  LatestGlucose          { get; }
    public DateTime? LatestMeasurementTime  { get; }
    public string?   LatestGlucoseUiState   { get; }
    public double    TimeInTargetRange      { get; }
    public int       CriticalEventsLast7Days { get; }
    public int       MeasurementsLast7Days  { get; }
    public string    FullName               => $"{FirstName} {LastName}".Trim();
}

/// <summary>
/// Health-check публичный (GET /api/health)
/// </summary>
public sealed class HealthVm
{
    public string  Status         { get; init; } = string.Empty;
    public string? DatabaseStatus { get; init; }
    public DateTime CheckedAt     { get; init; }
}

/// <summary>
/// Постраничный результат
/// </summary>
public sealed class PagedResult<T>
{
    public List<T> Items      { get; init; } = new();
    public int     TotalCount { get; init; }
    public int     Page       { get; init; }
    public int     PageSize   { get; init; }
    public int     TotalPages     => PageSize > 0
        ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPreviousPage   => Page > 1;
    public bool HasNextPage       => Page < TotalPages;
}

public sealed class ClaimInviteCodeVm
{
    public bool    Success   { get; init; }
    public Guid    ChildId   { get; init; }
    public Guid    LinkId    { get; init; }
    public string  LinkType  { get; init; } = string.Empty;
    public string? ErrorCode { get; init; }
    public string? ErrorMessage => ErrorCode switch
    {
        "unauthorized"  => "Необходимо войти в систему.",
        "codenotfound"  => "Код не найден. Проверьте правильность ввода.",
        "expired"       => "Срок действия кода истёк.",
        "alreadyused"   => "Код уже был использован.",
        "rolemismatch"  => "Ваша роль не совпадает с типом кода.",
        _ when ErrorCode is not null => "Произошла ошибка при активации кода.",
        _ => null
    };
}

// INTERNAL ADMIN DTOs

internal sealed class AdminUserResponseDto
{
    public Guid     UserId        { get; init; }
    public string   EmailForLogin { get; init; } = string.Empty;
    public long?    TelegramId    { get; init; }
    public string   Role          { get; init; } = string.Empty;
    public bool     IsActive      { get; init; }
    public DateTime CreatedAt     { get; init; }
}

internal sealed class AdminSystemStatsDto
{
    public int      HangfireActiveJobs       { get; init; }
    public int      TotalUsers               { get; init; }
    public int      TotalChildren            { get; init; }
    public long     TotalMeasurements        { get; init; }
    public int      PendingSyncItems         { get; init; }
    public int      UnresolvedConflicts      { get; init; }
    public int      PendingExportJobs        { get; init; }
    public int      CompletedExportJobsToday { get; init; }
    public DateTime ServerUtcTime            { get; init; }
}

internal sealed class AdminHealthDto
{
    public string?   Status    { get; init; }
    public bool      Database  { get; init; }
    public DateTime? ServerUtc { get; init; }
}

internal sealed class ChildAccessLinksApiDto
{
    public Guid ChildId { get; init; }
    public List<LinkedAccessUserApiDto> ParentLinks { get; init; } = [];
    public List<LinkedAccessUserApiDto> DoctorLinks { get; init; } = [];
}

internal sealed class LinkedAccessUserApiDto
{
    public Guid LinkId { get; init; }
    public Guid UserId { get; init; }
    public string? EmailForLogin { get; init; }
    public long? TelegramId { get; init; }
    public string UserRole { get; init; } = string.Empty;
    public DateTime LinkedAt { get; init; }
}

// REQUEST MODELS
/// <summary>
/// Добавление перекуса в рюкзак
/// </summary>
public sealed class AddBackpackItemRequest
{
    public Guid    ChildId    { get; init; }
    public string  SnackName  { get; init; } = string.Empty;
    public decimal BreadUnits { get; init; }
}

/// <summary>
/// Обновление настроек диабета
/// </summary>
public sealed class UpdateDiabetesSettingsRequest
{
    public decimal  TargetGlucoseMin   { get; set; }
    public decimal  TargetGlucoseMax   { get; set; }
    public decimal  InsulinToCarbRatio { get; set; }
}

/// <summary>
/// Создание задания на экспорт
/// </summary>
public sealed class CreateExportJobRequest
{
    public Guid      ChildId    { get; init; }
    public string    Period     { get; init; } = "month";
    public string?   Format     { get; init; }
    public DateTime? PeriodFrom { get; init; }
    public DateTime? PeriodTo   { get; init; }
}

/// <summary>
/// Генерация инвайт-кода
/// </summary>
public sealed class GenerateInviteCodeRequest
{
    public Guid     ChildId    { get; init; }
    public UserRole TargetRole { get; init; }
}

/// <summary>
/// Активация инвайт-кода
/// </summary>
public sealed class ClaimInviteCodeRequest
{
    public string Code { get; init; } = string.Empty;
}

/// <summary>
/// Создание заметки врача
/// </summary>
public sealed class CreateDoctorNoteVmRequest
{
    public Guid   ChildId       { get; init; }
    public Guid?  MeasurementId { get; init; }
    public string NoteText      { get; init; } = string.Empty;
    public bool   IsImportant   { get; init; }
}

/// <summary>
/// Обновление заметки врача
/// </summary>
public sealed class UpdateDoctorNoteVmRequest
{
    public string NoteText    { get; init; } = string.Empty;
    public bool   IsImportant { get; init; }
}
