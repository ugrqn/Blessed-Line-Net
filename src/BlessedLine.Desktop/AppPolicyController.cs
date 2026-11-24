using System.Diagnostics;

namespace BlessedLine.Desktop;

internal sealed class AppPolicyController(WindowStateData state, SettingsStore store, TrafficMeasurement measurement)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly HashSet<string> _queries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AppTraffic> _trayTotals = new(StringComparer.OrdinalIgnoreCase);
    private TimeSpan _lastSave;
    private bool _dirty;

    internal event Action? Changed;
    internal event Action<string>? Failed;
    internal bool HasByteBudgets => state.AppRules.Values.Any(rule => rule.QuotaBytes > 0);
    internal AppPolicyRule? Find(string? path) => path is not null && state.AppRules.TryGetValue(path, out var rule) ? rule : null;

    internal async Task<PolicyResult> ToggleAsync(string path)
    {
        await _gate.WaitAsync();
        try
        {
            var rule = Find(path) ?? new AppPolicyRule();
            var next = !rule.IsBlocked;
            var result = await measurement.ApplyPolicyAsync(new(PolicyOperation.Block, path, next ? 1 : 0));
            if (!result.Success) return result;
            rule.Blocked = next;
            if (!next && rule.BudgetBlocked)
            {
                rule.BudgetBlocked = false;
                rule.QuotaBytes = 0;
                rule.UsedBytes = 0;
            }
            return Commit(path, rule);
        }
        finally { _gate.Release(); }
    }

    internal async Task<PolicyResult> SetUploadAsync(string path, long kbps)
    {
        await _gate.WaitAsync();
        try
        {
            var rule = Find(path) ?? new AppPolicyRule();
            if (kbps == rule.UploadKbps) return new(true);
            var result = await measurement.ApplyPolicyAsync(new(PolicyOperation.Upload, path, kbps));
            if (!result.Success) return result;
            rule.UploadKbps = kbps;
            return Commit(path, rule);
        }
        finally { _gate.Release(); }
    }

    internal async Task<PolicyResult> SetQuotaAsync(string path, long bytes)
    {
        if (bytes < 0 || !ApplicationPolicy.IsControllable(path)) return new(false, "InvalidAppPolicy");
        await _gate.WaitAsync();
        try
        {
            var rule = Find(path) ?? new AppPolicyRule();
            if (rule.QuotaBytes == bytes)
            {
                if (bytes > 0 && measurement.CanStart) measurement.Start();
                _queries.Remove(path);
                return new(true);
            }
            if (rule.BudgetBlocked && !rule.Blocked)
            {
                var result = await measurement.ApplyPolicyAsync(new(PolicyOperation.Block, path, 0));
                if (!result.Success) return result;
            }
            rule.BudgetBlocked = false;
            rule.QuotaBytes = bytes;
            rule.UsedBytes = 0;
            if (bytes > 0 && measurement.CanStart) measurement.Start();
            return Commit(path, rule);
        }
        finally { _gate.Release(); }
    }

    internal async Task<PolicyResult> ClearOneAsync(string path)
    {
        await _gate.WaitAsync();
        try
        {
            var rule = Find(path);
            if (rule is null) return new(true);
            if (rule.IsBlocked)
            {
                var result = await measurement.ApplyPolicyAsync(new(PolicyOperation.Block, path, 0));
                if (!result.Success) return result;
                rule.Blocked = rule.BudgetBlocked = false;
                rule.QuotaBytes = rule.UsedBytes = 0;
            }
            if (rule.UploadKbps > 0)
            {
                var result = await measurement.ApplyPolicyAsync(new(PolicyOperation.Upload, path, 0));
                if (!result.Success)
                {
                    Commit(path, rule);
                    return result;
                }
            }
            return Commit(path, new AppPolicyRule());
        }
        finally { _gate.Release(); }
    }

    internal async Task<PolicyResult> ClearAllAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var result = await measurement.ApplyPolicyAsync(new(PolicyOperation.ClearAll));
            if (!result.Success && !result.FirewallCleared) return result;
            if (result.Success) state.AppRules.Clear();
            else
            {
                foreach (var (path, rule) in state.AppRules.ToArray())
                {
                    rule.Blocked = rule.BudgetBlocked = false;
                    rule.QuotaBytes = rule.UsedBytes = 0;
                    if (rule.IsEmpty) state.AppRules.Remove(path);
                }
            }
            _queries.Clear();
            _dirty = true;
            Changed?.Invoke();
            var saved = Save();
            return result.Success ? saved : result;
        }
        finally { _gate.Release(); }
    }

    internal void MeasureInTray()
    {
        if (!HasByteBudgets || measurement.Drain() is not { } reading) return;
        foreach (var item in reading.Traffic)
        {
            var key = item.Path ?? "";
            if (_trayTotals.Count >= ApplicationTraffic.MaxApplications && !_trayTotals.ContainsKey(key)) key = "";
            _trayTotals.TryGetValue(key, out var old);
            _trayTotals[key] = new(key.Length == 0 ? null : key, old.Received + item.Received, old.Sent + item.Sent);
        }
        Observe(reading.Traffic);
    }

    internal AppTraffic[] TakeTrayTotals()
    {
        var batch = _trayTotals.Values.ToArray();
        _trayTotals.Clear();
        return batch;
    }

    internal void Observe(AppTraffic[] samples)
    {
        foreach (var signal in samples)
        {
            if (signal.Path is not { } path || Find(path) is not { QuotaBytes: > 0 } rule || rule.IsBlocked) continue;
            if (signal.Received == 0 && signal.Sent == 0) continue;
            var remaining = Math.Max(0, rule.QuotaBytes - rule.UsedBytes);
            rule.UsedBytes += Math.Min(remaining, signal.Received) + Math.Min(Math.Max(0, remaining - signal.Received), signal.Sent);
            _dirty = true;
            if (rule.UsedBytes >= rule.QuotaBytes && _queries.Add(path))
                _ = EnforceBudgetAsync(path);
        }
        if (_dirty && _clock.Elapsed - _lastSave >= TimeSpan.FromSeconds(6)) Save();
    }

    private async Task EnforceBudgetAsync(string path)
    {
        await _gate.WaitAsync();
        try
        {
            var rule = Find(path);
            if (rule is not { QuotaBytes: > 0 } || rule.IsBlocked || rule.UsedBytes < rule.QuotaBytes)
            {
                _queries.Remove(path);
                return;
            }
            var result = await measurement.ApplyPolicyAsync(new(PolicyOperation.Block, path, 1));
            if (result.Success)
            {
                rule.BudgetBlocked = true;
                Commit(path, rule);
                _queries.Remove(path);
            }
            else Failed?.Invoke(Describe(result));
            // Re-entering the editor resets this guard after a failed system operation.
        }
        finally { _gate.Release(); }
    }

    private PolicyResult Commit(string path, AppPolicyRule rule)
    {
        if (rule.IsEmpty) state.AppRules.Remove(path);
        else state.AppRules[path] = rule;
        _queries.Remove(path);
        _dirty = true;
        Changed?.Invoke();
        return Save();
    }

    private PolicyResult Save()
    {
        _lastSave = _clock.Elapsed;
        try
        {
            store.Save(state);
            _dirty = false;
            return new(true);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine(ex);
            return new(false, "SaveError");
        }
    }

    internal static string Describe(PolicyResult result) => result.Success
        ? TextCatalog.Format("PolicySaved") : TextCatalog.Format("PolicyError", TextCatalog.StatusOrLiteral(result.Error));
}
