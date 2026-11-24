using Forms = System.Windows.Forms;

namespace BlessedLine.Desktop;

internal sealed class TrayMenu : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ContextMenuStrip _menu = new();
    private readonly System.Drawing.Icon _symbol = ApplicationIcon.Stock(103); // SIID_NETWORKCONNECT

    public TrayMenu(Action open, Action mini, Action restore, Action exit)
    {
        _menu.Items.Add("BlessedLine’ı aç", null, (_, _) => open());
        _menu.Items.Add("Küçük çubuğu göster", null, (_, _) => mini());
        _menu.Items.Add("Bağlantıyı geri aç", null, (_, _) => restore());
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("Çıkış", null, (_, _) => exit());
        _icon = new Forms.NotifyIcon { Icon = _symbol, Text = "BlessedLine", ContextMenuStrip = _menu, Visible = true };
        _icon.DoubleClick += (_, _) => open();
        _icon.BalloonTipClicked += (_, _) => open();
    }

    public void Update(string down, string up, string remaining)
    {
        var text = $"BlessedLine · ↓ {down} ↑ {up}\n{remaining}";
        _icon.Text = text.Length <= 63 ? text : text[..63];
    }

    public void Notify(string message) =>
        _icon.ShowBalloonTip(3500, "BlessedLine", message, Forms.ToolTipIcon.Info);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        _symbol.Dispose();
    }
}
