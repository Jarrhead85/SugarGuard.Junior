using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using SugarGuard.Shared.Constants;
using SugarGuard.Junior.Models.Api;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.Utilities;
using SugarGuard.Shared.Dto;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Mock API клиент — только для интеграционных тестов.
/// В production используется RealApiClient.
/// </summary>
[ExcludeFromCodeCoverage]
public class MockApiClient : IApiClient
{
    private readonly ILogger<MockApiClient> _logger;

    /// <summary>
    /// Имитация хранилища пользователей
    /// В реальном сервере это БД
    /// </summary>
    private static readonly Dictionary<string, MockUser> MockUsers = new()
    {
        {
            "test@example.com",
            new MockUser
            {
                UserId = "child_001",
                Email = "test@example.com",
                FirstName = "Иван",
                LastName = "Петров",
                PasswordHash = "mock_hash"
            }
        }
    };

    /// <summary>
    /// Имитация токенов
    /// </summary>
    private static readonly Dictionary<string, MockSession> MockSessions = new();

    /// <summary>
    /// Имитация рюкзака
    /// </summary>
    private static readonly Dictionary<string, List<string>> MockBackpacks = new()
    {
        { "child_001", new List<string> { "яблоко", "бутерброд" } }
    };

    public MockApiClient(ILogger<MockApiClient> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Имитирует вход пользователя
    /// </summary>
    public async Task<LoginResponse> LoginAsync(string email, string password)
    {
        _logger.LogInformation("[MOCK] Попытка входа: {Email}", email);

        // Имитируем задержку сети (500мс)
        await Task.Delay(500);

        // Проверяем "базу данных" (она же словарь)
        if (MockUsers.TryGetValue(email, out var user))
        {
            // Создаём токены
            var accessToken = $"mock_access_{Guid.NewGuid().ToString().Substring(0, 8)}";
            var refreshToken = $"mock_refresh_{Guid.NewGuid().ToString().Substring(0, 8)}";

            // Сохраняем сессию
            MockSessions[accessToken] = new MockSession
            {
                UserId = user.UserId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15)
            };

            _logger.LogInformation(" [MOCK] Вход успешен для {Email}", email);

            return new LoginResponse
            {
                Success = true,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                User = new UserDto
                {
                    UserId = user.UserId,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    IsEmailVerified = true
                }
            };
        }

        _logger.LogWarning(" [MOCK] Неверные учётные данные: {Email}", email);
        return new LoginResponse
        {
            Success = false,
            Message = "Неверный email или пароль"
        };
    }

    /// <summary>
    /// Имитирует регистрацию
    /// </summary>
    public async Task<RegistrationResponse> RegisterAsync(RegistrationRequest request)
    {
        _logger.LogInformation("[MOCK] Регистрация: {Email}", request.Email);
        await Task.Delay(500);

        // Проверяем, существует ли уже пользователь
        if (MockUsers.ContainsKey(request.Email))
        {
            return new RegistrationResponse
            {
                Success = false,
                Message = "Пользователь с этим email уже существует"
            };
        }

        // Создаём нового пользователя
        var userId = $"user_{Guid.NewGuid().ToString().Substring(0, 8)}";
        MockUsers[request.Email] = new MockUser
        {
            UserId = userId,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PasswordHash = "mock_hash" // В реальности - PBKDF2
        };

        // Создаём рюкзак для ребёнка
        MockBackpacks[userId] = new List<string> { "яблоко" };

        _logger.LogInformation(" [MOCK] Регистрация успешна: {Email}", request.Email);

        return new RegistrationResponse
        {
            Success = true,
            UserId = userId,
            Email = request.Email,
            RequiresEmailVerification = true
        };
    }

    /// <summary>
    /// Имитирует подтверждение email
    /// </summary>
    public async Task<VerifyCodeResponse> VerifyEmailAsync(string email, string code)
    {
        _logger.LogInformation("[MOCK] Проверка кода для {Email}", email);
        await Task.Delay(300);

        // В mock всегда успешно (код "123456")
        if (code == "123456")
        {
            _logger.LogInformation(" [MOCK] Email подтверждён: {Email}", email);
            return new VerifyCodeResponse { IsValid = true };
        }

        return new VerifyCodeResponse
        {
            IsValid = false,
            Message = "Неверный код"
        };
    }

    /// <summary>
    /// Имитирует отправку кода подтверждения
    /// </summary>
    public async Task<bool> SendEmailVerificationCodeAsync(string email)
    {
        _logger.LogInformation("[MOCK] Отправка кода на {Email}", email);
        await Task.Delay(1000);

        _logger.LogInformation(" [MOCK] Код '123456' отправлен на {Email}", email);
        return true;
    }

    /// <summary>
    /// Имитирует обновление токена
    /// </summary>
    public async Task<LoginResponse> RefreshTokenAsync(string refreshToken)
    {
        _logger.LogInformation("[MOCK] Обновление токена");
        await Task.Delay(300);

        var newAccessToken = $"mock_access_{Guid.NewGuid().ToString().Substring(0, 8)}";

        _logger.LogInformation(" [MOCK] Токен обновлён");

        return new LoginResponse
        {
            Success = true,
            AccessToken = newAccessToken,
            RefreshToken = refreshToken
        };
    }

    /// <summary>
    /// Имитирует отправку измерения
    /// </summary>
    public async Task<CreateChildOnboardingResponse> CreateChildOnboardingAsync(CreateChildOnboardingRequest request)
    {
        await Task.Delay(150);
        return new CreateChildOnboardingResponse
        {
            Success = true,
            ChildId = Guid.NewGuid(),
            LinkId = Guid.NewGuid(),
            NextStep = OnboardingStep.DiabetesSettings
        };
    }

    public async Task<MeasurementResponse> SendMeasurementAsync(SendMeasurementRequest request)
    {
        try
        {
            _logger.LogInformation("[MOCK] Отправка измерения: {GlucoseValue} ммоль/л", request.GlucoseValue);

            var response = new MeasurementResponse
            {
                Success = true,
                MeasurementId = $"measurement_{Guid.NewGuid().ToString().Substring(0, 8)}",
                IsCritical = GlucoseLevels.IsCritical(request.GlucoseValue)
            };

            // Если запрашивается рекомендация
            if (request.RequestRecommendation)
            {
                // ✅ ИСПРАВЛЯЕМ: Получаем RecommendationResponse из GetRecommendationAsync
                var recRequest = new RecommendationRequest
                {
                    ChildId = request.ChildId,
                    CurrentGlucose = request.GlucoseValue,
                    RecentGlucoseValues = new List<double> { request.GlucoseValue },
                    ChildState = request.ChildState,
                    AvailableSnacks = new List<string>()
                };

                var recommendation = await GetRecommendationAsync(recRequest);
                response.Recommendation = recommendation;  // ← Теперь правильный тип!
            }

            await Task.Delay(100);
            _logger.LogInformation("[MOCK] Измерение отправлено: {MeasurementId}", response.MeasurementId);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MOCK] Ошибка при отправке измерения: {Message}", ex.Message);
            return new MeasurementResponse { Success = false, ErrorMessage = ex.Message };
        }
    }


    /// <summary>
    /// Имитирует синхронизацию нескольких измерений
    /// </summary>
    public async Task<SyncResponse> SyncMeasurementsAsync(SyncRequest request)
    {
        _logger.LogInformation("[MOCK] Синхронизация {Count} измерений", request.Measurements.Count);
        await Task.Delay(request.Measurements.Count * 100); // Задержка пропорционально количеству

        _logger.LogInformation("[MOCK] Все {Count} измерения синхронизированы", request.Measurements.Count);

        return new SyncResponse
        {
            Success = true,
            SuccessCount = request.Measurements.Count,
            ErrorCount = 0
        };
    }

    /// <summary>
    /// Имитирует получение последнего измерения
    /// </summary>
    public async Task<MeasurementResponse> GetLatestMeasurementAsync(string childId)
    {
        _logger.LogInformation("[MOCK] Получение последнего измерения для {ChildId}", childId);
        await Task.Delay(300);

        return new MeasurementResponse
        {
            Success = true,
            MeasurementId = "last_measurement_001"
        };
    }

    public async Task<MeasurementResponse> GetMeasurementByIdAsync(string measurementId)
    {
        _logger.LogInformation("[MOCK] Получение измерения по ID {MeasurementId}", measurementId);
        await Task.Delay(100);

        return new MeasurementResponse
        {
            Success = true,
            MeasurementId = measurementId
        };
    }

    /// <summary>
    /// Имитирует получение рекомендации
    /// </summary>
    public async Task<RecommendationResponse?> GetRecommendationAsync(RecommendationRequest request)
    {
        try
        {
            _logger.LogInformation(" [MOCK] Получение рекомендации для {ChildId}", request.ChildId);

            // Анализируем глюкозу
            var response = new RecommendationResponse
            {
                RecommendationId = Guid.NewGuid().ToString(),
                GlucoseValueAtRequest = request.CurrentGlucose,
                Success = true,
                LatencyMs = Random.Shared.Next(50, 200)
            };

            // Определяем срочность по уровню глюкозы используя централизованную классификацию
            var status = GlucoseClassifier.Classify(request.CurrentGlucose);
            
            response.Urgency = status switch
            {
                Models.Enums.GlucoseStatus.CriticallyLow => "Critical",
                Models.Enums.GlucoseStatus.CriticallyHigh => "Critical",
                Models.Enums.GlucoseStatus.Low => "Warning",
                Models.Enums.GlucoseStatus.High => "Warning",
                _ => "Normal"
            };

            response.RecommendationText = status switch
            {
                Models.Enums.GlucoseStatus.CriticallyLow => $" КРИТИЧЕСКАЯ ГИПОГЛИКЕМИЯ! Глюкоза {request.CurrentGlucose} ммоль/л. НЕМЕДЛЕННО дайте сахар!",
                Models.Enums.GlucoseStatus.Low => $" ГИПОГЛИКЕМИЯ! Глюкоза {request.CurrentGlucose} ммоль/л. Дайте перекус.",
                Models.Enums.GlucoseStatus.CriticallyHigh => $" КРИТИЧЕСКАЯ ГИПЕРГЛИКЕМИЯ! Глюкоза {request.CurrentGlucose} ммоль/л. Обратитесь к врачу!",
                Models.Enums.GlucoseStatus.High => $" ГИПЕРГЛИКЕМИЯ! Глюкоза {request.CurrentGlucose} ммоль/л. Проверьте инсулин.",
                _ => $"✅ Все в норме! Глюкоза {request.CurrentGlucose} ммоль/л."
            };
            
            response.Text = response.RecommendationText;

            response.ModelUsed = "Mock";
            response.Model = "Mock";

            _logger.LogInformation(" [MOCK] Рекомендация: {Urgency}", response.Urgency);

            await Task.Delay(100); // Имитируем задержку сети
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " [MOCK] Ошибка при получении рекомендации: {Message}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Имитирует генерирование кода подключения Telegram
    /// </summary>
    public async Task<TelegramConnectResponse> GenerateTelegramCodeAsync(string childId)
    {
        _logger.LogInformation("[MOCK] Генерирование кода Telegram для {ChildId}", childId);
        await Task.Delay(300);

        _logger.LogInformation(" [MOCK] Код Telegram сгенерирован");

        var code = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        return new TelegramConnectResponse
        {
            Success = true,
            ConnectionCode = code,
            ConnectionCodeId = $"code_{Guid.NewGuid().ToString().Substring(0, 8)}",
            ExpiresIn = 600 // 10 минут
        };
    }

    /// <summary>
    /// Имитирует подтверждение кода Telegram
    /// </summary>
    public async Task<bool> VerifyTelegramConnectionAsync(VerifyTelegramCodeRequest request)
    {
        _logger.LogInformation("[MOCK] Проверка кода Telegram: {ConnectionCodeId}", request.ConnectionCodeId);
        await Task.Delay(500);

        _logger.LogInformation(" [MOCK] Telegram успешно подключён");
        return true;
    }

    /// <summary>
    /// Имитирует добавление перекуса
    /// </summary>
    public async Task<bool> AddSnackAsync(AddSnackRequest request)
    {
        _logger.LogInformation("[MOCK] Добавление перекуса: {SnackName} для {ChildId}", request.SnackName, request.ChildId);
        await Task.Delay(300);

        if (MockBackpacks.TryGetValue(request.ChildId, out var snacks))
        {
            snacks.Add(request.SnackName);
            _logger.LogInformation(" [MOCK] Перекус добавлен");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Имитирует удаление перекуса
    /// </summary>
    public async Task<bool> RemoveSnackAsync(RemoveSnackRequest request)
    {
        _logger.LogInformation("[MOCK] Удаление перекуса {SnackId}", request.SnackId);
        await Task.Delay(300);

        _logger.LogInformation(" [MOCK] Перекус удалён");
        return true;
    }

    /// <summary>
    /// Имитирует получение содержимого рюкзака
    /// </summary>
    public async Task<List<string>> GetBackpackAsync(string childId)
    {
        _logger.LogInformation("[MOCK] Получение рюкзака для {ChildId}", childId);
        await Task.Delay(300);

        if (MockBackpacks.TryGetValue(childId, out var snacks))
        {
            _logger.LogInformation(" [MOCK] Рюкзак получен: {SnackCount} перекусов", snacks.Count);
            return snacks;
        }

        return new List<string>();
    }

    /// <summary>
    /// Имитирует сохранение кода привязки (сырого, без хеширования —
    /// SEC-2: клиент больше не хеширует).
    /// </summary>
    public async Task<SaveConnectionCodeResponse> SaveConnectionCodeAsync(SaveConnectionCodeRequest request)
    {
        _logger.LogInformation("[MOCK] Сохранение кода привязки для ребёнка {ChildId}", request.ChildId);
        await Task.Delay(300);

        _logger.LogInformation(" [MOCK] Код привязки сохранён");

        return new SaveConnectionCodeResponse
        {
            Success = true,
            CodeId = $"code_{Guid.NewGuid().ToString().Substring(0, 8)}",
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };
    }

    /// <summary>
    /// Имитирует проверку кода привязки от Telegram-бота
    /// </summary>
    public async Task<VerifyConnectionCodeResponse> VerifyConnectionCodeAsync(VerifyConnectionCodeRequest request)
    {
        _logger.LogInformation("[MOCK] Проверка кода привязки: {ConnectionCode}", request.ConnectionCode);
        await Task.Delay(500);

        // В mock всегда успешно для тестового кода "ABCD-1234"
        // (формат согласован с ConnectionCodeFormat в SugarGuard.Shared).
        if (request.ConnectionCode == "ABCD-1234")
        {
            _logger.LogInformation(" [MOCK] Код привязки верен, связь создана");
            return new VerifyConnectionCodeResponse
            {
                Success = true,
                IsValid = true,
                ChildId = "child_001",
                LinkId = $"link_{Guid.NewGuid().ToString().Substring(0, 8)}",
                Message = "Связь успешно создана"
            };
        }

        _logger.LogWarning(" [MOCK] Неверный код привязки");
        return new VerifyConnectionCodeResponse
        {
            Success = false,
            IsValid = false,
            ErrorMessage = "Неверный или просроченный код"
        };
    }

    /// <summary>
    /// Имитирует отправку уведомления родителям об измерении
    /// </summary>
    public async Task<bool> SendMeasurementNotificationAsync(MeasurementNotificationRequest request)
    {
        _logger.LogInformation("[MOCK] Отправка уведомления об измерении: {GlucoseValue} ммоль/л", request.GlucoseValue);
        await Task.Delay(200);
        _logger.LogInformation(" [MOCK] Уведомление об измерении отправлено родителям");
        return true;
    }

    /// <summary>
    /// Имитирует отправку уведомления родителям о съеденном перекусе
    /// </summary>
    public async Task<bool> SendSnackConsumedNotificationAsync(SnackConsumedNotificationRequest request)
    {
        _logger.LogInformation("[MOCK] Отправка уведомления о перекусе: {SnackName}", request.SnackName);
        await Task.Delay(200);
        _logger.LogInformation(" [MOCK] Уведомление о перекусе отправлено родителям");
        return true;
    }

    /// <summary>
    /// Имитирует отправку критического уведомления с геолокацией
    /// </summary>
    public async Task<bool> SendCriticalAlertAsync(CriticalAlertRequest request)
    {
        _logger.LogWarning("[MOCK]  Критическое уведомление: {GlucoseValue} ммоль/л", request.GlucoseValue);
        await Task.Delay(300);
        _logger.LogInformation(" [MOCK] Критическое уведомление отправлено родителям");
        return true;
    }

    /// <summary>
    /// Имитирует отправку уведомления о пропущенном измерении
    /// </summary>
    public async Task<bool> SendMissedMeasurementNotificationAsync(MissedMeasurementNotificationRequest request)
    {
        _logger.LogWarning("[MOCK] Пропущенное измерение: {ScheduledTime}, опоздание {MinutesLate} мин", 
            request.ScheduledTime.ToString("HH:mm"), request.MinutesLate);
        await Task.Delay(200);
        _logger.LogInformation("[MOCK] Уведомление о пропущенном измерении отправлено родителям");
        return true;
    }

    /// <summary>
    /// Имитирует экспорт статистики в PDF
    /// </summary>
    public async Task<byte[]> ExportStatisticsToPdfAsync(string childId, string period = "day", bool detailed = false)
    {
        _logger.LogInformation("[MOCK] Экспорт PDF для ребёнка {ChildId}, период: {Period}, подробный: {Detailed}", childId, period, detailed);
        
        // Имитируем задержку генерации PDF
        await Task.Delay(1500);
        
        // Создаём простой mock PDF (в реальности это будет настоящий PDF)
        var mockPdfContent = System.Text.Encoding.UTF8.GetBytes(
            $"Mock PDF Report\nChild: {childId}\nPeriod: {period}\nDetailed: {detailed}\nGenerated: {DateTime.Now}");
        
        _logger.LogInformation("[MOCK] PDF сгенерирован, размер: {Size} байт", mockPdfContent.Length);
        return mockPdfContent;
    }

    /// <summary>
    /// Имитирует проверку доступности сервера
    /// </summary>
    public async Task<bool> HealthCheckAsync()
    {
        _logger.LogInformation("[MOCK] Health check");
        await Task.Delay(100);
        _logger.LogInformation("[MOCK] Сервер доступен");
        return true;
    }

    // ─────────────────────────────────────────────────────────────
    // Фото ребёнка (TODO-3) — mock-реализация
    // ─────────────────────────────────────────────────────────────

    public async Task<string?> UploadChildPhotoAsync(
        string childId,
        Stream photoStream,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        _logger.LogInformation("[MOCK] UploadChildPhoto for {ChildId}, {Name}, {Type}", childId, fileName, contentType);
        await Task.Delay(200, ct);

        // В mock-режиме возвращаем локальный data URL, чтобы UI мог отобразить превью.
        // Не сериализуем бинарный поток в строку — просто эмулируем успех с фейковым URL.
        return $"mock://children/{childId}/photo?file={Uri.EscapeDataString(fileName)}";
    }

    public async Task<bool> DeleteChildPhotoAsync(string childId, CancellationToken ct = default)
    {
        _logger.LogInformation("[MOCK] DeleteChildPhoto for {ChildId}", childId);
        await Task.Delay(100, ct);
        return true;
    }

    /// <summary>
    /// Помощный метод для генерирования mock рекомендации
    /// </summary>
    private static RecommendationResponse GetMockRecommendation(double glucose)
    {
        var status = GlucoseClassifier.Classify(glucose);
        
        var text = status switch
        {
            Models.Enums.GlucoseStatus.CriticallyLow => " КРИТИЧЕСКИ НИЗКО! Дайте углеводы (сок, конфету). Проверьте самочувствие.",
            Models.Enums.GlucoseStatus.Low => " Уровень глюкозы низкий. Съешьте небольшой перекус.",
            Models.Enums.GlucoseStatus.Normal => "✅ Уровень нормальный. Продолжайте как обычно.",
            Models.Enums.GlucoseStatus.High => " Уровень повышен. Убедитесь в наличии инсулина.",
            _ => " КРИТИЧЕСКИ ВЫСОКО! Проверьте инсулин, пейте воду."
        };

        return new RecommendationResponse
        {
            RecommendationId = $"rec_{Guid.NewGuid().ToString().Substring(0, 8)}",
            GlucoseValueAtRequest = glucose,
            RecommendationText = text,
            Text = text,
            ModelUsed = "GigaChat_Mock",
            Model = "GigaChat_Mock",
            LatencyMs = 1000,
            IsFromCache = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Вспомогательный класс для имитации пользователя
    /// </summary>
    private class MockUser
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
    }

    /// <summary>
    /// Вспомогательный класс для имитации сессии
    /// </summary>
    private class MockSession
    {
        public string UserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public Task LogoutAsync(string refreshToken)
    {
        return Task.CompletedTask;
    }
}
