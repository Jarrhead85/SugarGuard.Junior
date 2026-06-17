using SugarGuard.API.Application.Services;

namespace SugarGuard.Tests.Security;

/// <summary>
/// Тесты для <see cref="AuditDetailsRedactor"/>.
/// <para>
/// Цель: доказать, что PHI (SnackName, GlucoseValue, Notes, BreadUnits, Email, Name)
/// не попадает в AuditLog.details в открытом виде.
/// </para>
/// <para>
/// Compliance: 152-ФЗ §9, GDPR Art. 5(1)(c) (минимизация данных).
/// </para>
/// </summary>
public class AuditDetailsRedactorTests
{
    private readonly AuditDetailsRedactor _sut = new();

    [Fact]
    public void Redact_NullInput_ReturnsNull()
    {
        var result = _sut.Redact(null);
        Assert.Null(result);
    }

    [Fact]
    public void Redact_EmptyInput_ReturnsEmpty()
    {
        var result = _sut.Redact("");
        Assert.Equal("", result);
    }

    [Fact]
    public void Redact_WhitespaceInput_ReturnsWhitespace()
    {
        // whitespace считается "пустым" — redactor передаёт как есть
        var result = _sut.Redact("   ");
        Assert.Equal("   ", result);
    }

    [Fact]
    public void Redact_ChildIdAsGuid_PassesThrough()
    {
        var guid = "11111111-2222-3333-4444-555555555555";
        var input = $"Child={guid};";

        var result = _sut.Redact(input);

        Assert.Equal(input, result);
    }

    [Fact]
    public void Redact_SnackName_IsReplacedWithRedacted()
    {
        // Имитируем реальный details из BackpackService.AddAsync (BEFORE fix).
        var input = "Child=11111111-2222-3333-4444-555555555555;Snack=Яблоко Голден;BreadUnits=1.0";

        var result = _sut.Redact(input);

        Assert.NotNull(result);
        Assert.DoesNotContain("Яблоко", result);
        Assert.DoesNotContain("Голден", result);
        Assert.DoesNotContain("1.0", result);
        Assert.Contains("[REDACTED]", result);
        // Идентификатор ребёнка (Guid) должен сохраниться — он не PHI
        Assert.Contains("Child=11111111-2222-3333-4444-555555555555", result);
    }

    [Fact]
    public void Redact_GlucoseValue_IsReplacedWithRedacted()
    {
        var input = "Child=11111111-2222-3333-4444-555555555555;Glucose=12.4";

        var result = _sut.Redact(input);

        Assert.NotNull(result);
        Assert.DoesNotContain("12.4", result);
        Assert.Contains("Glucose=[REDACTED]", result);
    }

    [Fact]
    public void Redact_BreadUnits_IsReplacedWithRedacted()
    {
        var input = "Child=11111111-2222-3333-4444-555555555555;BreadUnits=2.5";

        var result = _sut.Redact(input);

        Assert.NotNull(result);
        Assert.DoesNotContain("2.5", result);
        Assert.Contains("BreadUnits=[REDACTED]", result);
    }

    [Fact]
    public void Redact_Notes_IsReplacedWithRedacted()
    {
        var input = "Child=guid;Notes=Плохое самочувствие после тренировки";

        var result = _sut.Redact(input);

        Assert.NotNull(result);
        Assert.DoesNotContain("Плохое", result);
        Assert.DoesNotContain("самочувствие", result);
        Assert.Contains("Notes=[REDACTED]", result);
    }

    [Fact]
    public void Redact_Email_IsReplacedWithRedacted()
    {
        var input = "Email=parent@example.com;Child=guid";

        var result = _sut.Redact(input);

        Assert.NotNull(result);
        Assert.DoesNotContain("parent@example.com", result);
        Assert.Contains("Email=[REDACTED]", result);
    }

    [Fact]
    public void Redact_AllWhitelistedKeys_PassesThrough()
    {
        var input = "Child=guid-1;Actor=guid-2;Role=Parent;Id=42;Type=BackpackItem";

        var result = _sut.Redact(input);

        Assert.Equal(input, result);
    }

    [Fact]
    public void Redact_MixedKeys_KeepsWhitelistedAndRedactsOthers()
    {
        var input = "Child=guid-1;Snack=Шоколадка;Actor=guid-2;Glucose=5.5;Role=Parent";

        var result = _sut.Redact(input);

        Assert.NotNull(result);
        Assert.Contains("Child=guid-1", result);
        Assert.Contains("Actor=guid-2", result);
        Assert.Contains("Role=Parent", result);
        Assert.DoesNotContain("Шоколадка", result);
        Assert.DoesNotContain("5.5", result);
        Assert.Contains("Snack=[REDACTED]", result);
        Assert.Contains("Glucose=[REDACTED]", result);
    }

    [Fact]
    public void Redact_FreeTextWithoutKeyValueFormat_RedactsEverything()
    {
        // Свободный текст (без Key=Value) — потенциально содержит PHI → редактируем полностью
        var input = "Пользователь жалуется на головокружение";

        var result = _sut.Redact(input);

        Assert.Equal("[REDACTED]", result);
    }

    [Fact]
    public void Redact_EmptyKeyValuePair_HandledGracefully()
    {
        var input = "Child=guid;Snack=;BreadUnits=1.0";

        var result = _sut.Redact(input);

        Assert.NotNull(result);
        Assert.Contains("Child=guid", result);
        Assert.Contains("Snack=[REDACTED]", result);
        Assert.Contains("BreadUnits=[REDACTED]", result);
    }

    [Fact]
    public void Redact_UnknownKey_IsAlsoRedacted()
    {
        // Любой не-whitelisted ключ должен быть отредактирован
        var input = "Child=guid;CustomKey=some-secret-value";

        var result = _sut.Redact(input);

        Assert.NotNull(result);
        Assert.DoesNotContain("some-secret-value", result);
        Assert.Contains("CustomKey=[REDACTED]", result);
    }

    [Fact]
    public void Redact_KeyIsCaseInsensitive()
    {
        var input = "CHILD=guid-1;snack=Яблоко;ROLE=Parent";

        var result = _sut.Redact(input);

        Assert.NotNull(result);
        Assert.Contains("CHILD=guid-1", result);
        Assert.Contains("snack=[REDACTED]", result);
        Assert.Contains("ROLE=Parent", result);
        Assert.DoesNotContain("Яблоко", result);
    }

    [Fact]
    public void Redact_ValueWithSpecialCharacters_Handled()
    {
        // SnackName может содержать кавычки, скобки и т.п.
        var input = "Child=guid;Snack=Шоколад \"Алёнка\" (100г)";

        var result = _sut.Redact(input);

        Assert.NotNull(result);
        Assert.DoesNotContain("Алёнка", result);
        Assert.DoesNotContain("100г", result);
        Assert.Contains("Snack=[REDACTED]", result);
    }

    [Fact]
    public void Redact_OnlyPhiKeys_ReturnsEmpty()
    {
        // Все ключи — PHI, после редакции ничего не остаётся.
        // Согласно контракту IAuditDetailsRedactor.Redact — возвращаем string.Empty.
        var input = "Snack=Яблоко;Glucose=10.0;Notes=Жалоба";

        var result = _sut.Redact(input);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Redact_OnlyPhiKeys_NeverContainsOriginalValues()
    {
        // Альтернативный тест: убедиться, что PHI не «протекает» даже в [REDACTED] форме
        var input = "Snack=Секретный_Снэк;Glucose=15.7;Notes=Личные_Данные";

        var result = _sut.Redact(input);

        Assert.NotNull(result);
        Assert.DoesNotContain("Секретный_Снэк", result);
        Assert.DoesNotContain("15.7", result);
        Assert.DoesNotContain("Личные_Данные", result);
    }
}
