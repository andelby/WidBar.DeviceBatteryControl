using System.Diagnostics;
using System.Text;
using DeviceBatteryControl.Models;

namespace DeviceBatteryControl.Services;

/// <summary>Runs the source-built device protocols outside the UI process.</summary>
public sealed class DeviceProtocolProvider : IBatteryProvider
{
    private readonly string _executable = Path.Combine(AppContext.BaseDirectory, "Native", "BatteryProtocols", "BatteryProtocols.exe");
    private readonly Dictionary<string, BatteryDevice> _last = new(StringComparer.OrdinalIgnoreCase);
    public string? LastError { get; private set; }
    public event EventHandler? DevicesChanged { add { } remove { } }

    public async Task<IReadOnlyList<BatteryDevice>> ReadAsync(CancellationToken token)
    {
        if (!File.Exists(_executable))
        {
            LastError = "ProtocolUnavailable";
            throw new FileNotFoundException("The device protocol engine is missing.", _executable);
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        using var pollGate = new Semaphore(1, 1, "Local\\WidBar.DeviceBatteryControl.ProtocolPoll");
        var ownsGate = false;
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(_executable)
            {
                UseShellExecute = false, CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(_executable)!,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            }
        };
        process.StartInfo.ArgumentList.Add("--json");
        try
        {
            var acquired = await Task.Run(() => WaitHandle.WaitAny([pollGate, timeout.Token.WaitHandle]), timeout.Token);
            if (acquired != 0) timeout.Token.ThrowIfCancellationRequested();
            ownsGate = true;
            process.Start();
            var stdout = ReadBoundedAsync(process.StandardOutput, 512 * 1024, timeout.Token);
            var stderr = ReadBoundedAsync(process.StandardError, 32 * 1024, timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var json = await stdout;
            var error = await stderr;
            if (process.ExitCode != 0) throw new IOException($"Device protocols exited with {process.ExitCode}: {error}");
            var readings = ProtocolSnapshot.Parse(json, DateTimeOffset.UtcNow).Select(device =>
            {
                if ((device.IsAsleep || !device.IsConnected) && _last.TryGetValue(device.Id, out var old))
                    return device with { Percentage = device.Percentage ?? old.Percentage, LastReading = old.LastReading, IsReadingStale = true };
                return device;
            }).ToArray();
            _last.Clear();
            foreach (var device in readings) _last[device.Id] = device;
            LastError = null;
            return readings;
        }
        catch (Exception ex)
        {
            LastError = ex is OperationCanceledException && !token.IsCancellationRequested ? "ProtocolTimeout" : "ProtocolUnavailable";
            try { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); } }
            catch (InvalidOperationException) { }
            if (token.IsCancellationRequested) throw new OperationCanceledException(token);
            throw new IOException(LastError, ex);
        }
        finally { if (ownsGate) pollGate.Release(); }
    }
    private static async Task<string> ReadBoundedAsync(StreamReader reader, int limit, CancellationToken token)
    {
        var result = new StringBuilder();
        var buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), token)) > 0)
        {
            if (result.Length + count > limit) throw new IOException("Protocol output exceeded its limit.");
            result.Append(buffer, 0, count);
        }
        return result.ToString();
    }
    public void Dispose() { }
}
