using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using SugarGuard.Web.Services;

namespace SugarGuard.Tests.Auth;

// Feature: sugarguard-project-completion, Property 8: JwtAuthStateProvider — role claim round-trip

/// <summary>
/// Тесты для <see cref="JwtAuthStateProvider"/>.
/// </summary>
/// <remarks>
/// Проверяет:
/// <list type="bullet">
///   <item>Истёкший JWT → анонимный <see cref="Microsoft.AspNetCore.Components.Authorization.AuthenticationState"/>.</item>
///   <item>Вызов <see cref="JwtAuthStateProvider.LogoutAsync"/> → анонимное состояние.</item>
/// </list>
/// Валидирует требования 14.4, 14.5, 14.6.
/// </remarks>
public class JwtAuthStateProviderTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Ключ подписи для тестовых токенов. Используется только локально —
    /// настоящий секрет для unit-тестов не требуется.
    /// </summary>
    private static readonly SymmetricSecurityKey TestSigningKey =
        new(System.Text.Encoding.UTF8.GetBytes("sugarguard-test-signing-key-32bytes!"));

    /// <summary>
    /// Строит подписанный JWT с указанными claims и временем жизни.
    /// </summary>
    private static string CreateToken(
        IEnumerable<Claim> claims,
        DateTime? notBefore = null,
        DateTime? expires = null)
    {
        var handler = new JwtSecurityTokenHandler();

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = notBefore ?? DateTime.UtcNow.AddMinutes(-1),
            Expires = expires ?? DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(
                TestSigningKey,
                SecurityAlgorithms.HmacSha256)
        };

        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    /// <summary>
    /// Создаёт <see cref="JwtAuthStateProvider"/> с замоканным
    /// <see cref="ITokenStore"/>, возвращающим <paramref name="storedToken"/>.
    /// </summary>
    private static (JwtAuthStateProvider Provider, Mock<ITokenStore> TokenStoreMock)
        CreateProvider(string? storedToken)
    {
        var tokenStoreMock = new Mock<ITokenStore>();

        tokenStoreMock
            .Setup(s => s.GetTokenAsync())
            .ReturnsAsync(storedToken);

        tokenStoreMock
            .Setup(s => s.RemoveTokenAsync())
            .Returns(Task.CompletedTask);

        // Refresh cookie: по умолчанию обновление не выполняется.
        tokenStoreMock
            .Setup(s => s.RefreshAccessTokenAsync(It.IsAny<string>()))
            .ReturnsAsync((RefreshAccessTokenResult?)null);

        tokenStoreMock
            .Setup(s => s.RemoveRefreshTokenAsync())
            .Returns(Task.CompletedTask);

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var configuration = new ConfigurationBuilder().Build();

        // NullLogger<T> — стандартная no-op реализация из Microsoft.Extensions.Logging.Abstractions.
        // Используется чтобы не мокать ILogger вручную в каждом тесте.
        var logger = NullLogger<JwtAuthStateProvider>.Instance;

        var provider = new JwtAuthStateProvider(
            tokenStoreMock.Object,
            httpClientFactoryMock.Object,
            configuration,
            logger);

        return (provider, tokenStoreMock);
    }

    // -----------------------------------------------------------------------
    // Req 14.5 — Истёкший токен → анонимный AuthenticationState
    // -----------------------------------------------------------------------

    /// <summary>
    /// Если в хранилище находится JWT с истёкшим полем <c>exp</c>,
    /// <see cref="JwtAuthStateProvider.GetAuthenticationStateAsync"/>
    /// должен вернуть анонимный (неаутентифицированный) principal.
    /// </summary>
    /// <remarks>Валидирует требование 14.5.</remarks>
    [Fact]
    public async Task GetAuthenticationStateAsync_ExpiredToken_ReturnsAnonymousState()
    {
        // ARRANGE — токен истёк час назад
        var expiredToken = CreateToken(
            claims: [new Claim(ClaimTypes.Email, "parent@example.com")],
            notBefore: DateTime.UtcNow.AddHours(-2),
            expires: DateTime.UtcNow.AddHours(-1));

        var (provider, _) = CreateProvider(expiredToken);

        // ACT
        var authState = await provider.GetAuthenticationStateAsync();

        // ASSERT
        Assert.NotNull(authState);
        Assert.False(
            authState.User.Identity?.IsAuthenticated ?? false,
            "Истёкший токен должен давать неаутентифицированный principal.");
    }

    /// <summary>
    /// При истёкшем токене <see cref="ITokenStore.RemoveTokenAsync"/>
    /// должен быть вызван ровно один раз.
    /// </summary>
    /// <remarks>Валидирует требование 14.5.</remarks>
    [Fact]
    public async Task GetAuthenticationStateAsync_ExpiredToken_RemovesTokenFromStore()
    {
        // ARRANGE
        var expiredToken = CreateToken(
            claims: [new Claim(ClaimTypes.Email, "parent@example.com")],
            notBefore: DateTime.UtcNow.AddHours(-2),
            expires: DateTime.UtcNow.AddHours(-1));

        var (provider, tokenStoreMock) = CreateProvider(expiredToken);

        // ACT
        await provider.GetAuthenticationStateAsync();

        // ASSERT
        tokenStoreMock.Verify(
            s => s.RemoveTokenAsync(),
            Times.Once,
            "Истёкший токен должен быть удалён из хранилища.");
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ExpiredToken_UsesHttpOnlyRefreshCookieBridge()
    {
        var expiredToken = CreateToken(
            claims: [new Claim(ClaimTypes.Email, "parent@example.com")],
            notBefore: DateTime.UtcNow.AddHours(-2),
            expires: DateTime.UtcNow.AddHours(-1));
        var refreshedToken = CreateToken(
            claims: [new Claim(ClaimTypes.Email, "parent@example.com"), new Claim(ClaimTypes.Role, "Parent")]);

        var (provider, tokenStoreMock) = CreateProvider(expiredToken);
        tokenStoreMock
            .Setup(store => store.RefreshAccessTokenAsync(expiredToken))
            .ReturnsAsync(new RefreshAccessTokenResult(refreshedToken));
        tokenStoreMock
            .Setup(store => store.SetTokenAsync(refreshedToken))
            .Returns(Task.CompletedTask);

        var state = await provider.GetAuthenticationStateAsync();

        Assert.True(state.User.Identity?.IsAuthenticated);
        tokenStoreMock.Verify(store => store.RefreshAccessTokenAsync(expiredToken), Times.Once);
        tokenStoreMock.Verify(store => store.SetTokenAsync(refreshedToken), Times.Once);
    }

    // -----------------------------------------------------------------------
    // Req 14.4 — Logout → анонимный AuthenticationState
    // -----------------------------------------------------------------------

    /// <summary>
    /// После вызова <see cref="JwtAuthStateProvider.LogoutAsync"/>
    /// последующий вызов <see cref="JwtAuthStateProvider.GetAuthenticationStateAsync"/>
    /// должен вернуть анонимное состояние.
    /// </summary>
    /// <remarks>Валидирует требование 14.4.</remarks>
    [Fact]
    public async Task GetAuthenticationStateAsync_AfterLogout_ReturnsAnonymousState()
    {
        // ARRANGE — начинаем с действительного токена в хранилище
        var validToken = CreateToken(
            claims:
            [
                new Claim(ClaimTypes.Email, "parent@example.com"),
                new Claim(ClaimTypes.Role, "Parent")
            ]);

        var tokenStoreMock = new Mock<ITokenStore>();
        var tokenRemoved = false;

        tokenStoreMock
            .Setup(s => s.GetTokenAsync())
            .ReturnsAsync(() => tokenRemoved ? null : validToken);

        tokenStoreMock
            .Setup(s => s.RemoveTokenAsync())
            .Callback(() => tokenRemoved = true)
            .Returns(Task.CompletedTask);

        tokenStoreMock
            .Setup(s => s.RefreshAccessTokenAsync(It.IsAny<string>()))
            .ReturnsAsync((RefreshAccessTokenResult?)null);

        tokenStoreMock
            .Setup(s => s.RemoveRefreshTokenAsync())
            .Returns(Task.CompletedTask);

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var configuration = new ConfigurationBuilder().Build();
        var logger = NullLogger<JwtAuthStateProvider>.Instance;

        var provider = new JwtAuthStateProvider(
            tokenStoreMock.Object,
            httpClientFactoryMock.Object,
            configuration,
            logger);

        // Предусловие: начальное состояние — аутентифицировано
        var initialState = await provider.GetAuthenticationStateAsync();
        Assert.True(
            initialState.User.Identity?.IsAuthenticated ?? false,
            "Предусловие: начальное состояние должно быть аутентифицированным.");

        // ACT
        await provider.LogoutAsync();

        // ASSERT
        var stateAfterLogout = await provider.GetAuthenticationStateAsync();
        Assert.NotNull(stateAfterLogout);
        Assert.False(
            stateAfterLogout.User.Identity?.IsAuthenticated ?? false,
            "После выхода principal должен быть неаутентифицированным.");
    }

    /// <summary>
    /// <see cref="JwtAuthStateProvider.LogoutAsync"/> должен вызвать
    /// <see cref="ITokenStore.RemoveTokenAsync"/> ровно один раз.
    /// </summary>
    /// <remarks>Валидирует требование 14.4.</remarks>
    [Fact]
    public async Task LogoutAsync_RemovesTokenFromStore()
    {
        // ARRANGE
        var (provider, tokenStoreMock) = CreateProvider(storedToken: null);

        // ACT
        await provider.LogoutAsync();

        // ASSERT
        tokenStoreMock.Verify(
            s => s.RemoveTokenAsync(),
            Times.Once,
            "LogoutAsync должен удалить токен из хранилища.");
    }

    // -----------------------------------------------------------------------
    // Req 14.6 — Действительный токен → аутентифицированное состояние
    // -----------------------------------------------------------------------

    /// <summary>
    /// Если в хранилище находится действительный (неистёкший) JWT,
    /// <see cref="JwtAuthStateProvider.GetAuthenticationStateAsync"/>
    /// должен вернуть аутентифицированное состояние.
    /// </summary>
    /// <remarks>Валидирует требование 14.6.</remarks>
    [Fact]
    public async Task GetAuthenticationStateAsync_ValidToken_ReturnsAuthenticatedState()
    {
        // ARRANGE
        const string email = "parent@example.com";
        var validToken = CreateToken(claims: [new Claim(ClaimTypes.Email, email)]);

        var (provider, _) = CreateProvider(validToken);

        // ACT
        var authState = await provider.GetAuthenticationStateAsync();

        // ASSERT
        Assert.NotNull(authState);
        Assert.True(
            authState.User.Identity?.IsAuthenticated ?? false,
            "Действительный токен должен давать аутентифицированный principal.");
    }

    /// <summary>
    /// Если JWT содержит role claim, principal должен включать эту роль.
    /// </summary>
    /// <remarks>Валидирует требование 14.6.</remarks>
    [Fact]
    public async Task GetAuthenticationStateAsync_ValidTokenWithRole_PrincipalContainsRoleClaim()
    {
        // ARRANGE
        const string role = "Parent";
        var validToken = CreateToken(
            claims:
            [
                new Claim(ClaimTypes.Email, "parent@example.com"),
                new Claim(ClaimTypes.Role, role)
            ]);

        var (provider, _) = CreateProvider(validToken);

        // ACT
        var authState = await provider.GetAuthenticationStateAsync();

        // ASSERT
        Assert.True(
            authState.User.IsInRole(role),
            $"Principal должен содержать роль '{role}' из JWT.");
    }

    // -----------------------------------------------------------------------
    // Edge case — null / пустой токен → анонимное состояние
    // -----------------------------------------------------------------------

    /// <summary>
    /// Если хранилище возвращает null (токен не сохранён),
    /// должно вернуться анонимное состояние.
    /// </summary>
    [Fact]
    public async Task GetAuthenticationStateAsync_NullToken_ReturnsAnonymousState()
    {
        // ARRANGE
        var (provider, _) = CreateProvider(storedToken: null);

        // ACT
        var authState = await provider.GetAuthenticationStateAsync();

        // ASSERT
        Assert.NotNull(authState);
        Assert.False(
            authState.User.Identity?.IsAuthenticated ?? false,
            "Отсутствующий токен должен давать неаутентифицированный principal.");
    }
}
