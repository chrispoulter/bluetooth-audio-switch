using BluetoothAudioSwitch.Bluetooth;

namespace BluetoothAudioSwitch.Tests;

public class DeviceNameMatcherTests
{
    private static readonly string[] Names =
    [
        "Chris's Buds4",
        "WH-1000XM4",
        "Kitchen Speaker",
        "Office Speaker",
    ];

    [Theory]
    [InlineData("Chris's Buds4", "Chris's Buds4")]
    [InlineData("chris's buds4", "Chris's Buds4")]
    [InlineData("Chris’s Buds4", "Chris's Buds4")]
    [InlineData("  Chris's   Buds4 ", "Chris's Buds4")]
    [InlineData("Chris Buds4", "Chris's Buds4")]
    [InlineData("buds", "Chris's Buds4")]
    [InlineData("1000", "WH-1000XM4")]
    [InlineData("kitchen", "Kitchen Speaker")]
    public void Finds_single_match(string query, string expected)
    {
        var result = DeviceNameMatcher.Match(Names, n => n, query);

        var found = Assert.IsType<MatchFound<string>>(result.Value);
        Assert.Equal(expected, found.Match);
    }

    [Fact]
    public void Reports_ambiguous_matches()
    {
        var result = DeviceNameMatcher.Match(Names, n => n, "speaker");

        var ambiguous = Assert.IsType<AmbiguousMatch<string>>(result.Value);
        Assert.Equal(["Kitchen Speaker", "Office Speaker"], ambiguous.Candidates);
    }

    [Fact]
    public void Exact_match_wins_over_substring_match()
    {
        var result = DeviceNameMatcher.Match(["Buds", "Chris's Buds"], n => n, "buds");

        var found = Assert.IsType<MatchFound<string>>(result.Value);
        Assert.Equal("Buds", found.Match);
    }

    [Theory]
    [InlineData("AirPods")]
    [InlineData("")]
    [InlineData("   ")]
    public void Reports_not_found(string query)
    {
        var result = DeviceNameMatcher.Match(Names, n => n, query);

        Assert.IsType<NoMatch>(result.Value);
    }
}
