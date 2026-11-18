using PokerToolkit.Engine;

namespace PokerToolkit.Engine.Tests;

public class HandRangeTests
{
    [Theory]
    [InlineData("AA", 6)]
    [InlineData("AKs", 4)]
    [InlineData("AKo", 12)]
    [InlineData("AK", 16)]
    [InlineData("TT+", 30)]
    [InlineData("22-66", 30)]
    [InlineData("A2s+", 48)]
    [InlineData("A2s-A5s", 16)]
    [InlineData("KTo+", 36)]
    [InlineData("aks, akO", 16)]      // case doesn't matter
    [InlineData("AA AA, AA", 6)]      // repeats don't double count
    [InlineData("", 0)]
    public void Parses_notation_into_the_right_number_of_combos(string text, int expectedCombos)
    {
        Assert.Equal(expectedCombos, HandRange.Parse(text).ComboCount);
    }

    [Theory]
    [InlineData("AAs")]
    [InlineData("XYZ")]
    [InlineData("A2s-K5s")]
    [InlineData("A2s-A5o")]
    [InlineData("A")]
    public void Rejects_bad_notation(string text)
    {
        Assert.Throws<FormatException>(() => HandRange.Parse(text));
        Assert.False(HandRange.TryParse(text, out _, out string? error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Writes_compact_notation()
    {
        Assert.Equal("TT+, AJs+, KQs, AKo", HandRange.Parse("AA,KK,QQ,JJ,TT,AKs,AQs,AJs,KQs,AKo").ToString());
        Assert.Equal("22+", HandRange.Parse("22+").ToString());
        Assert.Equal("A5s-A2s", HandRange.Parse("A2s-A5s").ToString());
    }

    [Fact]
    public void Notation_round_trips_for_every_slider_position()
    {
        for (double percent = 0; percent <= 100; percent += 2.5)
        {
            HandRange original = HandRange.TopPercent(percent);
            HandRange reparsed = HandRange.Parse(original.ToString());

            Assert.Equal(original.ToString(), reparsed.ToString());
            Assert.Equal(original.ComboCount, reparsed.ComboCount);
        }
    }

    [Fact]
    public void Grid_cells_are_named_like_a_range_chart()
    {
        Assert.Equal("AA", HandRange.CellName(0, 0));
        Assert.Equal("AKs", HandRange.CellName(0, 1));
        Assert.Equal("AKo", HandRange.CellName(1, 0));
        Assert.Equal("22", HandRange.CellName(12, 12));

        HandRange range = HandRange.Parse("AKs");
        Assert.True(range[0, 1]);
        Assert.False(range[1, 0]);
    }

    [Fact]
    public void Dead_cards_remove_combos()
    {
        HandRange aces = HandRange.Parse("AA");
        Assert.Equal(6, aces.GetCombos().Count);
        Assert.Equal(3, aces.GetCombos(Card.Parse("As").Mask).Count);
    }

    [Fact]
    public void Top_percent_grows_from_the_best_hands()
    {
        Assert.True(HandRange.TopPercent(0).IsEmpty);
        Assert.Equal(HandRange.TotalCombos, HandRange.TopPercent(100).ComboCount);

        HandRange tight = HandRange.TopPercent(2.5);
        Assert.True(tight[0, 0]);   // AA
        Assert.True(tight[1, 1]);   // KK
        Assert.False(tight[12, 12]); // 22

        Assert.InRange(HandRange.TopPercent(20).Percent, 18.5, 21.5);
    }
}
