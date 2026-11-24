using BlessedLine.Core;
using Xunit;

namespace BlessedLine.Core.Tests;

public sealed class WifiAdvertisementsTests
{
    [Fact]
    public void ReadsAdvertisedRegulatoryCountryAndExplicitDeviceAttributes()
    {
        var result = WifiAdvertisements.Read([
            7, 3, (byte)'T', (byte)'R', 0x20,
            221, 4, 0x50, 0x6F, 0x9A, 0x09,
            221, 29, 0, 0x50, 0xF2, 4,
            0x10, 0x21, 0, 3, (byte)'L', (byte)'a', (byte)'b',
            0x10, 0x23, 0, 2, (byte)'A', (byte)'1',
            0x10, 0x54, 0, 8, 0, 10, 0, 0x50, 0xF2, 4, 0, 1
        ]);
        Assert.Equal("TR", result.Country);
        Assert.True(result.Direct);
        Assert.Equal("Lab", result.Manufacturer);
        Assert.Equal("A1", result.Model);
        Assert.Equal(AdvertisedWifiDevice.Phone, result.Device);
    }

    [Fact]
    public void RejectsTruncatedMetadataAndReservedCountryCodes()
    {
        var result = WifiAdvertisements.Read([7, 3, (byte)'0', (byte)'0', 0x20, 221, 8, 0, 0x50, 0xF2, 4]);
        Assert.Null(result.Country);
        Assert.Null(result.Manufacturer);
        Assert.Equal(AdvertisedWifiDevice.Unknown, result.Device);
        Assert.False(result.Direct);
    }
}
