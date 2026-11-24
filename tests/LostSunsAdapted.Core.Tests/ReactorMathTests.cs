using HighPowerReactors;
using Xunit;

public class ReactorMathTests
{
    [Theory]
    [InlineData(5.0, "5")]
    [InlineData(1600.0, "1600")]
    [InlineData(0.25, "0.25")]
    public void Plain_numbers_have_no_separators(double value, string expected)
        => Assert.Equal(expected, ReactorMath.FormatNumber(value));

    [Theory]
    [InlineData(500000.0, "500 kW")]
    [InlineData(8250000.0, "8.25 MW")]
    [InlineData(11000000.0, "11 MW")]
    [InlineData(999.0, "999 W")]
    [InlineData(2.5e9, "2.5 GW")]
    public void Power_switches_to_bigger_units(double watts, string expected)
        => Assert.Equal(expected, ReactorMath.FormatScaled(watts, "W"));

    [Theory]
    [InlineData(8000.0, "8000 EC")]
    [InlineData(9000000.0, "9 MEC")]
    public void Charge_scales_only_from_a_million(double ec, string expected)
        => Assert.Equal(expected, ReactorMath.FormatScaled(ec, "EC", 1e6));

    [Fact]
    public void Kilowatts_are_converted_to_watts_first()
        => Assert.Equal("8.25 MW", ReactorMath.FormatScaled(8250.0 * 1000.0, "W"));
}