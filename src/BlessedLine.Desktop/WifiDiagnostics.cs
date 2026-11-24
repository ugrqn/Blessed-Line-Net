using System.ComponentModel;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using BlessedLine.Core;

namespace BlessedLine.Desktop;

internal sealed record WifiFact(string Label, string Value);
internal sealed record WifiAccessPoint(string SsidId, string Bssid, int Rssi, uint FrequencyKhz, uint BssType,
    bool Secure, WifiAdvertisements Advertisements);
internal sealed record WifiNetwork(string Name, uint Quality, string Security, string Cipher, string Standard,
    uint AccessPoints, string Profile, bool Connected, bool Connectable, WifiAccessPoint? Radio);
internal sealed record WifiSnapshot(WifiFact[] Overview, WifiNetwork[] Nearby, string Status, bool NeedsLocation);

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
            return new([], [], T("NoWifiAdapter"), false);

        var ip = adapter.GetIPProperties();
        var facts = new List<WifiFact>();
        void Add(string label, string value) => facts.Add(new(T(label), string.IsNullOrEmpty(value) ? "—" : value));
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
            var accessPoints = radio.GetAccessPoints(id);
            if (radio.GetConnection(id) is { } active)
            {
                Add("WifiNetwork", active.Network);
                if (!string.IsNullOrEmpty(active.Profile)) Add("WifiProfile", active.Profile);
                Add("WifiRadioAddress", active.Bssid);
                Add("WifiSignal", $"{active.Signal}%");
                if (radio.ReadChannel(id) is { } channel) Add("WifiChannel", channel.ToString(TextCatalog.Culture));
                Add("WifiStandard", active.Standard);
                Add("WifiSecurity", $"{active.Security} / {active.Cipher}");
                Add("WifiRxTx", $"↓ {active.Rx / 1000d:N1}   ↑ {active.Tx / 1000d:N1} Mb/s");
                AddAccessPointFacts(facts, accessPoints.FirstOrDefault(point => point.Bssid == active.Bssid));
            }
            var networks = radio.GetNearby(id, accessPoints);
            return new(facts.ToArray(), networks,
                networks.Length == 0 ? T("NoNearbyNetworks") : "", false);
        }
        catch (Win32Exception ex)
        {
            var status = ex.NativeErrorCode == 5 ? T("WifiLocationRequired") : TextCatalog.Format("WifiReadError", ex.Message);
            return new(facts.ToArray(), [], status, ex.NativeErrorCode == 5);
        }
    }

    internal static void AddAccessPointFacts(List<WifiFact> facts, WifiAccessPoint? point)
    {
        void Add(string label, string value) => facts.Add(new(T(label), value));
        var advertised = point?.Advertisements;
        var country = advertised?.Country;
        Add("WifiCountry", country is null ? T("WifiUnavailable") : FormatCountry(country));
        Add("WifiCity", T("WifiUnavailable"));
        Add("WifiDevice", T(advertised?.Device switch
        {
            AdvertisedWifiDevice.Phone => "WifiPhone",
            AdvertisedWifiDevice.Computer => "WifiComputer",
            AdvertisedWifiDevice.NetworkEquipment => "WifiEquipment",
            _ => "WifiUnavailable"
        }));
        if (point is null) return;
        if (advertised?.Manufacturer is { } manufacturer) Add("WifiManufacturer", manufacturer);
        if (advertised?.Model is { } model) Add("WifiModel", model);
        Add("WifiFrequency", $"{point.FrequencyKhz / 1000d:N0} MHz");
        Add("WifiRssi", $"{point.Rssi} dBm");
        Add("WifiTopology", point.BssType switch
        {
            1 => T("WifiInfrastructure"),
            2 => T("WifiPeerToPeer"),
            _ => T("WifiUnavailable")
        });
        if (point.Advertisements.Direct) Add("WifiCapabilities", "Wi-Fi Direct");
    }

    internal static int? Channel(uint khz)
    {
        var mhz = (int)(khz / 1000);
        return mhz switch
        {
            2484 => 14,
            >= 2412 and <= 2472 => (mhz - 2407) / 5,
            >= 5005 and <= 5895 => (mhz - 5000) / 5,
            5935 => 2,
            >= 5955 and <= 7115 => (mhz - 5950) / 5,
            >= 58320 and <= 70200 => (mhz - 56160) / 2160,
            _ => null
        };
    }

    private static string FormatCountry(string country)
    {
        try { return $"{new RegionInfo(country).DisplayName} ({country})"; }
        catch (ArgumentException) { return country; }
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

    internal WifiNetwork[] GetNearby(Guid adapter, WifiAccessPoint[] accessPoints)
    {
        Check(WlanGetAvailableNetworkList(this, in adapter, 0, 0, out var data));
        try
        {
            var count = Math.Clamp(Marshal.ReadInt32(data), 0, 512);
            var size = Marshal.SizeOf<AvailableNetwork>();
            var strongest = accessPoints.GroupBy(point => (point.SsidId, point.Secure))
                .ToDictionary(group => group.Key, group => group.MaxBy(point => point.Rssi));
            var result = new WifiNetwork[count];
            for (var i = 0; i < count; i++)
            {
                var item = Marshal.PtrToStructure<AvailableNetwork>(data + 8 + i * size);
                var standard = string.Join(", ", item.PhyTypes.Take((int)Math.Min(item.PhyCount, 8)).Select(Phy).Distinct());
                result[i] = new(item.Ssid.AsText(), item.Quality, Auth(item.Auth, item.Secure != 0),
                    CipherName(item.Cipher), standard, item.BssidCount, item.Profile, (item.Flags & 1) != 0, item.Connectable != 0,
                    item.Ssid.Length == 0 ? null : strongest.GetValueOrDefault((item.Ssid.Identity, item.Secure != 0)));
            }
            return result.OrderByDescending(item => item.Connected).ThenByDescending(item => item.Quality).ToArray();
        }
        finally { WlanFreeMemory(data); }
    }

    internal WifiAccessPoint[] GetAccessPoints(Guid adapter)
    {
        // Some Windows versions expose aggregate networks without per-access-point data.
        if (WlanGetNetworkBssList(this, in adapter, 0, 3, false, 0, out var list) != 0) return [];
        try
        {
            var byteCount = Marshal.ReadInt32(list);
            var count = Marshal.ReadInt32(list, 4);
            var entrySize = Marshal.SizeOf<BssEntry>();
            if (byteCount < 8 || byteCount > 4 * 1024 * 1024 || count < 0 || count > (byteCount - 8) / entrySize)
                return [];

            var results = new List<WifiAccessPoint>(Math.Min(count, 512));
            var information = new byte[4096];
            for (var i = 0; i < Math.Min(count, 512); i++)
            {
                var start = 8 + i * entrySize;
                var pointer = list + start;
                var entry = Marshal.PtrToStructure<BssEntry>(pointer);
                var bssid = string.Join(":", entry.Bssid.Select(value => value.ToString("X2")));
                var length = 0;
                if (entry.IeOffset >= entrySize && entry.IeOffset <= byteCount - start &&
                    entry.IeSize <= byteCount - start - entry.IeOffset)
                {
                    length = (int)Math.Min(entry.IeSize, (uint)information.Length);
                    Marshal.Copy(pointer + (int)entry.IeOffset, information, 0, length);
                }
                var advertised = WifiAdvertisements.Read(information.AsSpan(0, length));
                results.Add(new(entry.Ssid.Identity, bssid, entry.Rssi, entry.Frequency, entry.BssType,
                    (entry.Capability & 0x10) != 0, advertised));
            }
            return results.ToArray();
        }
        finally { WlanFreeMemory(list); }
    }

    // Fixed-width Windows SDK WLAN_BSS_ENTRY, including its rate array.
    [StructLayout(LayoutKind.Sequential)]
    private struct BssEntry
    {
        public Ssid Ssid;
        public uint PhyId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public byte[] Bssid;
        public uint BssType, PhyType;
        public int Rssi;
        public uint Quality;
        public byte InRegDomain;
        public ushort BeaconPeriod;
        public ulong Timestamp, HostTimestamp;
        public ushort Capability;
        public uint Frequency;
        public uint RatesLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 126)] public ushort[] Rates;
        public uint IeOffset, IeSize;
    }

    [DllImport("wlanapi.dll")]
    private static extern uint WlanGetNetworkBssList(WifiRadio radio, in Guid adapter, nint ssid, uint bssType,
        [MarshalAs(UnmanagedType.Bool)] bool securityEnabled, nint reserved, out nint list);

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

        public readonly string Identity => Convert.ToHexString(Value.AsSpan(0, (int)Math.Min(Length, 32)));

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
