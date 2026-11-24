using System.ComponentModel;
using System.Windows.Media;
using BlessedLine.Core;

namespace BlessedLine.Desktop;

public sealed class ApplicationRow : INotifyPropertyChanged
{
    public ApplicationRow(string? path, ImageSource fallback)
    {
        Path = path;
        Icon = ApplicationIcon.Read(path) ?? fallback;
    }

    public string? Path { get; }
    public string Name => ApplicationName.FromPath(Path);
    public ImageSource Icon { get; private set; }
    private string? _appId;
    private bool _measured;
    internal void SetAppId(string? appId)
    {
        if (appId is null || _appId == appId) return;
        _appId = appId;
        Icon = ApplicationIcon.Packaged(appId) ?? Icon;
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
