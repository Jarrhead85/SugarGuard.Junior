using SugarGuard.Junior.Core.Validation;

namespace SugarGuard.Tests.Unit.Junior;

public sealed class GlucoseInputNormalizerTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("5,6", "5.6")]
    [InlineData("5.6", "5.6")]
    [InlineData("5..6", "5.6")]
    [InlineData("10,5", "10.5")]
    [InlineData("+14,0 ммоль/л", "14.0")]
    [InlineData("abc", "")]
    public void Normalize_ReturnsCanonicalDecimalInput(string? source, string expected)
    {
        var result = GlucoseInputNormalizer.Normalize(source);

        Assert.Equal(expected, result);
    }
}
