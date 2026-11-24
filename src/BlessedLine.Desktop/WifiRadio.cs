using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BlessedLine.Desktop;

internal sealed record WifiRecovery(Guid Adapter, Guid Counter, uint[] EnabledPhys);

internal sealed class WifiRadio : SafeHandleZeroOrMinusOneIsInvalid
{
    private const uint RadioState = 4; // wlan_intf_opcode_radio_state
    private const int On = 1;
    private const int Off = 2;

    private WifiRadio() : base(ownsHandle: true) { }

    internal static WifiRadio Open()
    {
        Check(WlanOpenHandle(2, 0, out _, out var client));
        return client;
    }

    internal uint[] SoftwareEnabled(Guid adapter)
    {
        Check(WlanQueryInterface(this, in adapter, RadioState, 0, out var size, out var data, out _));
        try
        {
            if (size < sizeof(uint)) throw new InvalidOperationException();
            var count = Marshal.ReadInt32(data);
            var rowSize = Marshal.SizeOf<PhyState>();
            if (count < 0 || count > 64 || size < sizeof(uint) + count * rowSize)
                throw new InvalidOperationException();

            var enabled = new List<uint>(count);
            for (var i = 0; i < count; i++)
            {
                var state = Marshal.PtrToStructure<PhyState>(data + sizeof(uint) + i * rowSize);
                if (state.Software == On) enabled.Add(state.Index);
            }
            return enabled.ToArray();
        }
        finally { WlanFreeMemory(data); }
    }

    internal void Set(WifiRecovery entry, bool enabled)
    {
        foreach (var index in entry.EnabledPhys)
        {
            var state = new PhyState { Index = index, Software = enabled ? On : Off };
            Check(WlanSetInterface(this, entry.Adapter, RadioState, (uint)Marshal.SizeOf<PhyState>(), in state, 0));
        }
    }

    protected override bool ReleaseHandle() => WlanCloseHandle(handle, 0) == 0;

    private static void Check(uint error)
    {
        if (error != 0) throw new Win32Exception((int)error);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PhyState
    {
        internal uint Index;
        internal int Software, Hardware;
    }

    [DllImport("wlanapi.dll")]
    private static extern uint WlanOpenHandle(uint version, nint reserved, out uint negotiated, out WifiRadio client);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanCloseHandle(nint client, nint reserved);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanQueryInterface(WifiRadio client, in Guid adapter, uint opcode,
        nint reserved, out uint size, out nint data, out int valueType);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanSetInterface(WifiRadio client, in Guid adapter, uint opcode,
        uint size, in PhyState state, nint reserved);

    [DllImport("wlanapi.dll")]
    private static extern void WlanFreeMemory(nint data);
}
