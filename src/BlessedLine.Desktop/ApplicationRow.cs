using System.ComponentModel;
using System.Windows.Media;
using BlessedLine.Core;

namespace BlessedLine.Desktop;

public sealed class ApplicationRow : INotifyPropertyChanged
{
    public ApplicationRow(string? path, ImageSource fallback)
    {
        Path = path;
        CanControl = ApplicationPolicy.IsControllable(path);
        Name = ApplicationName.FromPath(path);
        _fallback = fallback;
    }

    public string? Path { get; }
    public string Name { get; }
    public bool CanControl { get; }
    public bool Favorite { get; private set; }
    public bool IsBlocked { get; private set; }
    public bool HasControls { get; private set; }
    public bool ShowUsageColor { get; private set; }
    public int UsageLevel => ShowUsageColor ? Math.Max(DownloadRate, UploadRate) switch
    {
        >= 1_000_000 => 2,
        >= 100_000 => 1,
        _ => 0
    } : 0;
    public string GateGlyph => IsBlocked ? "\uE785" : "\uE72E";
    public string GateLabel => TextCatalog.Format(IsBlocked ? "UnblockApp" : "BlockApp");
    public string FavoriteLabel => TextCatalog.Format(Favorite ? "UnfavoriteApp" : "FavoriteApp");

    internal void Sync(bool favorite, bool colors, AppPolicyRule? rule)
    {
        Favorite = favorite;
        ShowUsageColor = colors;
        IsBlocked = rule?.IsBlocked == true;
        HasControls = rule is not null && !rule.IsEmpty;
        RefreshText();
    }
    public ImageSource Icon => _icon ??= (_appId is null ? null : ApplicationIcon.Packaged(_appId))
        ?? ApplicationIcon.Read(Path) ?? _fallback;

    private readonly ImageSource _fallback;
    private ImageSource? _icon;
    private string? _appId;
    private bool _measured;
    internal void SetAppId(string? appId)
    {
        if (appId is null || _appId == appId) return;
        _appId = appId;
        if (_icon is null) return;
        _icon = null;
        PropertyChanged?.Invoke(this, new(nameof(Icon)));
    }
    public long Received { get; private set; }
    public long Sent { get; private set; }
    public long Total => Received + Sent;
    public long DownloadRate { get; private set; }
    public long UploadRate { get; private set; }
    public string TotalText => _measured ? TextCatalog.Bytes(Total) : "—";
    public string ReceivedText => _measured ? TextCatalog.Bytes(Received) : "—";
    public string SentText => _measured ? TextCatalog.Bytes(Sent) : "—";
    public string Details => !_measured
        ? $"{Name}\n{Path}\n{TextCatalog.Format("NoMeasurements")}"
        : $"{Name}\n{TextCatalog.Format("DownloadUploadTotals", TextCatalog.Bytes(Received), TextCatalog.Bytes(Sent))}\n{Path}".TrimEnd();
    internal void RefreshText() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

    public string DownloadRateText => _measured ? TextCatalog.Bytes(DownloadRate) : "—";
    public string UploadRateText => _measured ? TextCatalog.Bytes(UploadRate) : "—";
    public event PropertyChangedEventHandler? PropertyChanged;

    internal void Update(AppTraffic? traffic, double seconds)
    {
        var received = traffic?.Received ?? 0;
        var sent = traffic?.Sent ?? 0;
        _measured |= traffic is not null;
        Received += received;
        Sent += sent;
        var downloadRate = seconds > 0 ? (long)(received / seconds) : 0;
        var uploadRate = seconds > 0 ? (long)(sent / seconds) : 0;
        if (DownloadRate == downloadRate && UploadRate == uploadRate && received == 0 && sent == 0)
            return;

        DownloadRate = downloadRate;
        UploadRate = uploadRate;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}
