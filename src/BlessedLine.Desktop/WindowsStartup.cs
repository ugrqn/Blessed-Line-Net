using System.Diagnostics;
using System.Security;
using Microsoft.Win32;

namespace BlessedLine.Desktop;

internal static class WindowsStartup
{
    internal const string TrayArgument = "--tray";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string EntryName = "BlessedLine";

    // Windows owns the Startup Apps approval state. Only maintain this app's Run entry.
    internal static bool SetEnabled(bool enabled)
    {
        try
        {
            using var run = Registry.CurrentUser.CreateSubKey(RunKey);
            if (!enabled)
            {
                run.DeleteValue(EntryName, throwOnMissingValue: false);
                return true;
            }

            var executable = Environment.ProcessPath;
            if (executable is null) return false;
            var command = $"\"{executable}\" {TrayArgument}";
            if (command.Length > 260) return false;
            if (!string.Equals(run.GetValue(EntryName) as string, command, StringComparison.Ordinal))
                run.SetValue(EntryName, command, RegistryValueKind.String);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or System.IO.IOException)
        {
            Debug.WriteLine(ex);
            return false;
        }
    }
}
