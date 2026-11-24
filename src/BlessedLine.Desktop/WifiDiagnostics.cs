using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;

namespace BlessedLine.Desktop;

internal sealed record WifiNetwork(string Name, uint Quality, string Security, string Cipher, string Standard,
    uint AccessPoints, string Profile, bool Connected, bool Connectable);
internal sealed record WifiSnapshot(string Adapter, string Overview, WifiNetwork[] Nearby, string Status, bool NeedsLocation);

internal static class WifiDiagnostics
{
    private static string T(string key) => TextCatalog.Format(key);

    internal static async Task<WifiSnapshot> ReadAsync(string? selected, bool scan)
    {
        var adapters = NetworkInterface.GetAllNetworkInterfaces()
            .Where(item => item.NetworkInterfaceType == NetworkInterfaceType.Wireless80211).ToArray();
        var adapter = adapters.FirstOrDefault(item => item.Id == selected)
            ?? adapters.FirstOrDefault(item => item.OperationalStatus == OperationalStatus.Up)
            ?? adapters.FirstOrDefault();
        if (adapter is null || !Guid.TryParse(adapter.Id, out var id))
            return new("", "", [], T("NoWifiAdapter"), false);

        var ip = adapter.GetIPProperties();
        var facts = new StringBuilder();
        void Add(string label, string value) => facts.Append(T(label)).Append("   ").AppendLine(value);
        Add("WifiAdapter", adapter.Description);
        Add("WifiState", T(adapter.OperationalStatus == OperationalStatus.Up ? "WifiConnected" : "WifiDisconnected"));
        Add("WifiAddress", string.Join(", ", ip.UnicastAddresses.Select(item => item.Address.ToString())));
        Add("WifiGateway", string.Join(", ", ip.GatewayAddresses.Select(item => item.Address.ToString())));
        Add("WifiDns", string.Join(", ", ip.DnsAddresses));
        Add("WifiMac", string.Join(":", adapter.GetPhysicalAddress().GetAddressBytes().Select(value => value.ToString("X2"))));
        if (adapter.Speed >= 0) Add("WifiLinkRate", $"{adapter.Speed / 1_000_000d:N1} Mb/s");

        try
        {
            using var radio = WifiRadio.Open();
            if (scan)
            {
                radio.Scan(id);
                await Task.Delay(1800).ConfigureAwait(false);
            }
            if (radio.GetConnection(id) is { } active)
            {
                Add("WifiNetwork", active.Network);
                if (!string.IsNullOrEmpty(active.Profile)) Add("WifiProfile", active.Profile);
                Add("WifiRadioAddress", active.Bssid);
                Add("WifiSignal", $"{active.Signal}%");
                if (radio.ReadChannel(id) is { } channel) Add("WifiChannel", channel.ToString());
                Add("WifiStandard", active.Standard);
                Add("WifiSecurity", $"{active.Security} / {active.Cipher}");
                Add("WifiRxTx", $"↓ {active.Rx / 1000d:N1}   ↑ {active.Tx / 1000d:N1} Mb/s");
            }
            var networks = radio.GetNearby(id);
            return new(adapter.Name, facts.ToString().TrimEnd(), networks,
                networks.Length == 0 ? T("NoNearbyNetworks") : "", false);
        }
        catch (Win32Exception ex)
        {
            var status = ex.NativeErrorCode == 5 ? T("WifiLocationRequired") : TextCatalog.Format("WifiReadError", ex.Message);
            return new(adapter.Name, facts.ToString().TrimEnd(), [], status, ex.NativeErrorCode == 5);
        }
    }
}

internal sealed partial class WifiRadio
{
    internal sealed record Link(string Network, string Profile, string Bssid, uint Signal,
        string Standard, string Security, string Cipher, uint Rx, uint Tx);

    internal Link? GetConnection(Guid adapter)
    {
        var result = WlanQueryInterface(this, in adapter, CurrentConnection, 0, out var size, out var data, out _);
        if (result == 5023) return null; // ERROR_INVALID_STATE while disconnected
        Check(result);
        try
        {
            if (size < Marshal.SizeOf<ConnectionInfo>()) return null;
            var info = Marshal.PtrToStructure<ConnectionInfo>(data);
            if (info.State != 1) return null;
            var association = info.Association;
            return new(association.Ssid.AsText(), info.Profile,
                string.Join(":", association.Bssid.Select(value => value.ToString("X2"))), association.Quality,
                Phy(association.PhyType), Auth(info.Auth, info.Secure != 0), CipherName(info.Cipher), association.Rx, association.Tx);
        }
        finally { WlanFreeMemory(data); }
    }

    internal uint? ReadChannel(Guid adapter)
    {
        if (WlanQueryInterface(this, in adapter, 8, 0, out var size, out var data, out _) != 0) return null;
        try { return size >= sizeof(uint) ? (uint)Marshal.ReadInt32(data) : null; }
        finally { WlanFreeMemory(data); }
    }

    internal void Scan(Guid adapter) => Check(WlanScan(this, in adapter, 0, 0, 0));

    internal WifiNetwork[] GetNearby(Guid adapter)
    {
        Check(WlanGetAvailableNetworkList(this, in adapter, 0, 0, out var data));
        try
        {
            var count = Math.Clamp(Marshal.ReadInt32(data), 0, 512);
            var size = Marshal.SizeOf<AvailableNetwork>();
            var result = new WifiNetwork[count];
            for (var i = 0; i < count; i++)
            {
                var item = Marshal.PtrToStructure<AvailableNetwork>(data + 8 + i * size);
                var standard = string.Join(", ", item.PhyTypes.Take((int)Math.Min(item.PhyCount, 8)).Select(Phy).Distinct());
                result[i] = new(item.Ssid.AsText(), item.Quality, Auth(item.Auth, item.Secure != 0),
                    CipherName(item.Cipher), standard, item.BssidCount, item.Profile, (item.Flags & 1) != 0, item.Connectable != 0);
            }
            return result.OrderByDescending(item => item.Connected).ThenByDescending(item => item.Quality).ToArray();
        }
        finally { WlanFreeMemory(data); }
    }

    private static string Phy(uint value) => value switch
    {
        4 => "802.11a", 5 => "802.11b", 6 => "802.11g", 7 => "Wi-Fi 4 (n)",
        8 => "Wi-Fi 5 (ac)", 9 => "802.11ad", 10 => "Wi-Fi 6 (ax)", 11 => "Wi-Fi 7 (be)", _ => value.ToString()
    };

    private static string Auth(uint value, bool secure) => !secure ? TextCatalog.Format("WifiOpen") : value switch
    {
        1 or 2 => "WEP", 3 => "WPA Enterprise", 4 => "WPA Personal", 5 => "WPA2 Enterprise",
        6 => "WPA2 Personal", 8 => "WPA3 Enterprise 192", 9 => "WPA3 Personal", 10 => "OWE", 11 => "WPA3 Enterprise",
        _ => value.ToString()
    };

    private static string CipherName(uint value) => value switch
    {
        0 => "—", 1 => "WEP-40", 2 => "TKIP", 4 => "CCMP", 5 => "WEP-104", 6 => "BIP",
        8 => "GCMP", 9 => "GCMP-256", 10 => "CCMP-256", 257 => "WEP", _ => value.ToString()
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct Ssid
    {
        public uint Length;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] Value;

        public readonly string AsText()
        {
            if (Length == 0) return TextCatalog.Format("HiddenWifi");
            var text = Encoding.UTF8.GetString(Value, 0, (int)Math.Min(Length, 32));
            return string.Concat(text.Select(ch => char.IsControl(ch) ? '�' : ch));
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Association
    {
        public Ssid Ssid;
        public uint BssType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public byte[] Bssid;
        public uint PhyType, PhyIndex, Quality, Rx, Tx;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ConnectionInfo
    {
        public uint State, Mode;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Profile;
        public Association Association;
        public int Secure, OneX;
        public uint Auth, Cipher;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct AvailableNetwork
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Profile;
        public Ssid Ssid;
        public uint BssType, BssidCount;
        public int Connectable;
        public uint Reason, PhyCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public uint[] PhyTypes;
        public int MorePhy;
        public uint Quality;
        public int Secure;
        public uint Auth, Cipher, Flags, Reserved;
    }

    [DllImport("wlanapi.dll")]
    private static extern uint WlanGetAvailableNetworkList(WifiRadio radio, in Guid adapter, uint flags, nint reserved, out nint data);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanScan(WifiRadio radio, in Guid adapter, nint ssid, nint ie, nint reserved);
}
