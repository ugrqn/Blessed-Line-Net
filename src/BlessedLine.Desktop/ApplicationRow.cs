using System.ComponentModel;
using System.Windows.Media;
using BlessedLine.Core;

namespace BlessedLine.Desktop;

public sealed class ApplicationRow : INotifyPropertyChanged
{
    public ApplicationRow(string? path, ImageSource fallback)
    {
        Path = path;
        Name = ApplicationName.FromPath(path);
        Icon = ApplicationIcon.Read(path) ?? fallback;
    }

    public string? Path { get; }
    public string Name { get; }
    public ImageSource Icon { get; }
    public long Received { get; private set; }
    public long Sent { get; private set; }
    public long Total => Received + Sent;
    public long DownloadRate { get; private set; }
    public long UploadRate { get; private set; }
    public string TotalText => Units.Format(Total);
    public string Details => $"{Name}\nİndirilen {Units.Format(Received)} · Yüklenen {Units.Format(Sent)}\n{Path}".TrimEnd();
    public string DownloadRateText => Units.Format(DownloadRate);
    public string UploadRateText => Units.Format(UploadRate);
    public event PropertyChangedEventHandler? PropertyChanged;

    internal void Update(AppTraffic? traffic, double seconds)
    {
        var received = traffic?.Received ?? 0;
        var sent = traffic?.Sent ?? 0;
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
