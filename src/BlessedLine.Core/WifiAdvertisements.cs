using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace BlessedLine.Core;

public enum AdvertisedWifiDevice { Unknown, NetworkEquipment, Phone, Computer }

public sealed record WifiAdvertisements(string? Country, bool Direct, string? Manufacturer, string? Model,
    AdvertisedWifiDevice Device)
{
    public static WifiAdvertisements Read(ReadOnlySpan<byte> elements)
    {
        string? country = null, manufacturer = null, model = null;
        var direct = false;
        var device = AdvertisedWifiDevice.Unknown;

        while (elements.Length >= 2)
        {
            var kind = elements[0];
            var length = elements[1];
            elements = elements[2..];
            if (elements.Length < length) break;
            var data = elements[..length];
            elements = elements[length..];

            if (kind == 7 && data.Length >= 3 && UpperAscii(data[0]) && UpperAscii(data[1]))
                country ??= Encoding.ASCII.GetString(data[..2]);
            if (kind != 221 || data.Length < 4) continue;
            direct |= data.StartsWith((ReadOnlySpan<byte>)[0x50, 0x6F, 0x9A, 0x09]);
            if (!data.StartsWith((ReadOnlySpan<byte>)[0x00, 0x50, 0xF2, 0x04])) continue;

            // Wi-Fi Simple Configuration (WSC) vendor element and its big-endian attributes.
            data = data[4..];
            while (data.Length >= 4)
            {
                var attribute = BinaryPrimitives.ReadUInt16BigEndian(data);
                var size = BinaryPrimitives.ReadUInt16BigEndian(data[2..]);
                data = data[4..];
                if (data.Length < size) break;
                var value = data[..size];
                data = data[size..];

                if (attribute == 0x1021) manufacturer ??= Display(value, 64);
                if (attribute == 0x1023) model ??= Display(value, 32);
                if (attribute == 0x1054 && value.Length == 8 &&
                    value[2..6].SequenceEqual((ReadOnlySpan<byte>)[0x00, 0x50, 0xF2, 0x04]))
                    device = BinaryPrimitives.ReadUInt16BigEndian(value) switch
                    {
                        1 => AdvertisedWifiDevice.Computer,
                        6 => AdvertisedWifiDevice.NetworkEquipment,
                        10 => AdvertisedWifiDevice.Phone,
                        _ => AdvertisedWifiDevice.Unknown
                    };
            }
        }
        return new(country, direct, manufacturer, model, device);
    }

    private static bool UpperAscii(byte value) => value is >= (byte)'A' and <= (byte)'Z';

    private static string? Display(ReadOnlySpan<byte> value, int maximum)
    {
        var text = Encoding.UTF8.GetString(value[..Math.Min(value.Length, maximum)]);
        text = string.Concat(text.Where(ch => !char.IsControl(ch) && char.GetUnicodeCategory(ch) != UnicodeCategory.Format)).Trim();
        return text.Length == 0 ? null : text;
    }
}
