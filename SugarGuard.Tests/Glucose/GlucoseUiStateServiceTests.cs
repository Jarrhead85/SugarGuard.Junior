using SugarGuard.Domain.Enums;
using SugarGuard.Infrastructure.Glucose;
using SugarGuard.Shared.Constants;

namespace SugarGuard.Tests.Glucose;

/// <summary>
/// Boundary and range tests for <see cref="GlucoseUiStateService.Resolve"/>.
/// Validates Requirements 15.1 – 15.5.
/// </summary>
public class GlucoseUiStateServiceTests
{
    private readonly GlucoseUiStateService _sut = new();

    // -----------------------------------------------------------------------
    // 21.1  Boundary example tests (Requirements 15.1 – 15.5)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that known boundary values map to the expected <see cref="GlucoseUiState"/>.
    ///
    /// Boundary table (all values in mmol/L):
    ///   3.1  → Critical   (below CriticallyLowThreshold)
    ///   3.9  → Attention  (in [CriticallyLowThreshold, LowThreshold))
    ///   4.0  → Normal     (exact lower bound of normal range)
    ///   10.0 → Normal     (exact upper bound of normal range)
    ///   10.1 → Attention  (just above TargetRangeMax, below CriticallyHighThreshold)
    ///   15.0 → Attention  (at CriticallyHighThreshold — still Attention per IsAttention logic)
    ///   15.1 → Critical   (above CriticallyHighThreshold)
    /// </summary>
    [Theory]
    [InlineData(3.1,  GlucoseUiState.Critical)]   // Req 15.4 — below critical low threshold
    [InlineData(3.9,  GlucoseUiState.Attention)]  // Req 15.5 — in low-attention band
    [InlineData(4.0,  GlucoseUiState.Normal)]     // Req 15.2 — exact lower normal boundary
    [InlineData(10.0, GlucoseUiState.Normal)]     // Req 15.3 — exact upper normal boundary
    [InlineData(10.1, GlucoseUiState.Attention)]  // Req 15.5 — just above normal range
    [InlineData(15.0, GlucoseUiState.Attention)]  // Req 15.5 — at high-attention ceiling
    [InlineData(15.1, GlucoseUiState.Critical)]   // Req 15.5 — above critically-high threshold
    public void Resolve_BoundaryValues_ReturnsExpectedState(
        double glucoseValue,
        GlucoseUiState expectedState)
    {
        // ARRANGE
        var input = (decimal)glucoseValue;

        // ACT
        var result = _sut.Resolve(input);

        // ASSERT
        Assert.Equal(expectedState, result);
    }

    // -----------------------------------------------------------------------
    // Additional sanity checks for mid-range values
    // -----------------------------------------------------------------------

    /// <summary>
    /// Values clearly inside the normal range should always return Normal.
    /// </summary>
    [Theory]
    [InlineData(5.0)]
    [InlineData(7.0)]
    [InlineData(9.5)]
    public void Resolve_MidNormalRange_ReturnsNormal(double glucoseValue)
    {
        var result = _sut.Resolve((decimal)glucoseValue);

        Assert.Equal(GlucoseUiState.Normal, result);
    }

    /// <summary>
    /// Values clearly below the critically-low threshold should always return Critical.
    /// </summary>
    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    [InlineData(3.0)]
    public void Resolve_BelowCriticallyLowThreshold_ReturnsCritical(double glucoseValue)
    {
        // Req 15.4
        Assert.True(
            glucoseValue < GlucoseLevels.CriticallyLowThreshold,
            "Pre-condition: value must be below CriticallyLowThreshold");

        var result = _sut.Resolve((decimal)glucoseValue);

        Assert.Equal(GlucoseUiState.Critical, result);
    }

    /// <summary>
    /// Values above the normal range should never return Normal.
    /// </summary>
    [Theory]
    [InlineData(10.1)]
    [InlineData(12.0)]
    [InlineData(15.0)]
    [InlineData(15.1)]
    [InlineData(20.0)]
    public void Resolve_AboveNormalRange_NeverReturnsNormal(double glucoseValue)
    {
        // Req 15.5
        Assert.True(
            glucoseValue > GlucoseLevels.TargetRangeMax,
            "Pre-condition: value must be above TargetRangeMax");

        var result = _sut.Resolve((decimal)glucoseValue);

        Assert.NotEqual(GlucoseUiState.Normal, result);
    }
}
