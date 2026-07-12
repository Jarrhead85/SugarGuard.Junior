using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;

namespace SugarGuard.API.Application.Services;

public sealed class ServerMetricsService : IServerMetricsService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(5);

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly ILogger<ServerMetricsService> _logger;

    private ServerMetricsResponse? _cached;
    private DateTime _cacheExpiresAtUtc;
    private CpuSample? _lastCpuSample;
    private NetworkSample? _lastNetworkSample;

    public ServerMetricsService(ILogger<ServerMetricsService> logger)
    {
        _logger = logger;
    }

    public async Task<ServerMetricsResponse> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        if (_cached is not null && _cacheExpiresAtUtc > now)
        {
            return _cached;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            now = DateTime.UtcNow;
            if (_cached is not null && _cacheExpiresAtUtc > now)
            {
                return _cached;
            }

            var snapshot = await CollectAsync(now, cancellationToken);
            _cached = snapshot;
            _cacheExpiresAtUtc = now.Add(CacheTtl);
            return snapshot;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<ServerMetricsResponse> CollectAsync(DateTime now, CancellationToken cancellationToken)
    {
        var memory = await ReadMemoryAsync(cancellationToken);
        var network = await ReadNetworkThroughputAsync(now, cancellationToken);

        return new ServerMetricsResponse
        {
            CollectedAtUtc = now,
            MachineName = Environment.MachineName,
            OperatingSystem = RuntimeInformation.OSDescription,
            ProcessorCount = Environment.ProcessorCount,
            CpuUsagePercent = await ReadCpuUsagePercentAsync(cancellationToken),
            LoadAverage1Min = await ReadLoadAverageAsync(cancellationToken),
            MemoryTotalMb = memory.TotalMb,
            MemoryAvailableMb = memory.AvailableMb,
            MemoryUsedPercent = memory.UsedPercent,
            SystemUptime = await ReadSystemUptimeAsync(cancellationToken),
            ProcessUptime = GetProcessUptime(),
            NetworkReceiveBytesPerSecond = network.ReceiveBytesPerSecond,
            NetworkTransmitBytesPerSecond = network.TransmitBytesPerSecond,
            Disks = ReadDisks()
        };
    }

    private async Task<double?> ReadCpuUsagePercentAsync(CancellationToken cancellationToken)
    {
        var current = await ReadCpuSampleAsync(cancellationToken);
        if (current is null)
        {
            return null;
        }

        var previous = _lastCpuSample;
        _lastCpuSample = current;

        if (previous is null)
        {
            return null;
        }

        var totalDelta = current.Value.Total - previous.Value.Total;
        var idleDelta = current.Value.Idle - previous.Value.Idle;
        if (totalDelta <= 0)
        {
            return null;
        }

        var usage = (1d - (double)idleDelta / totalDelta) * 100d;
        return Math.Clamp(Math.Round(usage, 1), 0d, 100d);
    }

    private static async Task<CpuSample?> ReadCpuSampleAsync(CancellationToken cancellationToken)
    {
        const string procStatPath = "/proc/stat";
        if (!File.Exists(procStatPath))
        {
            return null;
        }

        var firstLine = (await File.ReadAllLinesAsync(procStatPath, cancellationToken))
            .FirstOrDefault()
            ?.Trim();
        if (string.IsNullOrWhiteSpace(firstLine) || !firstLine.StartsWith("cpu ", StringComparison.Ordinal))
        {
            return null;
        }

        var fields = firstLine
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .Select(value => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0L)
            .ToArray();

        if (fields.Length < 4)
        {
            return null;
        }

        var idle = fields[3] + (fields.Length > 4 ? fields[4] : 0);
        return new CpuSample(fields.Sum(), idle);
    }

    private static async Task<double?> ReadLoadAverageAsync(CancellationToken cancellationToken)
    {
        const string loadAveragePath = "/proc/loadavg";
        if (!File.Exists(loadAveragePath))
        {
            return null;
        }

        var raw = await File.ReadAllTextAsync(loadAveragePath, cancellationToken);
        var first = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return double.TryParse(first, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? Math.Round(value, 2)
            : null;
    }

    private static async Task<MemorySnapshot> ReadMemoryAsync(CancellationToken cancellationToken)
    {
        const string memInfoPath = "/proc/meminfo";
        if (!File.Exists(memInfoPath))
        {
            var managedMb = GC.GetTotalMemory(forceFullCollection: false) / 1024d / 1024d;
            return new MemorySnapshot(managedMb, 0, 100);
        }

        var values = new Dictionary<string, long>(StringComparer.Ordinal);
        await foreach (var line in File.ReadLinesAsync(memInfoPath, cancellationToken))
        {
            var parts = line.Split(':', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            var rawValue = parts[1].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var kb))
            {
                values[parts[0]] = kb;
            }
        }

        var totalKb = values.GetValueOrDefault("MemTotal");
        var availableKb = values.GetValueOrDefault("MemAvailable", values.GetValueOrDefault("MemFree"));
        if (totalKb <= 0)
        {
            return new MemorySnapshot(0, 0, 0);
        }

        var usedPercent = (1d - (double)availableKb / totalKb) * 100d;
        return new MemorySnapshot(
            Math.Round(totalKb / 1024d, 1),
            Math.Round(availableKb / 1024d, 1),
            Math.Clamp(Math.Round(usedPercent, 1), 0d, 100d));
    }

    private async Task<NetworkThroughput> ReadNetworkThroughputAsync(DateTime now, CancellationToken cancellationToken)
    {
        var current = await ReadNetworkSampleAsync(now, cancellationToken);
        if (current is null)
        {
            return new NetworkThroughput(0, 0);
        }

        var previous = _lastNetworkSample;
        _lastNetworkSample = current;

        if (previous is null || current.Value.CollectedAtUtc <= previous.Value.CollectedAtUtc)
        {
            return new NetworkThroughput(0, 0);
        }

        var seconds = (current.Value.CollectedAtUtc - previous.Value.CollectedAtUtc).TotalSeconds;
        if (seconds <= 0)
        {
            return new NetworkThroughput(0, 0);
        }

        var rx = Math.Max(0, current.Value.ReceiveBytes - previous.Value.ReceiveBytes);
        var tx = Math.Max(0, current.Value.TransmitBytes - previous.Value.TransmitBytes);
        return new NetworkThroughput((long)(rx / seconds), (long)(tx / seconds));
    }

    private static async Task<NetworkSample?> ReadNetworkSampleAsync(DateTime now, CancellationToken cancellationToken)
    {
        const string netDevPath = "/proc/net/dev";
        if (!File.Exists(netDevPath))
        {
            return null;
        }

        long receiveBytes = 0;
        long transmitBytes = 0;

        await foreach (var line in File.ReadLinesAsync(netDevPath, cancellationToken))
        {
            var parts = line.Split(':', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            var interfaceName = parts[0].Trim();
            if (string.Equals(interfaceName, "lo", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fields = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 16)
            {
                continue;
            }

            if (long.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rx))
            {
                receiveBytes += rx;
            }

            if (long.TryParse(fields[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out var tx))
            {
                transmitBytes += tx;
            }
        }

        return new NetworkSample(now, receiveBytes, transmitBytes);
    }

    private static async Task<TimeSpan?> ReadSystemUptimeAsync(CancellationToken cancellationToken)
    {
        const string uptimePath = "/proc/uptime";
        if (!File.Exists(uptimePath))
        {
            return null;
        }

        var raw = await File.ReadAllTextAsync(uptimePath, cancellationToken);
        var first = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return double.TryParse(first, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : null;
    }

    private static TimeSpan GetProcessUptime()
    {
        using var process = Process.GetCurrentProcess();
        return DateTime.UtcNow - process.StartTime.ToUniversalTime();
    }

    private IReadOnlyList<ServerDiskMetricsDto> ReadDisks()
    {
        try
        {
            return DriveInfo.GetDrives()
                .Where(drive => drive.IsReady)
                .GroupBy(drive => drive.RootDirectory.FullName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Take(8)
                .Select(drive =>
                {
                    var total = drive.TotalSize;
                    var free = drive.AvailableFreeSpace;
                    var usedPercent = total <= 0 ? 0 : (1d - (double)free / total) * 100d;
                    return new ServerDiskMetricsDto
                    {
                        Name = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? drive.Name : drive.VolumeLabel,
                        Root = drive.RootDirectory.FullName,
                        TotalGb = Math.Round(total / 1024d / 1024d / 1024d, 1),
                        FreeGb = Math.Round(free / 1024d / 1024d / 1024d, 1),
                        UsedPercent = Math.Clamp(Math.Round(usedPercent, 1), 0d, 100d)
                    };
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось прочитать метрики дисков.");
            return [];
        }
    }

    private readonly record struct CpuSample(long Total, long Idle);
    private readonly record struct MemorySnapshot(double TotalMb, double AvailableMb, double UsedPercent);
    private readonly record struct NetworkSample(DateTime CollectedAtUtc, long ReceiveBytes, long TransmitBytes);
    private readonly record struct NetworkThroughput(long ReceiveBytesPerSecond, long TransmitBytesPerSecond);
}
