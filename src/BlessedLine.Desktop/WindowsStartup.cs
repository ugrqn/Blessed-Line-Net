using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;

namespace BlessedLine.Desktop;

internal static class WindowsStartup
{
    internal const string TrayArgument = "--tray";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovalRoot = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";
    private const string EntryName = "BlessedLine";
    private const string ShortcutName = EntryName + ".lnk";

    internal static bool SetEnabled(bool enabled)
    {
        var startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        if (string.IsNullOrEmpty(startup)) return false;
        var shortcut = Path.Combine(startup, ShortcutName);
        try
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (!enabled)
            {
                File.Delete(shortcut);
                run?.DeleteValue(EntryName, throwOnMissingValue: false);
                return true;
            }

            var executable = Environment.ProcessPath;
            if (executable is null) return false;
            Directory.CreateDirectory(startup);
            if (run?.GetValue(EntryName) is not null) MigrateApproval();
            WriteShortcut(shortcut, executable);
            run?.DeleteValue(EntryName, throwOnMissingValue: false);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException or
                                   COMException or TargetInvocationException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            Debug.WriteLine(ex);
            return false;
        }
    }

    private static void MigrateApproval()
    {
        using var previous = Registry.CurrentUser.OpenSubKey($@"{ApprovalRoot}\Run");
        if (previous?.GetValue(EntryName) is not byte[] { Length: >= 12 } approval || (approval[0] & 1) == 0) return;
        using var folder = Registry.CurrentUser.CreateSubKey($@"{ApprovalRoot}\StartupFolder");
        if (folder.GetValue(ShortcutName) is null) folder.SetValue(ShortcutName, approval, RegistryValueKind.Binary);
    }

    private static void WriteShortcut(string shortcut, string executable)
    {
        var type = Type.GetTypeFromProgID("WScript.Shell", throwOnError: true)!;
        var shell = Activator.CreateInstance(type)!;
        var temporary = Path.Combine(Path.GetTempPath(), $"BlessedLine-{Guid.NewGuid():N}.lnk");
        try
        {
            if (File.Exists(shortcut))
            {
                object existing = ((dynamic)shell).CreateShortcut(shortcut);
                try
                {
                    dynamic link = existing;
                    if (string.Equals((string)link.TargetPath, executable, StringComparison.OrdinalIgnoreCase) &&
                        (string)link.Arguments == TrayArgument && (string)link.Description == EntryName &&
                        (string)link.IconLocation == executable + ",0") return;
                }
                finally { Marshal.ReleaseComObject(existing); }
            }
            object created = ((dynamic)shell).CreateShortcut(temporary);
            try
            {
                dynamic link = created;
                link.TargetPath = executable;
                link.Arguments = TrayArgument;
                link.Description = EntryName;
                link.IconLocation = executable + ",0";
                link.WorkingDirectory = Path.GetDirectoryName(executable);
                link.Save();
            }
            finally { Marshal.ReleaseComObject(created); }
            File.Move(temporary, shortcut, overwrite: true);
        }
        finally
        {
            Marshal.ReleaseComObject(shell);
            File.Delete(temporary);
        }
    }
}
