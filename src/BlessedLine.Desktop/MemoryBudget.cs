using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BlessedLine.Desktop;

internal static class MemoryBudget
{
    internal const int DesktopMiB = 77;
    internal const int HelperMiB = 21;
    private const uint HardMaximumEnable = 4;

    internal static void Apply(int maxMiB)
    {
        const int mib = 1024 * 1024;
        if (!SetProcessWorkingSetSizeEx(GetCurrentProcess(), 8 * mib, (nuint)(maxMiB * mib), HardMaximumEnable))
            Debug.WriteLine($"BlessedLine working-set cap was declined: {Marshal.GetLastPInvokeError()}");
    }

    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessWorkingSetSizeEx(nint process, nuint minimum, nuint maximum, uint flags);
}
