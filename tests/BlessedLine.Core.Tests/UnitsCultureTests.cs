using System.Globalization;
using BlessedLine.Core;
using Xunit;

namespace BlessedLine.Core.Tests;

public sealed class UnitsCultureTests
{
    [Theory]
    [InlineData("en-US", "1.5", "1.50 MB")]
    [InlineData("tr-TR", "1,5", "1,50 MB")]
    [InlineData("hi-IN", "१.५", "1.50 MB")]
    [InlineData("bn-BD", "১.৫", "1.50 MB")]
    public void ParsesAndFormatsTheSelectedCulture(string cultureName, string input, string formatted)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var count = Units.ParseMegabytes(input, culture);
        Assert.Equal(1_500_000, count);
        Assert.Equal(formatted, Units.Format(count, culture));
    }
}
