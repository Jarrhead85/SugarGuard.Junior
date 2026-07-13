using Microsoft.Extensions.Configuration;
using SugarGuard.Domain.Enums;
using SugarGuard.Shared.Constants;
using SugarGuard.Shared.Dto;
using SugarGuard.Web.Models.Analytics;
using SugarGuard.Web.ViewModels;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SugarGuard.Web.Services
{
    /// <summary>
    /// HTTP-клиент 
    /// </summary>
    public sealed partial class SugarGuardApiService
    {
        // Инфраструктура
        private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ITokenStore _tokenStore;

        /// <summary>
        /// Конструктор для DI
        /// </summary>
        public SugarGuardApiService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ITokenStore tokenStore)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _tokenStore = tokenStore;
        }

        // Вспомогательные свойства 
        /// <summary>
        /// true, если в конфигурации включён demo-режим
        /// </summary>
        private bool IsDemoModeEnabled =>
            _configuration.GetValue<bool>("UiDemoModeEnabled");

        // Фабричные методы
        private HttpClient CreateClient() =>
            _httpClientFactory.CreateClient("SugarGuardApi");

        private async Task<HttpClient> CreateAuthorizedClientAsync(CancellationToken cancellationToken = default)
        {
            var client = CreateClient();
            var token = await _tokenStore.GetTokenAsync();

            if (!string.IsNullOrWhiteSpace(token))
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }

            return client;
        }

        public async Task<AccountProfileVm?> GetAccountProfileAsync(CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            return await client.GetFromJsonAsync<AccountProfileVm>("api/account/profile", _jsonOptions, cancellationToken);
        }

        public async Task<AccountProfileVm> UpdateAccountProfileAsync(
            UpdateAccountProfileVmRequest request,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            using var response = await client.PutAsJsonAsync("api/account/profile", request, _jsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AccountProfileVm>(_jsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("API вернул пустой профиль.");
        }

        public async Task<string> UploadAccountPhotoAsync(
            Stream stream,
            string fileName,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            content.Add(fileContent, "file", fileName);
            using var response = await client.PostAsync("api/account/profile/photo", content, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AccountPhotoUploadApiDto>(_jsonOptions, cancellationToken);
            return result?.PhotoUrl ?? throw new InvalidOperationException("API не вернул адрес фотографии.");
        }

        private sealed class AccountPhotoUploadApiDto
        {
            public string PhotoUrl { get; init; } = string.Empty;
        }

        // Разрешение ChildId
        /// <summary>
        /// Пытается получить ChildId из конфигурации
        /// </summary>
        private bool TryResolveChildId(out Guid childId)
        {
            var raw = _configuration["ApiDemoChildId"];
            if (!string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out childId))
                return true;

            childId = Guid.Empty;
            return false;
        }

        // DASHBOARD
        /// <summary>
        /// Возвращает сводку дашборда для текущего ребёнка
        /// </summary>
        public async Task<DashboardSummaryVm> GetDashboardSummaryAsync(
            CancellationToken cancellationToken = default)
        {
            if (!TryResolveChildId(out var childId))
            {
                throw new InvalidOperationException(
                    "ChildId не удалось определить. Проверьте ApiDemoChildId в конфигурации.");
            }

            return await GetDashboardSummaryAsync(childId, cancellationToken);
        }

        /// <summary>
        /// GET api/dashboard/{childId}/summary
        /// </summary>
        public async Task<DashboardSummaryVm> GetDashboardSummaryAsync(
            Guid childId,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var url = $"api/dashboard/{childId}/summary";
            var raw = await GetRequiredAsync<DashboardSummaryApiDto>(client, url, cancellationToken);
            return MapDashboardSummary(raw);
        }

        // MEASUREMENTS
        /// <summary>
        /// GET api/measurements/{childId}?from=...&amp;to=...
        /// </summary>
        public async Task<List<MeasurementVm>> GetMeasurementsAsync(
            Guid childId,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var url = BuildUrl($"api/measurements/{childId}", ("from", from.ToString("o")), ("to", to.ToString("o")));
            return await GetRequiredAsync<List<MeasurementVm>>(client, url, cancellationToken);
        }

        // TIMELINE
        /// <summary>
        /// GET api/parentdashboard/{childId}/timeline?from=...&amp;to=...
        /// </summary>
        public async Task<List<TimelineEventDto>> GetTimelineAsync(
            Guid childId,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var url = BuildUrl(
                $"api/parentdashboard/{childId}/timeline",
                ("from", from.ToString("o")),
                ("to", to.ToString("o")));

            var raw = await GetRequiredAsync<List<TimelineEventApiDto>>(client, url, cancellationToken);
            return raw.Select(MapTimelineEvent).ToList();
        }

        // STATISTICS
        /// <summary>
        /// Возвращает статистику
        /// </summary>
        public async Task<StatisticsVm?> GetStatisticsVmAsync(
            Guid childId,
            string period,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var client = await CreateAuthorizedClientAsync(cancellationToken);
                var (from, to) = ResolveStatisticsPeriod(period);
                var url = BuildUrl(
                    $"api/parentdashboard/{childId}/statistics",
                    ("from", from.ToString("o")),
                    ("to", to.ToString("o")));
                using var response = await client.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                    return null;

                var raw = await ReadOptionalAsync<StatisticsApiDto>(response.Content, cancellationToken);
                return raw is null ? null : MapStatistics(raw);
            }
            catch (Exception ex)
                when (ex is HttpRequestException
                    or TaskCanceledException
                    or JsonException
                    or InvalidOperationException)
            {
                return null;
            }
        }

        public async Task<List<NutritionEntryVm>> GetNutritionEntriesAsync(Guid childId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var url = BuildUrl($"api/children/{childId}/nutrition/entries", ("from", from.ToUniversalTime().ToString("O")), ("to", to.ToUniversalTime().ToString("O")));
            return await GetRequiredAsync<List<NutritionEntryVm>>(client, url, cancellationToken);
        }

        public async Task<NutritionEntryVm> SaveNutritionEntryAsync(Guid childId, Guid? entryId, SaveNutritionEntryVm request, CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var url = entryId.HasValue ? $"api/children/{childId}/nutrition/entries/{entryId}" : $"api/children/{childId}/nutrition/entries";
            using var response = entryId.HasValue
                ? await client.PutAsJsonAsync(url, request, _jsonOptions, cancellationToken)
                : await client.PostAsJsonAsync(url, request, _jsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<NutritionEntryVm>(_jsonOptions, cancellationToken) ?? throw new InvalidOperationException("API вернул пустую запись дневника.");
        }

        public async Task<bool> DeleteNutritionEntryAsync(Guid childId, Guid entryId, CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            using var response = await client.DeleteAsync($"api/children/{childId}/nutrition/entries/{entryId}", cancellationToken);
            return response.IsSuccessStatusCode;
        }

        public async Task<List<MealScheduleVm>> GetMealScheduleAsync(Guid childId, CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            return await GetRequiredAsync<List<MealScheduleVm>>(client, $"api/children/{childId}/nutrition/schedule", cancellationToken);
        }

        public async Task<MealScheduleVm> SaveMealScheduleAsync(Guid childId, Guid? scheduleId, MealScheduleVm request, CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var url = scheduleId.HasValue ? $"api/children/{childId}/nutrition/schedule/{scheduleId}" : $"api/children/{childId}/nutrition/schedule";
            using var response = scheduleId.HasValue
                ? await client.PutAsJsonAsync(url, request, _jsonOptions, cancellationToken)
                : await client.PostAsJsonAsync(url, request, _jsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<MealScheduleVm>(_jsonOptions, cancellationToken) ?? throw new InvalidOperationException("API вернул пустое расписание.");
        }

        public async Task<bool> DeleteMealScheduleAsync(Guid childId, Guid scheduleId, CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            using var response = await client.DeleteAsync($"api/children/{childId}/nutrition/schedule/{scheduleId}", cancellationToken);
            return response.IsSuccessStatusCode;
        }

        public async Task<NutritionSummaryVm> GetNutritionSummaryAsync(Guid childId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var url = BuildUrl($"api/children/{childId}/nutrition/summary", ("from", from.ToUniversalTime().ToString("O")), ("to", to.ToUniversalTime().ToString("O")));
            return await GetRequiredAsync<NutritionSummaryVm>(client, url, cancellationToken);
        }

        public async Task<byte[]> ExportNutritionAsync(Guid childId, DateTime from, DateTime to, string format, CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var safeFormat = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase) ? "csv" : "pdf";
            var url = BuildUrl($"api/children/{childId}/nutrition/export.{safeFormat}", ("from", from.ToUniversalTime().ToString("O")), ("to", to.ToUniversalTime().ToString("O")));
            using var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        private static (DateTime From, DateTime To) ResolveStatisticsPeriod(string period)
        {
            var to = DateTime.UtcNow;
            var normalized = period.Trim().ToLowerInvariant();
            var from = normalized switch
            {
                "24h" or "day" or "today" => to.AddHours(-24),
                "7d" or "week" => to.AddDays(-7),
                "30d" or "month" => to.AddDays(-30),
                _ => to.AddDays(-7)
            };

            return (from, to);
        }

        // COMPARISON
        /// <summary>
        /// GET api/parentdashboard/{childId}/comparison?period={period}
        /// </summary>
        public async Task<PeriodComparisonVm?> GetComparisonAsync(
            Guid childId,
            string period,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var client = await CreateAuthorizedClientAsync(cancellationToken);
                var url = BuildUrl($"api/parentdashboard/{childId}/comparison", ("period", period));
                using var response = await client.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                    return null;

                var dto = await ReadOptionalAsync<PeriodComparisonRawDto>(response.Content, cancellationToken);
                if (dto is null)
                    return null;

                return new PeriodComparisonVm
                {
                    AverageGlucoseDelta = (double)dto.AverageGlucoseDelta,
                    TimeInRangeDelta = (double)dto.TimeInRangeDelta,
                    HypoEpisodesDelta = dto.HypoEpisodesDelta,
                    HyperEpisodesDelta = dto.HyperEpisodesDelta,
                    IsReliable = dto.IsReliable,
                    UnreliableReason = dto.UnreliableReason
                };
            }
            catch (Exception ex)
                when (ex is HttpRequestException
                    or TaskCanceledException
                    or JsonException
                    or InvalidOperationException)
            {
                return null;
            }
        }

        // PATTERNS
        /// <summary>
        /// GET api/parentdashboard/{childId}/patterns
        /// </summary>
        public async Task<IReadOnlyList<GlucosePatternVm>> GetPatternsAsync(
            Guid childId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var client = await CreateAuthorizedClientAsync(cancellationToken);
                using var response = await client.GetAsync(
                    $"api/parentdashboard/{childId}/patterns", cancellationToken);

                if (!response.IsSuccessStatusCode)
                    return [];

                var raw = await ReadOptionalAsync<List<GlucosePatternDto>>(response.Content, cancellationToken);
                if (raw is null)
                    return [];

                return raw.Select(dto => new GlucosePatternVm
                {
                    PatternType = dto.PatternType,
                    PeakHour = dto.PeakHour,
                    OccurrenceDays = dto.OccurrenceDays,
                    AverageGlucoseInWindow = (double)dto.AverageGlucoseInWindow,
                    Description = dto.Description
                }).ToList();
            }
            catch (Exception ex)
                when (ex is HttpRequestException
                    or TaskCanceledException
                    or JsonException
                    or InvalidOperationException)
            {
                return [];
            }
        }

        // BACKPACK
        /// <summary>
        /// GET api/backpack/{childId}
        /// </summary>
        public async Task<List<BackpackItemVm>> GetBackpackAsync(
            Guid childId,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var response = await GetRequiredAsync<BackpackResponseApiDto>(
                client, $"api/backpack/{childId}", cancellationToken);

            return response.Items;
        }

        /// <summary>
        /// POST api/backpack
        /// </summary>
        public async Task<BackpackItemVm> AddBackpackItemAsync(
            AddBackpackItemRequest request,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var response = await client.PostAsJsonAsync(
                "api/backpack", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content
                .ReadFromJsonAsync<BackpackItemVm>(_jsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("API вернул пустое тело для AddBackpackItem.");
        }

        /// <summary>
        /// PUT api/backpack/{itemId}
        /// </summary>
        public async Task<BackpackItemVm> UpdateBackpackItemAsync(
            Guid itemId,
            UpdateBackpackItemRequest request,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var response = await client.PutAsJsonAsync(
                $"api/backpack/{itemId}", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content
                .ReadFromJsonAsync<BackpackItemVm>(_jsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("API вернул пустое тело для UpdateBackpackItem.");
        }

        /// <summary>
        /// DELETE api/backpack/{itemId}
        /// </summary>
        public async Task DeleteBackpackItemAsync(
            Guid itemId,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var response = await client.DeleteAsync(
                $"api/backpack/{itemId}", cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        // SYNC LOGS
        /// <summary>
        /// GET api/sync-logs — журнал синхронизации
        /// </summary>
        public async Task<IReadOnlyList<SyncLogVm>?> GetSyncLogsAsync(
            Guid? childId = null,
            bool? onlyConflicts = null,
            int limit = 200,
            CancellationToken cancellationToken = default)
        {
            var qs = new List<string> { $"limit={Math.Clamp(limit, 1, 1000)}" };
            if (childId.HasValue) qs.Add($"childId={childId.Value}");
            if (onlyConflicts == true) qs.Add("onlyConflicts=true");
            var url = "api/sync-logs?" + string.Join("&", qs);

            var client = await CreateAuthorizedClientAsync(cancellationToken);
            return await GetRequiredAsync<List<SyncLogVm>>(client, url, cancellationToken);
        }

        /// <summary>
        /// POST api/sync-logs/{id}/resolve — разрешает конфликт по ID
        /// </summary>
        public async Task<SyncLogVm?> ResolveSyncConflictAsync(
            Guid syncLogId,
            string resolution = "useServer",
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            using var response = await client.PostAsJsonAsync(
                $"api/sync-logs/{syncLogId}/resolve",
                new { resolution },
                cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            return await ReadOptionalAsync<SyncLogVm>(response.Content, cancellationToken);
        }

        /// <summary>
        /// POST api/sync-logs/resolve-all — разрешает все конфликты пользователя
        /// </summary>
        public async Task<int> ResolveAllSyncConflictsAsync(
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            using var response = await client.PostAsync(
                "api/sync-logs/resolve-all",
                content: null,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return 0;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(body)) return 0;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("resolvedCount", out var n) &&
                    n.TryGetInt32(out var count))
                {
                    return count;
                }
            }
            catch (System.Text.Json.JsonException)
            {
            }
            return 0;
        }

        // EXPORT JOBS
        /// <summary>
        /// GET api/export-jobs
        /// </summary>
        public async Task<IReadOnlyList<ExportJobVm>> GetExportJobsAsync(
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            return await GetRequiredAsync<List<ExportJobVm>>(client, "api/export-jobs", cancellationToken);
        }

        /// <summary>
        /// POST api/export-jobs
        /// </summary>
        public async Task<ExportJobVm> CreateExportJobAsync(
            CreateExportJobRequest request,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var response = await client.PostAsJsonAsync("api/export-jobs", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content
                .ReadFromJsonAsync<ExportJobVm>(_jsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("API вернул пустое тело для CreateExportJob.");
        }

        /// <summary>
        /// POST api/measurements/{childId}/export-pdf — возвращает байты PDF-файла
        /// </summary>
        public async Task<byte[]?> ExportStatisticsToPdfAsync(
            Guid childId,
            string period = "day",
            bool detailed = false,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var url = $"api/measurements/{childId}/export-pdf" +
                $"?period={Uri.EscapeDataString(period)}&detailed={(detailed ? "true" : "false")}";

            using var response = await client.PostAsync(url, content: null, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        // DIABETES SETTINGS
        /// <summary>
        /// PUT api/dashboard/{childId}/settings
        /// </summary>
        public async Task UpdateDiabetesSettingsAsync(
            Guid childId,
            UpdateDiabetesSettingsRequest request,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var response = await client.PatchAsJsonAsync(
                $"api/children/{childId}/diabetes-settings",
                new
                {
                    targetRangeMin = request.TargetGlucoseMin,
                    targetRangeMax = request.TargetGlucoseMax,
                    insulinSensitivity = request.InsulinSensitivity,
                    carbInsulinRatio = request.InsulinToCarbRatio
                },
                cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// GET api/dashboard/{childId}/settings
        /// </summary>
        public async Task<DiabetesSettingsVm?> GetDiabetesSettingsAsync(
            Guid childId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var client = await CreateAuthorizedClientAsync(cancellationToken);
                using var response = await client.GetAsync(
                    $"api/children/{childId}/diabetes-settings", cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return null;

                var dto = await ReadOptionalAsync<DiabetesSettingsApiDto>(response.Content, cancellationToken);
                return dto is null ? null : MapDiabetesSettings(dto);
            }
            catch (Exception ex)
                when (ex is HttpRequestException
                    or TaskCanceledException
                    or JsonException
                    or InvalidOperationException)
            {
                return null;
            }
        }

        // CHILDREN CRUD
        /// <summary>
        /// GET api/children
        /// </summary>
        public async Task<List<ChildProfileVm>> GetMyChildrenAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var client = await CreateAuthorizedClientAsync(cancellationToken);
                var page = await GetRequiredAsync<PagedResult<ChildSummaryApiDto>>(
                    client, "api/children", cancellationToken);

                return page.Items
                    .Select(c => new ChildProfileVm
                    {
                        ChildId = c.ChildId,
                        FirstName = c.FirstName ?? string.Empty,
                        LastName = c.LastName ?? string.Empty,
                        DateOfBirth = c.DateOfBirth,
                        DiabetesType = c.DiabetesType ?? string.Empty,
                        PhotoUrl = c.PhotoUrl
                    })
                    .ToList();
            }
            catch (Exception ex)
                when (ex is HttpRequestException
                    or TaskCanceledException
                    or JsonException
                    or InvalidOperationException)
            {
                return new List<ChildProfileVm>();
            }
        }

        /// <summary>
        /// GET api/children/{childId}
        /// </summary>
        public async Task<ChildProfileVm?> GetChildAsync(
            Guid childId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var client = await CreateAuthorizedClientAsync(cancellationToken);
                using var response = await client.GetAsync(
                    $"api/children/{childId}", cancellationToken);

                if (!response.IsSuccessStatusCode)
                    return null;

                var dto = await ReadOptionalAsync<ChildDetailApiDto>(response.Content, cancellationToken);
                return dto is null ? null : MapChildDetail(dto);
            }
            catch (Exception ex)
                when (ex is HttpRequestException
                    or TaskCanceledException
                    or JsonException
                    or InvalidOperationException)
            {
                return null;
            }
        }

        // CHILD PROFILE
        /// <summary>
        /// GET api/parentdashboard/{childId}/profile
        /// </summary>
        public async Task<ChildProfileVm?> GetChildProfileAsync(
            Guid childId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var client = await CreateAuthorizedClientAsync(cancellationToken);
                using var response = await client.GetAsync(
                    $"api/parentdashboard/{childId}/profile", cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return null;

                var dto = await ReadOptionalAsync<ChildProfileApiDto>(response.Content, cancellationToken);
                return dto is null ? null : MapChildProfile(dto);
            }
            catch (Exception ex)
                when (ex is HttpRequestException
                    or TaskCanceledException
                    or JsonException
                    or InvalidOperationException)
            {
                return null;
            }
        }

        // CHILD PHOTO
        /// <summary>
        /// POST api/children/{childId}/photo
        /// </summary>
        public async Task<string?> UploadChildPhotoAsync(
            Guid childId,
            Stream fileStream,
            string fileName,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var client = await CreateAuthorizedClientAsync(cancellationToken);
                using var content = new MultipartFormDataContent();
                var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                content.Add(streamContent, "photo", fileName);

                using var response = await client.PostAsync(
                    $"api/children/{childId:D}/photo", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                    return null;

                var dto = await ReadOptionalAsync<ChildPhotoUploadApiDto>(response.Content, cancellationToken);
                return dto?.PhotoUrl;
            }
            catch (Exception ex)
                when (ex is HttpRequestException
                    or TaskCanceledException
                    or JsonException
                    or InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>
        /// DELETE api/children/{childId}/photo
        /// </summary>
        public async Task<bool> DeleteChildPhotoAsync(
            Guid childId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var client = await CreateAuthorizedClientAsync(cancellationToken);
                using var response = await client.DeleteAsync(
                    $"api/children/{childId:D}/photo", cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
                when (ex is HttpRequestException
                    or TaskCanceledException
                    or JsonException
                    or InvalidOperationException)
            {
                return false;
            }
        }

        private sealed class ChildPhotoUploadApiDto
        {
            public string PhotoUrl { get; set; } = string.Empty;
        }

        // CHILD ACCESS LINKS
        /// <summary>
        /// GET api/invite-codes/{childId}/links
        /// </summary>
        public async Task<ChildAccessLinksVm?> GetChildAccessLinksAsync(
            Guid childId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var client = await CreateAuthorizedClientAsync(cancellationToken);
                using var response = await client.GetAsync(
                    $"api/invite-codes/{childId}/links", cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return null;

                var dto = await ReadOptionalAsync<ChildAccessLinksApiDto>(response.Content, cancellationToken);
                return dto is null ? null : MapChildAccessLinks(dto);
            }
            catch (Exception ex)
                when (ex is HttpRequestException
                    or TaskCanceledException
                    or JsonException
                    or InvalidOperationException)
            {
                return null;
            }
        }

        public async Task<TelegramConnectionCodeVm?> CreateTelegramConnectionCodeAsync(
            Guid childId,
            CancellationToken cancellationToken = default)
        {
            var code = ConnectionCodeFormat.Format(ConnectionCodeFormat.Generate());
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            using var response = await client.PostAsJsonAsync(
                "api/parent-link/code",
                new { childId, code },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<SaveConnectionCodeApiDto>(
                _jsonOptions,
                cancellationToken);

            return result is { Success: true }
                ? new TelegramConnectionCodeVm(code, result.ExpiresAt)
                : null;
        }

        private sealed class SaveConnectionCodeApiDto
        {
            public bool Success { get; init; }
            public DateTime ExpiresAt { get; init; }
        }

        // INVITE CODES
        /// <summary>
        /// GET api/invite-codes/{childId}/active — список активных инвайт-кодов для ребёнка
        /// </summary>
        public async Task<List<InviteCodeVm>> GetInviteCodesAsync(
            Guid childId,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            return await GetRequiredAsync<List<InviteCodeVm>>(
                client, $"api/invite-codes/{childId}/active", cancellationToken);
        }

        /// <summary>
        /// POST api/invite-codes/generate — генерирует новый инвайт-код
        /// </summary>
        public async Task<InviteCodeVm> CreateInviteCodeAsync(
            Guid childId,
            UserRole targetRole,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var response = await client.PostAsJsonAsync(
                "api/invite-codes/generate",
                new { childId, targetRole = (int)targetRole },
                cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content
                .ReadFromJsonAsync<InviteCodeVm>(_jsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("API вернул пустое тело для CreateInviteCode.");
        }

        /// <summary>
        /// POST api/invite-codes/claim — активирует инвайт-код
        /// </summary>
        public async Task<ClaimInviteCodeVm?> ClaimInviteCodeAsync(
            string code,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var token = await _tokenStore.GetTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                return new ClaimInviteCodeVm
                {
                    Success = false,
                    ErrorCode = "unauthorized"
                };
            }

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            using var response = await client.PostAsJsonAsync(
                "api/invite-codes/claim",
                new { code },
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new ClaimInviteCodeVm
                {
                    Success = false,
                    ErrorCode = "unauthorized"
                };
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(body))
            {
                return new ClaimInviteCodeVm
                {
                    Success = false,
                    ErrorCode = "empty_response"
                };
            }

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<ClaimInviteCodeVm>(body,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
            }
            catch (System.Text.Json.JsonException)
            {
                return new ClaimInviteCodeVm
                {
                    Success = false,
                    ErrorCode = "parse_error"
                };
            }
        }

        /// <summary>
        /// POST api/invite-codes/generate — генерирует новый инвайт-код для ребёнка и роли
        /// </summary>
        public async Task<InviteCodeVm?> GenerateInviteCodeAsync(
            Guid childId,
            UserRole targetRole,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            using var response = await client.PostAsJsonAsync(
                "api/invite-codes/generate",
                new { childId, targetRole = (int)targetRole },
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            return await ReadOptionalAsync<InviteCodeVm>(response.Content, cancellationToken);
        }

        /// <summary>
        /// GET api/invite-codes/{childId}/active — список активных кодов для ребёнка
        /// </summary>
        public async Task<IReadOnlyList<InviteCodeVm>> GetActiveInviteCodesAsync(
            Guid childId,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            return await GetRequiredAsync<List<InviteCodeVm>>(
                client, $"api/invite-codes/{childId}/active", cancellationToken);
        }

        /// <summary>
        /// DELETE api/invite-codes/{inviteCodeId} — отзывает инвайт-код
        /// </summary>
        public async Task<bool> RevokeInviteCodeAsync(
            Guid inviteCodeId,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            using var response = await client.DeleteAsync(
                $"api/invite-codes/{inviteCodeId}", cancellationToken);
            return response.IsSuccessStatusCode;
        }

        /// <summary>
        /// DELETE api/invite-codes/{childId}/links/{linkType}/{linkId} — отвязывает родителя или врача от ребёнка
        /// </summary>
        public async Task<bool> UnlinkChildAccessAsync(
            Guid childId,
            string linkType,
            Guid linkId,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            using var response = await client.DeleteAsync(
                $"api/invite-codes/{childId}/links/{linkType}/{linkId}", cancellationToken);
            return response.IsSuccessStatusCode;
        }

        // DOCTOR — пациенты, когорта, заметки
         /// <summary>
        /// GET api/doctor/patients
        /// </summary>
        public async Task<List<DoctorPatientSummaryVm>> GetDoctorPatientsAsync(
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var raw = await GetRequiredAsync<List<DoctorPatientSummaryApiDto>>(
                client, "api/doctor/patients", cancellationToken);
            return raw.Select(MapDoctorPatient).ToList();
        }

        /// <summary>
        /// GET api/doctor/patients?sortBy={sortBy} — сортировка по полю
        /// </summary>
        public async Task<List<DoctorPatientSummaryVm>> GetDoctorPatientsAsync(
            string? sortBy,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var url = string.IsNullOrWhiteSpace(sortBy)
                ? "api/doctor/patients"
                : $"api/doctor/patients?sortBy={Uri.EscapeDataString(sortBy)}";
            var raw = await GetRequiredAsync<List<DoctorPatientSummaryApiDto>>(
                client, url, cancellationToken);
            return raw.Select(MapDoctorPatient).ToList();
        }

        /// <summary>
        /// GET api/doctor/cohort/summary
        /// </summary>
        public async Task<DoctorCohortSummaryVm?> GetDoctorCohortSummaryAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var client = await CreateAuthorizedClientAsync(cancellationToken);
                using var response = await client.GetAsync("api/doctor/cohort/summary", cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return null;

                var dto = await ReadOptionalAsync<DoctorCohortSummaryApiDto>(response.Content, cancellationToken);
                if (dto is null)
                    return null;

                return new DoctorCohortSummaryVm(
                    dto.TotalPatients,
                    dto.PatientsWithCriticalToday,
                    dto.AverageTimeInTargetRange,
                    dto.PatientsWithoutMeasurementsToday,
                    dto.GeneratedAt);
            }
            catch (Exception ex)
                when (ex is HttpRequestException
                    or TaskCanceledException
                    or JsonException
                    or InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>
        /// GET api/doctor/patients/{childId}/notes?page={page}&amp;pageSize={pageSize}&amp;onlyImportant={onlyImportant}
        /// </summary>
        public async Task<PagedResult<DoctorNoteVm>> GetDoctorNotesAsync(
            Guid childId,
            int page = 1,
            int pageSize = 20,
            bool onlyImportant = false,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var urlParams = new List<(string, string)>
            {
                ("page",     page.ToString()),
                ("pageSize", pageSize.ToString())
            };
            if (onlyImportant)
                urlParams.Add(("onlyImportant", "true"));
            var url = BuildUrl($"api/doctor/patients/{childId}/notes", [.. urlParams]);

            var raw = await GetRequiredAsync<PagedDoctorNotesApiDto>(client, url, cancellationToken);
            return new PagedResult<DoctorNoteVm>
            {
                Items = raw.Items.Select(MapDoctorNote).ToList(),
                TotalCount = raw.TotalCount,
                Page = raw.Page,
                PageSize = raw.PageSize
            };
        }

        /// <summary>
        /// POST api/doctor/notes
        /// </summary>
        public async Task<DoctorNoteVm> CreateDoctorNoteAsync(
            Guid childId,
            string noteText,
            bool isImportant = false,
            Guid? measurementId = null,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var response = await client.PostAsJsonAsync(
                "api/doctor/notes",
                new { childId, noteText, isImportant, measurementId },
                cancellationToken);
            response.EnsureSuccessStatusCode();
            var dto = await response.Content
                .ReadFromJsonAsync<DoctorNoteApiDto>(_jsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("API вернул пустое тело для CreateDoctorNote.");
            return MapDoctorNote(dto);
        }

        /// <summary>
        /// POST api/doctor/notes — overload для CreateDoctorNoteVmRequest
        /// </summary>
        public Task<DoctorNoteVm> CreateDoctorNoteAsync(
            CreateDoctorNoteVmRequest request,
            CancellationToken cancellationToken = default)
        {
            return CreateDoctorNoteAsync(
                request.ChildId,
                request.NoteText,
                request.IsImportant,
                request.MeasurementId,
                cancellationToken);
        }

        /// <summary>
        /// PUT api/doctor/notes/{noteId} — обновляет текст и флаг важности
        /// </summary>
        public async Task<DoctorNoteVm?> UpdateDoctorNoteAsync(
            Guid noteId,
            UpdateDoctorNoteVmRequest request,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            using var response = await client.PutAsJsonAsync(
                $"api/doctor/notes/{noteId}",
                new { noteText = request.NoteText, isImportant = request.IsImportant },
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            var dto = await ReadOptionalAsync<DoctorNoteApiDto>(response.Content, cancellationToken);
            return dto is null ? null : MapDoctorNote(dto);
        }

        /// <summary>
        /// DELETE api/doctor/notes/{noteId}
        /// </summary>
        public async Task DeleteDoctorNoteAsync(
            Guid noteId,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var response = await client.DeleteAsync($"api/doctor/notes/{noteId}", cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        // ADMIN — пользователи, системная статистика, здоровье
        /// <summary>
        /// GET api/admin/users-roles/users?role={role}
        /// </summary>
        public async Task<List<AdminUserVm>> GetAdminUsersAsync(
            string? role = null,
            int limit = 500,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var safeLimit = Math.Clamp(limit, 1, 1000);
            var url = string.IsNullOrWhiteSpace(role)
                ? BuildUrl("api/admin/users-roles/users", ("limit", safeLimit.ToString()))
                : BuildUrl("api/admin/users-roles/users",
                    ("role", role),
                    ("limit", safeLimit.ToString()));

            var users = await GetRequiredAsync<List<AdminUserResponseDto>>(client, url, cancellationToken);
            return users.Select(AdminUserVm.FromDto).ToList();
        }

        public async Task<List<AuditLogVm>> GetAdminAuditLogsAsync(
            Guid? actorUserId = null,
            string? action = null,
            DateTime? from = null,
            DateTime? to = null,
            int limit = 200,
            CancellationToken cancellationToken = default)
        {
            var queryParams = new List<(string Key, string Value)>
            {
                ("limit", Math.Clamp(limit, 1, 1000).ToString())
            };

            if (actorUserId.HasValue)
            {
                queryParams.Add(("actorUserId", actorUserId.Value.ToString()));
            }

            if (!string.IsNullOrWhiteSpace(action))
            {
                queryParams.Add(("action", action.Trim()));
            }

            if (from.HasValue)
            {
                queryParams.Add(("from", from.Value.ToUniversalTime().ToString("O")));
            }

            if (to.HasValue)
            {
                queryParams.Add(("to", to.Value.ToUniversalTime().ToString("O")));
            }

            var client = await CreateAuthorizedClientAsync(cancellationToken);
            return await GetRequiredAsync<List<AuditLogVm>>(
                client,
                BuildUrl("api/admin/audit-logs", queryParams.ToArray()),
                cancellationToken);
        }

        /// <summary>
        /// GET api/admin/system/stats
        /// </summary>
        public async Task<AdminSystemStatsVm?> GetAdminSystemStatsAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var client = await CreateAuthorizedClientAsync(cancellationToken);
                using var response = await client.GetAsync("api/admin/system/stats", cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return null;

                var dto = await ReadOptionalAsync<AdminSystemStatsDto>(response.Content, cancellationToken);
                return dto is null ? null : AdminSystemStatsVm.FromDto(dto);
            }
            catch (Exception ex)
                when (ex is HttpRequestException
                    or TaskCanceledException
                    or JsonException
                    or InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>
        /// GET api/admin/system/health
        /// </summary>
        public async Task<AdminHealthVm?> GetAdminHealthAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var client = await CreateAuthorizedClientAsync(cancellationToken);
                using var response = await client.GetAsync("api/admin/system/health", cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return null;

                var dto = await ReadOptionalAsync<AdminHealthDto>(response.Content, cancellationToken);
                return dto is null ? null : AdminHealthVm.FromDto(dto);
            }
            catch (Exception ex)
                when (ex is HttpRequestException
                    or TaskCanceledException
                    or JsonException
                    or InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>
        /// PUT api/children/{childId}
        /// </summary>
        public async Task<ChildProfileVm?> UpdateChildAsync(
            Guid childId,
            UpdateChildProfileRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var client = await CreateAuthorizedClientAsync(cancellationToken);
                using var response = await client.PutAsJsonAsync(
                    $"api/children/{childId:D}",
                    request,
                    _jsonOptions,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                    return null;

                var dto = await ReadOptionalAsync<ChildDetailApiDto>(response.Content, cancellationToken);
                return dto is null ? null : MapChildDetail(dto);
            }
            catch (Exception ex)
                when (ex is HttpRequestException
                    or TaskCanceledException
                    or JsonException
                    or InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>
        /// GET api/admin/system/server-metrics
        /// </summary>
        public async Task<ServerMetricsVm?> GetAdminServerMetricsAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var client = await CreateAuthorizedClientAsync(cancellationToken);
                using var response = await client.GetAsync("api/admin/system/server-metrics", cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return null;

                var dto = await ReadOptionalAsync<ServerMetricsDto>(response.Content, cancellationToken);
                return dto is null ? null : ServerMetricsVm.FromDto(dto);
            }
            catch (Exception ex)
                when (ex is HttpRequestException
                    or TaskCanceledException
                    or JsonException
                    or InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>
        /// GET api/admin/system/gigachat-usage
        /// </summary>
        public async Task<GigaChatUsageVm?> GetAdminGigaChatUsageAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var client = await CreateAuthorizedClientAsync(cancellationToken);
                using var response = await client.GetAsync("api/admin/system/gigachat-usage", cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return null;

                var dto = await ReadOptionalAsync<GigaChatUsageDto>(response.Content, cancellationToken);
                return dto is null ? null : GigaChatUsageVm.FromDto(dto);
            }
            catch (Exception ex)
                when (ex is HttpRequestException
                    or TaskCanceledException
                    or JsonException
                    or InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>
        /// PUT api/admin/users-roles/users/{userId}/role
        /// </summary>
        public async Task UpdateUserRoleAsync(
            Guid userId,
            string role,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var response = await client.PutAsJsonAsync(
                $"api/admin/users-roles/users/{userId}/role",
                new UpdateUserRoleApiRequest { NewRole = role },
                cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// POST api/admin/users-roles/links/parent-child — создаёт связь Parent–Child
        /// </summary>
        public async Task CreateParentChildLinkAsync(
            Guid parentUserId,
            Guid childId,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var response = await client.PostAsJsonAsync(
                "api/admin/users-roles/links/parent-child",
                new { parentUserId, childId },
                cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// POST api/admin/users-roles/links/doctor-child — создаёт связь Doctor–Child
        /// </summary>
        public async Task CreateDoctorChildLinkAsync(
            Guid doctorUserId,
            Guid childId,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var response = await client.PostAsJsonAsync(
                "api/admin/users-roles/links/doctor-child",
                new { doctorUserId, childId },
                cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        // Admin Links CRUD (через /api/admin/users-roles/links/...)

        /// <summary>
        /// POST api/admin/users-roles/links/parent-child — создаёт связь Parent–Child
        /// </summary>
        public async Task CreateAdminParentChildLinkAsync(
            Guid parentUserId,
            Guid childId,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            using var response = await client.PostAsJsonAsync(
                "api/admin/users-roles/links/parent-child",
                new { parentUserId, childId },
                cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// POST api/admin/users-roles/links/doctor-child — создаёт связь Doctor–Child
        /// </summary>
        public async Task CreateAdminDoctorChildLinkAsync(
            Guid doctorUserId,
            Guid childId,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            using var response = await client.PostAsJsonAsync(
                "api/admin/users-roles/links/doctor-child",
                new { doctorUserId, childId },
                cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// DELETE api/admin/users-roles/links/parent-child?parentUserId=…&amp;childId=… — удаляет связь
        /// </summary>
        public async Task<bool> DeleteAdminParentChildLinkAsync(
            Guid parentUserId,
            Guid childId,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var url = BuildUrl("api/admin/users-roles/links/parent-child",
                ("parentUserId", parentUserId.ToString()),
                ("childId", childId.ToString()));
            using var response = await client.DeleteAsync(url, cancellationToken);
            return response.IsSuccessStatusCode;
        }

        /// <summary>
        /// DELETE api/admin/users-roles/links/doctor-child?doctorUserId=…&amp;childId=… — удаляет связь
        /// </summary>
        public async Task<bool> DeleteAdminDoctorChildLinkAsync(
            Guid doctorUserId,
            Guid childId,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var url = BuildUrl("api/admin/users-roles/links/doctor-child",
                ("doctorUserId", doctorUserId.ToString()),
                ("childId", childId.ToString()));
            using var response = await client.DeleteAsync(url, cancellationToken);
            return response.IsSuccessStatusCode;
        }

        /// <summary>
        /// GET api/admin/users-roles/links/parent-child — список всех Parent–Child связей
        /// </summary>
        public async Task<IReadOnlyList<AdminParentChildLinkDto>?> GetAdminParentChildLinksAsync(
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            using var response = await client.GetAsync(
                "api/admin/users-roles/links/parent-child", cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            return await ReadOptionalAsync<List<AdminParentChildLinkDto>>(
                response.Content, cancellationToken);
        }

        /// <summary>
        /// GET api/admin/users-roles/links/doctor-child — список всех Doctor–Child связей
        /// </summary>
        public async Task<IReadOnlyList<AdminDoctorChildLinkDto>?> GetAdminDoctorChildLinksAsync(
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            using var response = await client.GetAsync(
                "api/admin/users-roles/links/doctor-child", cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            return await ReadOptionalAsync<List<AdminDoctorChildLinkDto>>(
                response.Content, cancellationToken);
        }

        // ADMIN — Doctor Requests (users-roles + onboarding funnel)
        /// <summary>
        /// GET api/admin/users-roles/users?limit={limit}. Pending-фильтр выполняет страница.
        /// </summary>
        public async Task<HttpResponseMessage> GetDoctorPendingUsersAsync(
            int limit = 500,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var url = BuildUrl("api/admin/users-roles/users",
                ("limit", limit.ToString()));
            return await client.GetAsync(url, cancellationToken);
        }

        /// <summary>
        /// GET api/admin/onboarding/funnel?role={role}
        /// </summary>
        public async Task<HttpResponseMessage> GetOnboardingFunnelAsync(
            string role,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var url = BuildUrl("api/admin/onboarding/funnel", ("role", role));
            return await client.GetAsync(url, cancellationToken);
        }

        /// <summary>
        /// PUT api/admin/users-roles/users/{userId}/role
        /// </summary>
        public async Task<HttpResponseMessage> UpdateDoctorUserRoleAsync(
            Guid userId,
            string role,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            return await client.PutAsJsonAsync(
                $"api/admin/users-roles/users/{userId}/role",
                new UpdateDoctorUserRoleRequest { NewRole = role },
                cancellationToken);
        }

        // HEALTH CHECK (публичный)
        /// <summary>
        /// GET api/health — публичный health-check
        /// </summary>
        public async Task<(HealthVm? Health, bool IsHealthy, string? ErrorDetail)> GetHealthAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var client = await CreateAuthorizedClientAsync(cancellationToken);
                using var response = await client.GetAsync("api/health", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return (null, false,
                        $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}".Trim());
                }

                var dto = await ReadOptionalAsync<HealthApiDto>(response.Content, cancellationToken);
                if (dto is null)
                {
                    return (null, false, "Пустой или некорректный ответ health-endpoint.");
                }

                var vm = new HealthVm
                {
                    Status = dto.Status ?? string.Empty,
                    DatabaseStatus = dto.DatabaseStatus ?? dto.Database,
                    CheckedAt = dto.CheckedAt ?? dto.TimestampUtc ?? dto.ServerUtc ?? DateTime.UtcNow
                };

                var isOk = string.IsNullOrEmpty(vm.Status)
                    || string.Equals(vm.Status, "ok", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(vm.Status, "healthy", StringComparison.OrdinalIgnoreCase);

                return (vm, isOk, isOk ? null : $"Status={vm.Status}");
            }
            catch (Exception ex)
                when (ex is HttpRequestException
                    or TaskCanceledException
                    or JsonException
                    or InvalidOperationException)
            {
                return (null, false, ex.Message);
            }
        }

        // FAQ
        /// <summary>
        /// GET api/faq-content
        /// </summary>
        public async Task<IReadOnlyList<FaqVm>> GetFaqAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var client = await CreateAuthorizedClientAsync(cancellationToken);
                using var response = await client.GetAsync("api/faq-content", cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return [];

                var raw = await ReadOptionalAsync<List<FaqArticleApiDto>>(response.Content, cancellationToken);
                return raw?.Select(MapFaq).ToList() ?? [];
            }
            catch (Exception ex)
                when (ex is HttpRequestException
                    or TaskCanceledException
                    or JsonException
                    or InvalidOperationException)
            {
                return [];
            }
        }

        /// <summary>
        /// GET api/faq-content/admin
        /// </summary>
        public async Task<IReadOnlyList<FaqVm>> GetAdminFaqAsync(
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            using var response = await client.GetAsync("api/faq-content/admin", cancellationToken);
            response.EnsureSuccessStatusCode();

            var raw = await ReadOptionalAsync<List<FaqArticleApiDto>>(response.Content, cancellationToken);
            return raw?.Select(MapFaq).ToList() ?? [];
        }

        /// <summary>
        /// POST api/faq-content
        /// </summary>
        public async Task<FaqVm> CreateFaqAsync(
            FaqArticleRequestVm request,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            using var response = await client.PostAsJsonAsync("api/faq-content", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var raw = await response.Content.ReadFromJsonAsync<FaqArticleApiDto>(_jsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("API вернул пустое тело для CreateFaq.");
            return MapFaq(raw);
        }

        /// <summary>
        /// PUT api/faq-content/{id}
        /// </summary>
        public async Task<FaqVm> UpdateFaqAsync(
            Guid faqId,
            FaqArticleRequestVm request,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            using var response = await client.PutAsJsonAsync($"api/faq-content/{faqId}", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var raw = await response.Content.ReadFromJsonAsync<FaqArticleApiDto>(_jsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("API вернул пустое тело для UpdateFaq.");
            return MapFaq(raw);
        }

        /// <summary>
        /// DELETE api/faq-content/{id}
        /// </summary>
        public async Task DeleteFaqAsync(
            Guid faqId,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            using var response = await client.DeleteAsync($"api/faq-content/{faqId}", cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        // ONBOARDING
        /// <summary>
        /// GET api/onboarding/status
        /// </summary>
        public async Task<OnboardingStatusResponse> GetOnboardingStatusAsync(
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var response = await client.GetAsync("api/onboarding/status", cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content
                .ReadFromJsonAsync<OnboardingStatusResponse>(_jsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("api/onboarding/status вернул пустое тело.");
        }

        /// <summary>
        /// POST api/verification/verify-email
        /// </summary>
        public async Task<VerifyEmailResponse> VerifyEmailCodeAsync(
            string code,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var response = await client.PostAsJsonAsync(
                "api/verification/verify-email", new { code }, cancellationToken);

            if (response.IsSuccessStatusCode)
                return await response.Content
                    .ReadFromJsonAsync<VerifyEmailResponse>(_jsonOptions, cancellationToken)
                    ?? new VerifyEmailResponse { IsValid = false, ErrorMessage = "Пустой ответ сервера." };

            return new VerifyEmailResponse
            {
                IsValid = false,
                ErrorMessage = response.StatusCode == HttpStatusCode.BadRequest
                    ? "Неверный или истёкший код подтверждения."
                    : $"Ошибка сервера: {(int)response.StatusCode}."
            };
        }

        /// <summary>
        /// POST api/verification/send-email
        /// </summary>
        public async Task<bool> ResendEmailVerificationAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var client = await CreateAuthorizedClientAsync(cancellationToken);
                var response = await client.PostAsync(
                    "api/verification/send-email", content: null, cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
                when (ex is HttpRequestException or TaskCanceledException)
            {
                return false;
            }
        }

        /// <summary>
        /// POST api/onboarding/child
        /// </summary>
        public async Task<CreateChildOnboardingResponse> CreateChildOnboardingAsync(
            CreateChildOnboardingRequest request,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var response = await client.PostAsJsonAsync("api/onboarding/child", request, cancellationToken);

            if (response.IsSuccessStatusCode)
                return await response.Content
                    .ReadFromJsonAsync<CreateChildOnboardingResponse>(_jsonOptions, cancellationToken)
                    ?? new CreateChildOnboardingResponse { Success = false, ErrorMessage = "Пустой ответ сервера." };

            var body = await response.Content
                .ReadFromJsonAsync<ApiErrorBodyInternal>(_jsonOptions, cancellationToken);
            return new CreateChildOnboardingResponse
            {
                Success = false,
                ErrorMessage = body?.Message ?? body?.Detail ?? "Ошибка создания профиля ребёнка."
            };
        }

        /// <summary>
        /// POST api/onboarding/steps/{step}/complete
        /// </summary>
        public async Task<OnboardingStatusResponse> CompleteOnboardingStepAsync(
            int step,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var response = await client.PostAsync(
                $"api/onboarding/steps/{step}/complete", content: null, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content
                .ReadFromJsonAsync<OnboardingStatusResponse>(_jsonOptions, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"api/onboarding/steps/{step}/complete вернул пустое тело.");
        }

        /// <summary>
        /// POST api/onboarding/complete
        /// </summary>
        public async Task<CompleteOnboardingResponse> CompleteOnboardingAsync(
            CompleteOnboardingRequest request,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            var response = await client.PostAsJsonAsync("api/onboarding/complete", request, cancellationToken);

            if (response.IsSuccessStatusCode)
                return await response.Content
                    .ReadFromJsonAsync<CompleteOnboardingResponse>(_jsonOptions, cancellationToken)
                    ?? new CompleteOnboardingResponse { Success = false, ErrorMessage = "Пустой ответ сервера." };

            var body = await response.Content
                .ReadFromJsonAsync<ApiErrorBodyInternal>(_jsonOptions, cancellationToken);
            return new CompleteOnboardingResponse
            {
                Success = false,
                ErrorMessage = body?.Message ?? body?.Detail ?? "Ошибка завершения онбординга."
            };
        }

        /// <summary>
        /// POST api/onboarding/skip
        /// </summary>
        public async Task SkipOnboardingAsync(
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            await client.PostAsync("api/onboarding/skip", content: null, cancellationToken);

        }

        /// <summary>
        /// Возвращает ChildId из статуса онбординга или Guid.Empty
        /// </summary>
        public async Task<Guid> GetCurrentChildIdAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var status = await GetOnboardingStatusAsync(cancellationToken);
                return status.ChildId ?? Guid.Empty;
            }
            catch (Exception ex)
                when (ex is HttpRequestException
                    or TaskCanceledException
                    or InvalidOperationException)
            {
                return Guid.Empty;
            }
        }

        /// <summary>
        /// Синхронный Try-паттерн для ChildId из конфигурации Api:DemoChildId
        /// </summary>
        public bool TryGetChildId(out Guid childId)
        {
            childId = Guid.Empty;
            try
            {
                var raw = _configuration["Api:DemoChildId"]
                       ?? _configuration["DemoChildId"];
                return Guid.TryParse(raw, out childId) && childId != Guid.Empty;
            }
            catch
            {
                return false;
            }
        }

        // NOTIFICATIONS
        /// <summary>
        /// GET /api/notifications — возвращает список уведомлений для текущего пользователя
        /// </summary>
        public async Task<List<UserNotificationItem>> GetNotificationsAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var client = await CreateAuthorizedClientAsync(cancellationToken);
                var url = "api/notifications";
                var dtos = await GetRequiredAsync<List<UserNotificationItemDto>>(client, url, cancellationToken);
                return dtos
                    .OrderByDescending(d => d.CreatedAt)
                    .Select(d => new UserNotificationItem(
                        d.Title,
                        d.Description,
                        d.Time,
                        d.Type,
                        d.IsUnread,
                        d.NotificationId,
                        d.CreatedAt))
                    .ToList();
            }
            catch (Exception ex)
                when (ex is HttpRequestException
                    or TaskCanceledException
                    or JsonException
                    or InvalidOperationException)
            {
                return new List<UserNotificationItem>
                {
                    new("Ошибка загрузки", "Не удалось загрузить уведомления", "сейчас", "warn", false)
                };
            }
        }

        public sealed class UserNotificationItemDto
        {
            public Guid? NotificationId { get; init; }
            public string Title { get; init; } = string.Empty;
            public string Description { get; init; } = string.Empty;
            public string Time { get; init; } = string.Empty;
            public DateTime CreatedAt { get; init; }
            public string Type { get; init; } = "info";
            public bool IsUnread { get; init; }
        }

        public async Task MarkAllNotificationsAsReadAsync(
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            using var response = await client.PostAsync(
                "api/notifications/read-all",
                content: null,
                cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        public async Task MarkNotificationAsReadAsync(
            Guid notificationId,
            CancellationToken cancellationToken = default)
        {
            var client = await CreateAuthorizedClientAsync(cancellationToken);
            using var response = await client.PostAsync(
                $"api/notifications/{notificationId}/read",
                content: null,
                cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        // USER PREFERENCES
        /// <summary>
        /// GET /api/user-preferences
        /// </summary>
        public async Task<UserPreferencesVm> GetUserPreferencesAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var client = await CreateAuthorizedClientAsync(cancellationToken);
                return await GetRequiredAsync<UserPreferencesVm>(client, "api/user-preferences", cancellationToken);
            }
            catch
            {
                return new UserPreferencesVm();
            }
        }

        /// <summary>
        /// PUT /api/user-preferences
        /// </summary>
        public async Task<bool> SaveUserPreferencesAsync(
            UserPreferencesVm prefs,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var client = await CreateAuthorizedClientAsync(cancellationToken);
                using var response = await client.PutAsJsonAsync("api/user-preferences", prefs, cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // Приватные helper-методы
        /// <summary>
        /// Выполняет GET и десериализует ответ; при ошибке HTTP бросает HttpRequestException
        /// </summary>
        private async Task<T> GetRequiredAsync<T>(
            HttpClient client,
            string url,
            CancellationToken cancellationToken)
        {
            using var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content
                .ReadFromJsonAsync<T>(_jsonOptions, cancellationToken)
                ?? throw new InvalidOperationException($"API вернул пустое тело: {url}");
        }

        /// <summary>
        /// Безопасно десериализует содержимое ответа; возвращает null при ошибке
        /// </summary>
        private static async Task<T?> ReadOptionalAsync<T>(
            HttpContent content,
            CancellationToken cancellationToken) where T : class
        {
            try
            {
                return await content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Строит URL с query-параметрами
        /// </summary>
        private static string BuildUrl(string path, params (string Key, string Value)[] queryParams)
        {
            if (queryParams.Length == 0)
                return path;

            var query = string.Join("&", queryParams.Select(p =>
                $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
            return $"{path}?{query}";
        }

        // Приватный DTO для разбора ошибок onboarding/auth
        private sealed record ApiErrorBodyInternal(
            string? Message,
            string? Detail,
            string? Error);

        // MAP-методы
        private static DashboardSummaryVm MapDashboardSummary(DashboardSummaryApiDto dto) => new()
        {
            ChildId = dto.ChildId,
            LatestGlucose = dto.LatestGlucose,
            LatestMeasurementTime = dto.LatestMeasurementTime,
            LatestGlucoseStatus = dto.LatestGlucoseStatus,
            LatestGlucoseUiState = dto.LatestGlucoseUiState,
            TotalMeasurements = dto.TotalMeasurements24H,
            CriticalEpisodes = dto.CriticalEvents,
            RecommendationsCount = dto.RecommendationsCount,
            PendingExportJobs = dto.PendingExportJobs,
            PendingSyncConflicts = dto.PendingSyncConflicts
        };

        private static TimelineEventDto MapTimelineEvent(TimelineEventApiDto dto) => new()
        {
            EventId = dto.EventId,
            EventType = MapTimelineEventType(dto.EventType),
            OccurredAt = dto.OccurredAt,
            GlucoseValue = dto.GlucoseValue,
            GlucoseUiState = dto.GlucoseUiState,
            DataSource = dto.DataSource,
            SnackName = dto.SnackName,
            BreadUnits = dto.BreadUnits,
            Notes = dto.Notes,
            IsImportant = dto.IsImportant
        };

        private static TimelineEventType MapTimelineEventType(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numericValue))
            {
                return numericValue switch
                {
                    0 => TimelineEventType.Measurement,
                    1 => TimelineEventType.SnackConsumed,
                    2 => TimelineEventType.CriticalAlert,
                    3 => TimelineEventType.Note,
                    _ => TimelineEventType.GlucoseMeasurement
                };
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                var eventType = value.GetString();
                if (string.Equals(eventType, "DoctorNote", StringComparison.OrdinalIgnoreCase))
                {
                    return TimelineEventType.Note;
                }

                if (Enum.TryParse<TimelineEventType>(eventType, ignoreCase: true, out var parsed))
                {
                    return parsed;
                }
            }

            return TimelineEventType.GlucoseMeasurement;
        }

        private static StatisticsVm MapStatistics(StatisticsApiDto dto) => new()
        {
            AverageGlucose = (double)dto.AverageGlucose,
            MinGlucose = (double)dto.MinGlucose,
            MaxGlucose = (double)dto.MaxGlucose,
            StandardDeviation = (double)dto.StandardDeviation,
            Gmi = (double)dto.Gmi,
            TimeInTargetRange = (double)dto.TimeInTargetRange,
            TimeBelowRange = (double)dto.TimeBelowRange,
            TimeAboveRange = (double)dto.TimeAboveRange,
            TimeCriticallyLow = (double)dto.TimeCriticallyLow,
            TimeCriticallyHigh = (double)dto.TimeCriticallyHigh,
            HypoEpisodes = dto.HypoEpisodes,
            HyperEpisodes = dto.HyperEpisodes,
            TotalMeasurements = dto.TotalMeasurements
        };

        private static DoctorPatientSummaryVm MapDoctorPatient(DoctorPatientSummaryApiDto dto) =>
            new(
                dto.LinkId,
                dto.ChildId,
                dto.FirstName,
                dto.LastName,
                dto.DiabetesType,
                dto.DateOfBirth,
                dto.LatestGlucose,
                dto.LatestMeasurementTime,
                dto.LatestGlucoseUiState,
                dto.TimeInTargetRange,
                dto.CriticalEventsLast7Days,
                dto.MeasurementsLast7Days);

        private static DoctorNoteVm MapDoctorNote(DoctorNoteApiDto dto) => new()
        {
            NoteId = dto.NoteId,
            DoctorUserId = dto.DoctorUserId,
            DoctorName = dto.DoctorName,
            ChildId = dto.ChildId,
            MeasurementId = dto.MeasurementId,
            NoteText = dto.NoteText,
            IsImportant = dto.IsImportant,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt
        };

        private static DiabetesSettingsVm MapDiabetesSettings(DiabetesSettingsApiDto dto) => new()
        {
            TargetGlucoseMin = dto.TargetRangeMin,
            TargetGlucoseMax = dto.TargetRangeMax,
            InsulinSensitivity = dto.InsulinSensitivity,
            InsulinToCarbRatio = dto.CarbInsulinRatio
        };

        private static FaqVm MapFaq(FaqArticleApiDto dto) => new()
        {
            FaqId = dto.FaqArticleId,
            Title = dto.Title,
            Content = dto.Content,
            Category = string.Empty,
            SortOrder = 0,
            IsPublished = dto.IsPublished,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt
        };

        private static ChildProfileVm MapChildProfile(ChildProfileApiDto dto) => new()
        {
            ChildId = dto.ChildId,
            FirstName = dto.FirstName ?? string.Empty,
            LastName = dto.LastName ?? string.Empty,
            DateOfBirth = dto.DateOfBirth,
            DiabetesType = dto.DiabetesType ?? string.Empty,
            PhotoUrl = dto.PhotoUrl,
            DiagnosisDate = dto.DiagnosisDate,
            Weight = dto.Weight,
            Height = dto.Height,
            InsulinScheme = dto.InsulinScheme,
            TimeZoneId = string.IsNullOrWhiteSpace(dto.TimeZoneId) ? "Europe/Moscow" : dto.TimeZoneId
        };

        private static ChildProfileVm MapChildDetail(ChildDetailApiDto dto) => new()
        {
            ChildId = dto.ChildId,
            FirstName = dto.FirstName ?? string.Empty,
            LastName = dto.LastName ?? string.Empty,
            DateOfBirth = dto.DateOfBirth,
            DiabetesType = dto.DiabetesType ?? string.Empty,
            PhotoUrl = dto.PhotoUrl,
            DiagnosisDate = dto.DiagnosisDate,
            Weight = dto.Weight,
            Height = dto.Height,
            InsulinScheme = dto.InsulinScheme,
            TimeZoneId = string.IsNullOrWhiteSpace(dto.TimeZoneId) ? "Europe/Moscow" : dto.TimeZoneId
        };

        private static ChildAccessLinksVm MapChildAccessLinks(ChildAccessLinksApiDto dto) => new()
        {
            ParentLinks = dto.ParentLinks.Select(MapLinkedAccessUser).ToList(),
            DoctorLinks = dto.DoctorLinks.Select(MapLinkedAccessUser).ToList()
        };

        private static LinkedAccessUserVm MapLinkedAccessUser(LinkedAccessUserApiDto dto) => new()
        {
            LinkId = dto.LinkId,
            UserId = dto.UserId,
            EmailForLogin = dto.EmailForLogin,
            TelegramId = dto.TelegramId,
            UserRole = dto.UserRole,
            LinkedAt = dto.LinkedAt,
            DisplayName = dto.DisplayName,
            PhotoUrl = dto.PhotoUrl,
            Specialty = dto.Specialty
        };

        private static InviteCodeVm MapInviteCode(InviteCodeApiDto dto) => new()
        {
            InviteCodeId = dto.InviteCodeId,
            ChildId = dto.ChildId,
            Code = dto.Code,
            TargetRole = Enum.TryParse<UserRole>(dto.TargetRole, ignoreCase: true, out var r)
                               ? r : UserRole.Parent,
            Status = dto.Status,
            IsActive = dto.IsActive,
            ExpiresAt = dto.ExpiresAt,
            CreatedAt = dto.CreatedAt
        };

        // Приватные DTO
        private sealed class DashboardSummaryApiDto
        {
            public Guid ChildId { get; init; }
            public decimal? LatestGlucose { get; init; }
            public DateTime? LatestMeasurementTime { get; init; }
            public string? LatestGlucoseStatus { get; init; }
            public string? LatestGlucoseUiState { get; init; }
            public int TotalMeasurements24H { get; init; }
            public int CriticalEvents { get; init; }
            public int RecommendationsCount { get; init; }
            public int PendingExportJobs { get; init; }
            public int PendingSyncConflicts { get; init; }
        }

        private sealed class TimelineEventApiDto
        {
            public Guid EventId { get; init; }
            public JsonElement EventType { get; init; }
            public DateTime OccurredAt { get; init; }
            public decimal? GlucoseValue { get; init; }
            public string? GlucoseUiState { get; init; }
            public string? DataSource { get; init; }
            public string? SnackName { get; init; }
            public decimal? BreadUnits { get; init; }
            public string? Notes { get; init; }
            public bool IsImportant { get; init; }
        }

        private sealed class StatisticsApiDto
        {
            public Guid ChildId { get; init; }
            public string Period { get; init; } = string.Empty;
            public DateTime FromDate { get; init; }
            public DateTime ToDate { get; init; }
            public int TotalMeasurements { get; init; }
            public decimal AverageGlucose { get; init; }
            public decimal MinGlucose { get; init; }
            public decimal MaxGlucose { get; init; }
            public decimal StandardDeviation { get; init; }
            public decimal Gmi { get; init; }
            public decimal TimeInTargetRange { get; init; }
            public decimal TimeBelowRange { get; init; }
            public decimal TimeAboveRange { get; init; }
            public decimal TimeCriticallyLow { get; init; }
            public decimal TimeCriticallyHigh { get; init; }
            public int HypoEpisodes { get; init; }
            public int HyperEpisodes { get; init; }
            public int CriticalEpisodes { get; init; }
        }

        private sealed class PeriodComparisonRawDto
        {
            public decimal AverageGlucoseDelta { get; init; }
            public decimal TimeInRangeDelta { get; init; }
            public int HypoEpisodesDelta { get; init; }
            public int HyperEpisodesDelta { get; init; }
            public bool IsReliable { get; init; }
            public string? UnreliableReason { get; init; }
        }

        private sealed class BackpackResponseApiDto
        {
            public Guid ChildId { get; init; }
            public List<BackpackItemVm> Items { get; init; } = [];
            public int TotalItems { get; init; }
            public decimal TotalBreadUnits { get; init; }
            public DateTime LastUpdated { get; init; }
        }

        private sealed class DoctorPatientSummaryApiDto
        {
            public Guid LinkId { get; init; }
            public Guid ChildId { get; init; }
            public string FirstName { get; init; } = string.Empty;
            public string LastName { get; init; } = string.Empty;
            public string DiabetesType { get; init; } = string.Empty;
            public DateOnly DateOfBirth { get; init; }
            public decimal? LatestGlucose { get; init; }
            public DateTime? LatestMeasurementTime { get; init; }
            public string? LatestGlucoseUiState { get; init; }
            public double TimeInTargetRange { get; init; }
            public int CriticalEventsLast7Days { get; init; }
            public int MeasurementsLast7Days { get; init; }
        }

        private sealed class DoctorCohortSummaryApiDto
        {
            public int TotalPatients { get; init; }
            public int PatientsWithCriticalToday { get; init; }
            public double AverageTimeInTargetRange { get; init; }
            public int PatientsWithoutMeasurementsToday { get; init; }
            public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
        }

        private sealed class PagedDoctorNotesApiDto
        {
            public List<DoctorNoteApiDto> Items { get; init; } = new();
            public int TotalCount { get; init; }
            public int Page { get; init; }
            public int PageSize { get; init; }
        }

        private sealed class DoctorNoteApiDto
        {
            public Guid NoteId { get; init; }
            public Guid DoctorUserId { get; init; }
            public string DoctorName { get; init; } = string.Empty;
            public Guid ChildId { get; init; }
            public Guid? MeasurementId { get; init; }
            public string NoteText { get; init; } = string.Empty;
            public bool IsImportant { get; init; }
            public DateTime CreatedAt { get; init; }
            public DateTime? UpdatedAt { get; init; }
        }

        private sealed class DiabetesSettingsApiDto
        {
            public decimal TargetRangeMin { get; init; }
            public decimal TargetRangeMax { get; init; }
            public decimal InsulinSensitivity { get; init; }
            public decimal CarbInsulinRatio { get; init; }
        }

        private sealed class FaqArticleApiDto
        {
            public Guid FaqArticleId { get; init; }
            public string Title { get; init; } = string.Empty;
            public string Content { get; init; } = string.Empty;
            public bool IsPublished { get; init; }
            public DateTime CreatedAt { get; init; }
            public DateTime UpdatedAt { get; init; }
        }

        private sealed class ChildProfileApiDto
        {
            public Guid ChildId { get; init; }
            public string? FirstName { get; init; }
            public string? LastName { get; init; }
            public DateOnly DateOfBirth { get; init; }
            public string? DiabetesType { get; init; }
            public string? PhotoUrl { get; init; }
            public DateOnly? DiagnosisDate { get; init; }
            public decimal Weight { get; init; }
            public decimal Height { get; init; }
            public string? InsulinScheme { get; init; }
            public string? TimeZoneId { get; init; }
        }

        private sealed class ChildSummaryApiDto
        {
            public Guid ChildId { get; init; }
            public string? FirstName { get; init; }
            public string? LastName { get; init; }
            public DateOnly DateOfBirth { get; init; }
            public string? DiabetesType { get; init; }
            public DateOnly? DiagnosisDate { get; init; }
            public string? PhotoUrl { get; init; }
        }

        private sealed class ChildDetailApiDto
        {
            public Guid ChildId { get; init; }
            public string? FirstName { get; init; }
            public string? LastName { get; init; }
            public DateOnly DateOfBirth { get; init; }
            public decimal Weight { get; init; }
            public decimal Height { get; init; }
            public string? DiabetesType { get; init; }
            public string? PhotoUrl { get; init; }
            public DateOnly? DiagnosisDate { get; init; }
            public string? InsulinScheme { get; init; }
            public string? CurrentInsulins { get; init; }
            public string? TimeZoneId { get; init; }
            public DateTime CreatedAt { get; init; }
            public DateTime UpdatedAt { get; init; }
        }

        private sealed class ChildAccessLinksApiDto
        {
            public Guid ChildId { get; init; }
            public List<LinkedAccessUserApiDto> ParentLinks { get; init; } = new();
            public List<LinkedAccessUserApiDto> DoctorLinks { get; init; } = new();
        }

        private sealed class LinkedAccessUserApiDto
        {
            public Guid LinkId { get; init; }
            public Guid UserId { get; init; }
            public string? EmailForLogin { get; init; }
            public long? TelegramId { get; init; }
            public string UserRole { get; init; } = string.Empty;
            public DateTime LinkedAt { get; init; }
            public string DisplayName { get; init; } = string.Empty;
            public string? PhotoUrl { get; init; }
            public string? Specialty { get; init; }
        }

        private sealed class InviteCodeApiDto
        {
            public Guid InviteCodeId { get; init; }
            public Guid ChildId { get; init; }
            public string Code { get; init; } = string.Empty;
            public string TargetRole { get; init; } = string.Empty;
            public string Status { get; init; } = string.Empty;
            public bool IsActive { get; init; }
            public DateTime? ExpiresAt { get; init; }
            public DateTime CreatedAt { get; init; }
        }

        private sealed class HealthApiDto
        {
            public string? Status { get; init; }
            public string? DatabaseStatus { get; init; }
            public string? Database { get; init; }
            public DateTime? CheckedAt { get; init; }
            public DateTime? TimestampUtc { get; init; }
            public DateTime? ServerUtc { get; init; }
        }

        private sealed class UpdateUserRoleApiRequest
        {
            public string NewRole { get; init; } = string.Empty;
        }

        private sealed class UpdateDoctorUserRoleRequest
        {
            public string NewRole { get; init; } = string.Empty;
        }

        private sealed class CreateParentChildLinkApiRequest
        {
            public Guid ParentUserId { get; init; }
            public Guid ChildId { get; init; }
        }

        private sealed class CreateDoctorChildLinkApiRequest
        {
            public Guid DoctorUserId { get; init; }
            public Guid ChildId { get; init; }
        }
    }
}

