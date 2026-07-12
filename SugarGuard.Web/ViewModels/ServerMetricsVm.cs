using SugarGuard.Web.Services;

namespace SugarGuard.Web.ViewModels;

public sealed class ServerMetricsVm
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
    public IReadOnlyList<ServerDiskMetricsVm> Disks { get; init; } = [];

    internal static ServerMetricsVm FromDto(ServerMetricsDto dto) => new()
    {
        CollectedAtUtc = dto.CollectedAtUtc,
        MachineName = dto.MachineName,
        OperatingSystem = dto.OperatingSystem,
        ProcessorCount = dto.ProcessorCount,
        CpuUsagePercent = dto.CpuUsagePercent,
        LoadAverage1Min = dto.LoadAverage1Min,
        MemoryTotalMb = dto.MemoryTotalMb,
        MemoryAvailableMb = dto.MemoryAvailableMb,
        MemoryUsedPercent = dto.MemoryUsedPercent,
        SystemUptime = dto.SystemUptime,
        ProcessUptime = dto.ProcessUptime,
        NetworkReceiveBytesPerSecond = dto.NetworkReceiveBytesPerSecond,
        NetworkTransmitBytesPerSecond = dto.NetworkTransmitBytesPerSecond,
        Disks = dto.Disks.Select(ServerDiskMetricsVm.FromDto).ToList()
    };
}

public sealed class ServerDiskMetricsVm
{
    public string Name { get; init; } = string.Empty;
    public string Root { get; init; } = string.Empty;
    public double TotalGb { get; init; }
    public double FreeGb { get; init; }
    public double UsedPercent { get; init; }

    internal static ServerDiskMetricsVm FromDto(ServerDiskMetricsDto dto) => new()
    {
        Name = dto.Name,
        Root = dto.Root,
        TotalGb = dto.TotalGb,
        FreeGb = dto.FreeGb,
        UsedPercent = dto.UsedPercent
    };
}
