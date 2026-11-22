using System.Diagnostics;
using System.Text.Json;
using Microsoft.UI.Dispatching;
using DeviceBatteryControl.Models;

namespace DeviceBatteryControl.Services;

/// <summary>One provider, history and refresh gate for every widget in the process.
/// Registered taskbar widgets keep discovery active while their empty preview is hidden.</summary>
public sealed class BatteryMonitor : IAsyncDisposable
{
    private static BatteryMonitor? _shared;
    private readonly IBatteryProvider _provider;
    private readonly DispatcherQueue _dispatcher;
    private readonly DispatcherQueueTimer _timer;
    private readonly DispatcherQueueTimer _debounce;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Dictionary<object, (bool Active, BatterySettings Settings)> _clients = [];
    private readonly BatteryHistoryStore _store;
    private bool _dirty;
    private bool _disposed;
    private Task? _refresh;
    private Task _save = Task.CompletedTask;
    private Task? _historyLoad;
    public IReadOnlyList<BatteryDevice> Devices { get; private set; } = [];
    public BatteryHistory History { get; private set; } = new();
    public bool IsRefreshing { get; private set; }
    public string? Error { get; private set; }
    public DateTimeOffset? LastRefresh { get; private set; }
    public double LastRefreshMilliseconds { get; private set; }
    public long RefreshCount { get; private set; }
    public event EventHandler? Changed;

    public static BatteryMonitor Join(object client, string directory, BatterySettings settings, bool active)
    {
        _shared ??= new BatteryMonitor(new ConnectedBatteryProvider(), directory);
        _shared.UpdateClient(client, settings, active);
        return _shared;
    }

    public BatteryMonitor(IBatteryProvider provider, string directory)
    {
        _provider = provider;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _store = new(directory);
        _timer = _dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(BatterySettings.RefreshSeconds);
        _timer.Tick += OnTimer;
        _debounce = _dispatcher.CreateTimer();
        _debounce.Interval = TimeSpan.FromSeconds(1);
        _debounce.IsRepeating = false;
        _debounce.Tick += OnTimer;
        provider.DevicesChanged += OnDevicesChanged;
    }

    public Task LoadHistoryAsync() => _historyLoad ??= LoadHistoryCoreAsync();

    private async Task LoadHistoryCoreAsync()
    {
        try
        {
            History = await _store.LoadAsync(DateTimeOffset.UtcNow);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or ArgumentException)
        { Trace.WriteLine($"Battery history: {ex.Message}"); History = new(); }
    }

    public void UpdateClient(object client, BatterySettings settings, bool active)
    {
        var wasActive = _clients.Values.Any(x => x.Active);
        _clients[client] = (active, settings);
        var activeClients = _clients.Values.Where(x => x.Active).ToArray();
        if (activeClients.Length > 0)
        {
            if (!_timer.IsRunning) _timer.Start();
            if (!wasActive && LastRefresh is not null) _ = RefreshAsync();
        }
        else { _timer.Stop(); _debounce.Stop(); }
    }

    private void OnDevicesChanged(object? sender, EventArgs args) => _dispatcher.TryEnqueue(() =>
    {
        if (!_disposed && _clients.Values.Any(x => x.Active) && !_debounce.IsRunning) _debounce.Start();
    });
    private void OnTimer(DispatcherQueueTimer sender, object args) => _ = RefreshAsync();

    public Task RefreshAsync()
    {
        if (_disposed) return Task.CompletedTask;
        if (_refresh is { IsCompleted: false }) return _refresh;
        return _refresh = RefreshCoreAsync();
    }

    private async Task RefreshCoreAsync()
    {
        IsRefreshing = true;
        Changed?.Invoke(this, EventArgs.Empty);
        var watch = Stopwatch.StartNew();
        try
        {
            var readings = await _provider.ReadAsync(_lifetime.Token);
            var now = DateTimeOffset.UtcNow;
            if (_provider is ConnectedBatteryProvider connected) _dirty |= History.Remap(connected.IdentityAliases);
            Devices = History.WithLastKnown(readings, now);
            _dirty |= History.Remember(Devices, now);
            if (_clients.Values.Any(x => x.Settings.KeepHistory))
                foreach (var device in Devices.Where(x => x.LastReading is not null && (LastRefresh is null || x.LastReading > LastRefresh)))
                    _dirty |= History.Add(device, device.LastReading!.Value);
            LastRefresh = now;
            Error = (_provider as ConnectedBatteryProvider)?.WarningKey;
            RefreshCount++;
            if (_dirty) await SaveHistoryAsync();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Error = ex is UnauthorizedAccessException ? "AccessDenied" : "RefreshFailed";
            Trace.WriteLine($"Battery refresh: {ex}");
        }
        finally
        {
            LastRefreshMilliseconds = watch.Elapsed.TotalMilliseconds;
            IsRefreshing = false;
            if (!_disposed) Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private Task SaveHistoryAsync()
    {
        if (!_dirty) return _save;
        History.Normalize(DateTimeOffset.UtcNow);
        _dirty = false;
        return _save = SaveCoreAsync();
    }

    private async Task SaveCoreAsync()
    {
        try
        {
            await _store.SaveAsync(History);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { _dirty = true; Trace.WriteLine($"Battery history save: {ex.Message}"); }
    }

    public async ValueTask LeaveAsync(object client)
    {
        _clients.Remove(client);
        if (_clients.Count == 0)
        {
            if (ReferenceEquals(_shared, this)) _shared = null;
            await DisposeAsync();
        }
        else
        {
            var pair = _clients.First();
            UpdateClient(pair.Key, pair.Value.Settings, pair.Value.Active);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _debounce.Stop();
        _timer.Tick -= OnTimer;
        _debounce.Tick -= OnTimer;
        _provider.DevicesChanged -= OnDevicesChanged;
        _lifetime.Cancel();
        _provider.Dispose();
        if (_refresh is not null) await _refresh;
        await _save;
        await SaveHistoryAsync();
        _lifetime.Dispose();
    }
}
