using System.Net;
using System.Net.NetworkInformation;

namespace BlessedLine.Desktop;

internal sealed record AdapterChoice(NetworkInterface Interface, string Label, bool HasGateway)
{
    public string Id => Interface.Id;

    public static IReadOnlyList<AdapterChoice> Connected()
    {
        var choices = new List<(NetworkInterface Adapter, string Kind, bool HasGateway)>();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            var kind = adapter.NetworkInterfaceType switch
            {
                NetworkInterfaceType.Wireless80211 => "Wi-Fi",
                NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or
                    NetworkInterfaceType.FastEthernetFx or NetworkInterfaceType.FastEthernetT =>
                    IsNamedVpn(adapter) ? "VPN" : "Kablolu ağ",
                NetworkInterfaceType.Ppp or NetworkInterfaceType.Tunnel => "VPN / paylaşım",
                NetworkInterfaceType.Wwanpp or NetworkInterfaceType.Wwanpp2 => "Mobil ağ",
                _ => null
            };
            if (kind is null || adapter.OperationalStatus != OperationalStatus.Up || IsWindowsFilter(adapter))
                continue;

            var properties = adapter.GetIPProperties();
            if (!properties.UnicastAddresses.Any(entry => IsUsableAddress(entry.Address)))
                continue;

            choices.Add((adapter, kind, properties.GatewayAddresses.Any(entry => IsUsableAddress(entry.Address))));
        }

        return choices.OrderByDescending(choice => choice.HasGateway)
            .ThenBy(choice => choice.Kind == "Wi-Fi" ? 0 : choice.Kind == "Kablolu ağ" ? 1 : 2)
            .Select(choice =>
            {
                var label = choices.Count(other => other.Kind == choice.Kind) == 1
                    ? choice.Kind
                    : $"{choice.Kind} · {choice.Adapter.Name}";
                return new AdapterChoice(choice.Adapter, label, choice.HasGateway);
            }).ToArray();
    }

    private static bool IsUsableAddress(IPAddress address) =>
        !IPAddress.IsLoopback(address) && !address.Equals(IPAddress.Any) && !address.Equals(IPAddress.IPv6Any) &&
        !address.IsIPv6LinkLocal && !(address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
        address.GetAddressBytes() is [169, 254, _, _]);

    private static bool IsNamedVpn(NetworkInterface adapter) =>
        HasText(adapter.Description, "TAP-") || HasText(adapter.Description, "Wintun") ||
        HasText(adapter.Description, "WireGuard") || HasText(adapter.Description, "VPN");

    private static bool IsWindowsFilter(NetworkInterface adapter) =>
        HasText(adapter.Name, "Filter-") || HasText(adapter.Name, "Packet Scheduler-") ||
        HasText(adapter.Description, "LightWeight Filter") || HasText(adapter.Description, "Wi-Fi Direct Virtual Adapter") ||
        HasText(adapter.Description, "Packet Scheduler");

    private static bool HasText(string value, string text) => value.Contains(text, StringComparison.OrdinalIgnoreCase);
}
