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

        Assert.Equal(MatchKind.Found, result.Kind);
        Assert.Equal(expected, result.Match);
    }

    [Fact]
    public void Reports_ambiguous_matches()
    {
        var result = DeviceNameMatcher.Match(Names, n => n, "speaker");

        Assert.Equal(MatchKind.Ambiguous, result.Kind);
        Assert.Equal(["Kitchen Speaker", "Office Speaker"], result.Candidates);
    }

    [Fact]
    public void Exact_match_wins_over_substring_match()
    {
        var result = DeviceNameMatcher.Match(["Buds", "Chris's Buds"], n => n, "buds");

        Assert.Equal(MatchKind.Found, result.Kind);
        Assert.Equal("Buds", result.Match);
    }

    [Theory]
    [InlineData("AirPods")]
    [InlineData("")]
    [InlineData("   ")]
    public void Reports_not_found(string query)
    {
        var result = DeviceNameMatcher.Match(Names, n => n, query);

        Assert.Equal(MatchKind.NotFound, result.Kind);
        Assert.Null(result.Match);
    }
}
