namespace SugarGuard.API.DTOs;

public sealed class ServerMetricsResponse
{
    public DateTime CollectedAtUtc { get; init; }
    public string MachineName { get; init; } = string.Empty;
    public string OperatingSystem { get; init; } = string.Empty;
    public int ProcessorCount { get; init; }
    public double? CpuUsagePercent { get; init; }
    public double? LoadAverage1Min { get; init; }
    public double MemoryTotalMb { get; init; }
    public double MemoryAvailableMb { get; init; }
    public double MemoryUsedPercent { get; init; }
    public TimeSpan? SystemUptime { get; init; }
    public TimeSpan ProcessUptime { get; init; }
    public long NetworkReceiveBytesPerSecond { get; init; }
    public long NetworkTransmitBytesPerSecond { get; init; }
    public IReadOnlyList<ServerDiskMetricsDto> Disks { get; init; } = [];
}

public sealed class ServerDiskMetricsDto
{
    public string Name { get; init; } = string.Empty;
    public string Root { get; init; } = string.Empty;
    public double TotalGb { get; init; }
    public double FreeGb { get; init; }
    public double UsedPercent { get; init; }
}
