using System.ComponentModel;
using Microsoft.Win32.SafeHandles;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace BlessedLine.Desktop;

internal sealed record ConnectedApp(string Path, string? AppId);

internal static class ConnectedApps
{
    public static List<ConnectedApp> Read()
    {
        var ids = new HashSet<int>();
        AddOwners(ids, 2);  // AF_INET
        AddOwners(ids, 23); // AF_INET6
        var apps = new Dictionary<string, ConnectedApp>(StringComparer.OrdinalIgnoreCase);
        var image = new StringBuilder(32768);
        foreach (var pid in ids)
        {
            try
            {
                using var process = OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION
                if (process.IsInvalid) continue;
                image.Clear();
                int size = image.Capacity;
                if (!QueryFullProcessImageName(process, 0, image, ref size)) continue;
                var path = image.ToString();
                if (apps.ContainsKey(path)) continue;
                var id = new StringBuilder(130);
                uint length = (uint)id.Capacity;
                var packaged = GetApplicationUserModelId(process, ref length, id) == 0;
                apps.Add(path, new(path, packaged ? id.ToString() : null));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
            {
                // Protected processes and processes which exit between snapshots have no verified path.
            }
        }
        return apps.Values.ToList();
    }

    private static void AddOwners(HashSet<int> ids, int family)
    {
        int size = 0;
        uint result = GetExtendedTcpTable(0, ref size, false, family, 5, 0);
        for (int attempt = 0; result == 122 && size >= 4 && attempt < 3; attempt++)
        {
            var capacity = size;
            var buffer = Marshal.AllocHGlobal(capacity);
            try
            {
                result = GetExtendedTcpTable(buffer, ref size, false, family, 5, 0);
                if (result != 0) continue;
                var rowSize = family == 2 ? 24 : 56;
                var count = Math.Min(Marshal.ReadInt32(buffer), (capacity - 4) / rowSize);
                for (int index = 0; index < count; index++)
                {
                    var row = buffer + 4 + index * rowSize;
                    var state = Marshal.ReadInt32(row, family == 2 ? 0 : 48);
                    if (state != 5) continue; // MIB_TCP_STATE_ESTAB
                    var address = new byte[family == 2 ? 4 : 16];
                    Marshal.Copy(row + (family == 2 ? 12 : 24), address, 0, address.Length);
                    var remote = new IPAddress(address);
                    if (IPAddress.IsLoopback(remote.IsIPv4MappedToIPv6 ? remote.MapToIPv4() : remote)) continue;
                    var pid = Marshal.ReadInt32(row, rowSize - 4);
                    if (pid > 4) ids.Add(pid);
                }
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        if (result != 0 && result != 50) throw new Win32Exception((int)result);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder path, ref int length);

    [DllImport("iphlpapi.dll")]
    private static extern uint GetExtendedTcpTable(nint table, ref int size, bool sort, int family, int tableClass, int reserved);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetApplicationUserModelId(Microsoft.Win32.SafeHandles.SafeProcessHandle process, ref uint length, StringBuilder appId);
}
