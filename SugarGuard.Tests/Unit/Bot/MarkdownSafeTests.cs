using SugarGuard.Bot.Services;

namespace SugarGuard.Tests.Unit.Bot;

/// <summary>
/// Unit-тесты для <see cref="MarkdownSafe"/>.
/// Защита от Markdown-injection (UI spoofing) в Telegram-сообщениях.
/// </summary>
public class MarkdownSafeTests
{
    [Fact]
    public void EscapeMarkdownV1_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, MarkdownSafe.EscapeMarkdownV1(null));
        Assert.Equal(string.Empty, MarkdownSafe.EscapeMarkdownV1(string.Empty));
    }

    [Fact]
    public void EscapeMarkdownV1_PlainText_ReturnedAsIs()
    {
        Assert.Equal("Hello, World!", MarkdownSafe.EscapeMarkdownV1("Hello, World!"));
        Assert.Equal("Яблоко 100г", MarkdownSafe.EscapeMarkdownV1("Яблоко 100г"));
    }

    [Theory]
    [InlineData("*bold*", "\\*bold\\*")]
    [InlineData("_italic_", "\\_italic\\_")]
    [InlineData("[Click](https://evil.com)", "\\[Click\\](https://evil.com)")]
    [InlineData("`code`", "\\`code\\`")]
    public void EscapeMarkdownV1_SpecialChars_AreEscaped(string input, string expected)
    {
        Assert.Equal(expected, MarkdownSafe.EscapeMarkdownV1(input));
    }

    [Fact]
    public void EscapeMarkdownV1_BackslashIsEscapedFirst()
    {
        // \n в user input не должен создавать \n после escape.
        // Но \n это наша escape-последовательность для обратного слэша.
        var input = @"C:\path\file";
        var expected = @"C:\\path\\file";
        Assert.Equal(expected, MarkdownSafe.EscapeMarkdownV1(input));
    }

    [Fact]
    public void EscapeMarkdownV1_PhishingAttempt_Neutralized()
    {
        var malicious = "*URGENT* [Verify account](https://phishing.com/login) `now!`";
        var escaped = MarkdownSafe.EscapeMarkdownV1(malicious);
        Assert.DoesNotContain("*", escaped.Replace("\\*", ""));
        Assert.DoesNotContain("[", escaped.Replace("\\[", ""));
        Assert.DoesNotContain("`", escaped.Replace("\\`", ""));
    }

    [Fact]
    public void Truncate_LongInput_Truncated()
    {
        var longInput = new string('x', 500);
        var result = MarkdownSafe.Truncate(longInput, 10);
        Assert.Equal(11, result.Length); // 10 chars + ellipsis
    }

    [Fact]
    public void Truncate_ShortInput_ReturnedAsIs()
    {
        Assert.Equal("hello", MarkdownSafe.Truncate("hello", 100));
    }
}
