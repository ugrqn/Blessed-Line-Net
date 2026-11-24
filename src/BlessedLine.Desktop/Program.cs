using System.Windows.Media;

namespace BlessedLine.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args is ["--traffic" or "--policy-only", var pipe, var owner] &&
            Guid.TryParseExact(pipe, "N", out var id) && int.TryParse(owner, out var parent))
        {
            MemoryBudget.Apply(MemoryBudget.HelperMiB);
            TrafficHelper.Run(id, parent, args[0] == "--traffic");
            return;
        }

        MemoryBudget.Apply(MemoryBudget.DesktopMiB);
        RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
