using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.API.Extensions;
using SugarGuard.API.Security;
using SugarGuard.API.Services;
using SugarGuard.Application.Security;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Controllers;

/// <summary>
/// Управляет аутентификацией
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IRolePermissionService _rolePermissionService;
    private readonly IVerificationService _verificationService;
    private readonly JwtSettings _jwtSettings;
    private readonly DemoEmailBypassSettings _demoEmailBypassSettings;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AuthController> _logger;

    /// <summary>
    /// Инициализирует контроллер через DI
    /// </summary>
    public AuthController(
        IAuthService auth,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        IRolePermissionService rolePermissionService,
        IVerificationService verificationService,
        JwtSettings jwtSettings,
        DemoEmailBypassSettings demoEmailBypassSettings,
        IWebHostEnvironment environment,
        ILogger<AuthController> logger)
    {
        _auth = auth;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _rolePermissionService = rolePermissionService;
        _verificationService = verificationService;
        _jwtSettings = jwtSettings;
        _demoEmailBypassSettings = demoEmailBypassSettings;
        _environment = environment;
        _logger = logger;
    }

    // POST api/auth/login
    /// <summary>
    /// Выполняет вход по email и паролю, возвращает access- и refresh-токены
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _auth.LoginAsync(
            request.Email, request.Password, cancellationToken);

        if (result.FailureReason != LoginFailureReason.None)
        {
            return Unauthorized(new LoginResponse
            {
                Success = false,
                ErrorMessage = result.FailureReason switch
                {
                    LoginFailureReason.UserNotFound => "Invalid email or password",
                    LoginFailureReason.AccountDeactivated => "Account is deactivated",
                    LoginFailureReason.PasswordNotConfigured => "Invalid email or password",
                    LoginFailureReason.PasswordMismatch => "Invalid email or password",
                    LoginFailureReason.EmailNotVerified =>
                        "Email is not verified. Please check your inbox.",
                    _ => "Invalid email or password"
                }
            });
        }

        var user = result.User!;
        var accessToken = _jwtTokenService.GenerateToken(user);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var (plainRefreshToken, _) = await _refreshTokenService.CreateAsync(
            user.UserId.ToString(), ip, userAgent, cancellationToken);

        return Ok(new LoginResponse
        {
            Success = true,
            UserId = user.UserId,
            Role = user.Role.ToString(),
            Permissions = _rolePermissionService.GetPermissions(user.Role),
            AccessToken = accessToken,
            RefreshToken = plainRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddHours(_jwtSettings.ExpiryHours),
            Message = "Login successful"
        });
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        var role = ParseRegistrationRole(request.Role);
        if (role is null)
        {
            return BadRequest(new
            {
                error = "role_not_allowed",
                message = "Requested role is not allowed for self-registration."
            });
        }

        var result = await _auth.RegisterAsync(
            request.Email,
            request.Password,
            role.Value,
            cancellationToken);

        if (!result.Success)
        {
            if (result.ErrorCode == "email_already_registered")
            {
                return Conflict(new
                {
                    error = result.ErrorCode,
                    message = result.ErrorMessage
                });
            }

            return BadRequest(new
            {
                error = result.ErrorCode ?? "registration_failed",
                message = result.ErrorMessage ?? "Registration failed."
            });
        }

        var sendResult = await _verificationService.SendCodeAsync(
            request.Email,
            VerificationPurpose.Registration,
            cancellationToken);

        if (!sendResult.Success)
        {
            if (sendResult.ErrorCode == "too_many_requests")
            {
                _logger.LogInformation(
                    "Register: verification email was recently sent. Continuing registration flow. UserId={UserId}.",
                    result.User!.UserId);

                return Ok(new
                {
                    success = true,
                    userId = result.User!.UserId,
                    role = result.User.Role.ToString(),
                    message = "Verification code was recently sent. Check your email or spam folder.",
                    requiresEmailVerification = true,
                    retryAfterSeconds = sendResult.RetryAfterSeconds,
                    expiresAt = sendResult.ExpiresAt
                });
            }

            if (IsDemoEmailBypassEnabled())
            {
                await _auth.ConfirmEmailAsync(
                    request.Email,
                    "demo-email-bypass",
                    cancellationToken);

                _logger.LogWarning(
                    "Register: verification email failed, demo bypass confirmed email. UserId={UserId} Error={ErrorCode}.",
                    result.User!.UserId,
                    sendResult.ErrorCode);

                return Ok(new
                {
                    success = true,
                    userId = result.User!.UserId,
                    role = result.User.Role.ToString(),
                    message = "Registration successful. Email verification was bypassed for demo mode.",
                    requiresEmailVerification = false,
                    emailVerificationBypassed = true
                });
            }

            _logger.LogWarning(
                "Register: user created but verification email failed. UserId={UserId} Error={ErrorCode}.",
                result.User!.UserId,
                sendResult.ErrorCode);

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = sendResult.ErrorCode ?? "verification_send_failed",
                message = sendResult.ErrorMessage ?? "Could not send verification code."
            });
        }

        return Ok(new
        {
            success = true,
            userId = result.User!.UserId,
            role = result.User.Role.ToString(),
            message = "Registration successful. Check your email for verification code.",
            expiresAt = sendResult.ExpiresAt
        });
    }

    private static UserRole? ParseRegistrationRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return UserRole.Parent;
        }

        return role.Trim().ToLowerInvariant() switch
        {
            "parent" => UserRole.Parent,
            "doctor" => UserRole.DoctorPending,
            _ => null
        };
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail(
        [FromBody] VerifyEmailRequest request,
        CancellationToken cancellationToken)
    {
        var verifyResult = await _verificationService.VerifyCodeAsync(
            request.Email,
            request.Code,
            VerificationPurpose.Registration,
            cancellationToken);

        if (!verifyResult.Success || string.IsNullOrWhiteSpace(verifyResult.VerificationToken))
        {
            return BadRequest(new
            {
                error = verifyResult.ErrorCode ?? "verification_failed",
                message = verifyResult.ErrorMessage ?? "Invalid verification code.",
                attemptsLeft = verifyResult.AttemptsLeft
            });
        }

        var confirmResult = await _auth.ConfirmEmailAsync(
            request.Email,
            verifyResult.VerificationToken,
            cancellationToken);

        if (!confirmResult.Success)
        {
            return BadRequest(new
            {
                error = confirmResult.ErrorCode ?? "email_confirm_failed",
                message = confirmResult.ErrorMessage ?? "Could not confirm email."
            });
        }

        var user = confirmResult.User!;
        var accessToken = _jwtTokenService.GenerateToken(user);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var (plainRefreshToken, _) = await _refreshTokenService.CreateAsync(
            user.UserId.ToString(), ip, userAgent, cancellationToken);

        return Ok(new
        {
            success = true,
            isValid = true,
            userId = user.UserId,
            role = user.Role.ToString(),
            permissions = _rolePermissionService.GetPermissions(user.Role),
            accessToken,
            token = accessToken,
            refreshToken = plainRefreshToken,
            expiresAt = DateTime.UtcNow.AddHours(_jwtSettings.ExpiryHours),
            message = "Email verified."
        });
    }

    [HttpPost("resend-verification")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResendVerification(
        [FromBody] ResendVerificationRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _auth.FindActiveUserByEmailAsync(request.Email, cancellationToken);
        if (user is null)
            return Ok(new { success = true });

        if (user.IsEmailVerified)
            return Ok(new { success = true, message = "Email already verified." });

        var sendResult = await _verificationService.SendCodeAsync(
            request.Email,
            VerificationPurpose.Registration,
            cancellationToken);

        if (!sendResult.Success)
        {
            return BadRequest(new
            {
                error = sendResult.ErrorCode ?? "verification_send_failed",
                message = sendResult.ErrorMessage ?? "Could not send verification code.",
                retryAfterSeconds = sendResult.RetryAfterSeconds
            });
        }

        return Ok(new
        {
            success = true,
            expiresAt = sendResult.ExpiresAt
        });
    }

    // POST api/auth/refresh
    /// <summary>
    /// Выполняет Refresh Token Rotation: инвалидирует старый refresh-токен
    /// и возвращает новую пару access+refresh. Требует передачи обоих токенов.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RefreshTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        // Извлекаем UserId из истёкшего access-токена
        var userId = ExtractUserIdFromExpiredToken(request.AccessToken);
        if (userId is null)
        {
            _logger.LogWarning("Refresh: не удалось извлечь UserId из access-токена.");
            return Unauthorized(new { error = "invalid_token", message = "Invalid access token." });
        }

        if (!Guid.TryParse(userId, out var userGuid))
            return Unauthorized(new { error = "invalid_token", message = "Invalid user identifier." });

        // Проверяем refresh-токен по хешу, сроку и принадлежности пользователю
        var existingToken = await _refreshTokenService.ValidateAsync(
            request.RefreshToken, userId, cancellationToken);

        if (existingToken is null)
        {
            await _auth.WriteRefreshFailedAuditAsync(
                userId, "invalid_or_revoked_refresh_token", cancellationToken);
            _logger.LogWarning("Refresh: невалидный/отозванный токен. UserId={UserId}", userId);
            return Unauthorized(new
            {
                error = "invalid_refresh_token",
                message = "Refresh token is invalid or expired."
            });
        }

        // Загружаем пользователя и проверяем активность
        var (user, isActive) = await _auth.GetUserForRefreshAsync(userGuid, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning("Refresh: пользователь не найден. UserId={UserId}", userId);
            return Unauthorized(new { error = "user_not_found", message = "User not found." });
        }

        if (!isActive)
        {
            await _refreshTokenService.RevokeAllForUserAsync(
                userId, "account_deactivated", cancellationToken);
            _logger.LogWarning("Refresh: аккаунт деактивирован. UserId={UserId}", userId);
            return Unauthorized(new
            {
                error = "account_deactivated",
                message = "Account is deactivated."
            });
        }

        // Меняем refresh-токен 
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var newPlainRefreshToken = await _refreshTokenService.RotateAsync(
            existingToken, userId, ip, userAgent, cancellationToken);

        // Выпускаем новый access-токен
        var newAccessToken = _jwtTokenService.GenerateToken(user);

        await _auth.WriteRefreshSuccessAuditAsync(userId, cancellationToken);
        _logger.LogInformation("Refresh Token Rotation успешен. UserId={UserId}", userId);

        return Ok(new RefreshTokenResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newPlainRefreshToken,
            AccessTokenExpiresAt = DateTime.UtcNow.AddHours(_jwtSettings.ExpiryHours),
            RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays)
        });
    }

    // POST api/auth/logout
    /// <summary>
    /// Отзывает refresh-токен текущей сессии
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue("UserId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId is not null && !string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            await _refreshTokenService.RevokeAsync(
                request.RefreshToken, userId, "logout", cancellationToken);
        }

        await _auth.WriteLogoutAuditAsync(userId, cancellationToken);
        _logger.LogInformation("Выход выполнен. UserId={UserId}", userId);

        return NoContent();
    }

    // POST api/auth/forgot-password
    /// <summary>
    /// Отправляет 8-значный код сброса пароля на указанный email.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _auth.FindActiveUserByEmailAsync(request.Email, cancellationToken);

        if (user is not null)
        {
            try
            {
                await _verificationService.SendCodeAsync(
                    request.Email,
                    VerificationPurpose.PasswordReset,
                    cancellationToken);

                await _auth.WriteForgotPasswordAuditAsync(
                    user.UserId.ToString(), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "ForgotPassword: ошибка отправки кода. Email={Email}", request.Email);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "Сейчас не удалось отправить код. Попробуйте позже."
                });
            }
        }

        return Ok(new { message = "Если email зарегистрирован, код отправлен." });
    }

    // POST api/auth/reset-password
    /// <summary>
    /// Проверяет код сброса и устанавливает новый пароль
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var verifyResult = await _verificationService.VerifyCodeAsync(
            request.Email,
            request.Code,
            VerificationPurpose.PasswordReset,
            cancellationToken);

        if (!verifyResult.Success)
        {
            return BadRequest(new
            {
                message = verifyResult.ErrorMessage ?? "Неверный код.",
                errorCode = verifyResult.ErrorCode,
                attemptsLeft = verifyResult.AttemptsLeft
            });
        }

        var result = await _auth.ResetPasswordAsync(
            request.Email, request.NewPassword, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { message = result.ErrorMessage ?? "Пользователь не найден." });
        }

        // Отзываем все refresh-токены
        await _refreshTokenService.RevokeAllForUserAsync(
            result.User!.UserId.ToString(), "password_reset", cancellationToken);

        return Ok(new { message = "Пароль успешно изменён." });
    }


    // POST api/auth/bot-login
    /// <summary>
    /// Аутентификация Telegram Bot Service по статичному API-ключу
    /// </summary>
    [HttpPost("bot-login")]
    [AllowAnonymous]
    [EnableRateLimiting("bot-login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LoginResponse>> BotLogin(
        [FromBody] BotLoginRequest request,
        CancellationToken cancellationToken)
    {
        var keyValidation = _auth.ValidateBotApiKey(request.ApiKey);

        if (keyValidation is null)
        {
            _logger.LogError("BotLogin: переменная BOT_SERVICE_AUTH_KEY не задана.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new LoginResponse
                {
                    Success = false,
                    ErrorMessage = "Bot authentication is not configured"
                });
        }

        if (keyValidation == false)
        {
            await _auth.WriteBotLoginAuditAsync(
                success: false, serviceAccountUserId: null, reason: "invalid_api_key",
                cancellationToken: cancellationToken);
            return Unauthorized(new LoginResponse
            {
                Success = false,
                ErrorMessage = "Invalid bot API key"
            });
        }

        var serviceAccountUser = await _auth.GetOrCreateServiceAccountAsync(cancellationToken);

        var accessToken = _jwtTokenService.GenerateToken(serviceAccountUser);

        await _auth.WriteBotLoginAuditAsync(
            success: true,
            serviceAccountUserId: serviceAccountUser.UserId.ToString(),
            reason: null,
            cancellationToken: cancellationToken);

        _logger.LogInformation("BotLogin успешен. UserId={UserId}", serviceAccountUser.UserId);

        return Ok(new LoginResponse
        {
            Success = true,
            UserId = serviceAccountUser.UserId,
            Role = serviceAccountUser.Role.ToString(),
            Permissions = _rolePermissionService.GetPermissions(serviceAccountUser.Role),
            AccessToken = accessToken,
            RefreshToken = null, 
            ExpiresAt = DateTime.UtcNow.AddHours(_jwtSettings.ExpiryHours),
            Message = "Bot login successful"
        });
    }

    // Вспомогательные методы
    private string? ExtractUserIdFromExpiredToken(string accessToken)
    {
        try
        {
            var tokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = false,   
                ValidateIssuerSigningKey = true,
                ValidIssuer = _jwtSettings.Issuer,
                ValidAudience = _jwtSettings.Audience,
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                    System.Text.Encoding.UTF8.GetBytes(_jwtSettings.Secret)),
                ClockSkew = TimeSpan.Zero
            };

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(
                accessToken, tokenValidationParameters, out _);

            return principal.FindFirstValue("UserId")
                ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ExtractUserIdFromExpiredToken: не удалось прочитать токен.");
            return null;
        }
    }

    private bool IsDemoEmailBypassEnabled()
    {
        if (!_environment.IsDevelopment())
        {
            return false;
        }

        return _demoEmailBypassSettings.Enabled;
    }
}
