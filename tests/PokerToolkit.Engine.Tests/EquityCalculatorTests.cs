using PokerToolkit.Engine;

namespace PokerToolkit.Engine.Tests;

/// <summary>
/// The expected numbers come from counting every possible board, checked against
/// a second, independent implementation of the same algorithm.
/// </summary>
public class EquityCalculatorTests
{
    private static EquityRequest Request(string board, params PlayerSpec[] players)
    {
        var request = new EquityRequest();
        request.Players.AddRange(players);
        request.Board.AddRange(Card.ParseMany(board));
        return request;
    }

    private static void Near(double expected, double actual, double tolerance = 0.00001)
        => Assert.InRange(actual, expected - tolerance, expected + tolerance);

    [Fact]
    public void Aces_against_kings_preflop()
    {
        EquityResult result = EquityCalculator.CalculateExact(
            Request("", PlayerSpec.FromHand("AsAh"), PlayerSpec.FromHand("KsKh")));

        Assert.True(result.IsExact);
        Assert.Equal(1_712_304, result.Trials);
        Near(0.823648, result.Players[0].Win);
        Near(0.005436, result.Players[0].Tie);
        Near(0.826366, result.Players[0].Equity);
        Near(0.173634, result.Players[1].Equity);
    }

    [Fact]
    public void Flush_draw_against_a_set_on_the_flop()
    {
        EquityResult result = EquityCalculator.CalculateExact(
            Request("7s2s9d", PlayerSpec.FromHand("AsKs"), PlayerSpec.FromHand("7h7d")));

        Assert.Equal(990, result.Trials);
        Near(0.246465, result.Players[0].Equity);
        Near(0.753535, result.Players[1].Equity);
    }

    [Fact]
    public void Hand_against_a_range_on_the_turn()
    {
        // The Kd on the board and the two known aces remove combos from the range:
        // KK has 3 left, QQ has 6, AKs has 2. That is 11 hands x 44 river cards.
        EquityResult result = EquityCalculator.CalculateExact(
            Request("Kd7c2s9h", PlayerSpec.FromHand("AhAd"), PlayerSpec.FromRange("KK, QQ, AKs")));

        Assert.Equal(484, result.Trials);
        Near(0.706612, result.Players[0].Equity);
        Near(0.293388, result.Players[1].Equity);
    }

    [Fact]
    public void Three_players_preflop()
    {
        EquityResult result = EquityCalculator.CalculateExact(
            Request("", PlayerSpec.FromHand("AsAh"), PlayerSpec.FromHand("KsKh"), PlayerSpec.FromHand("7c2d")));

        Assert.Equal(1_370_754, result.Trials);
        Near(0.704836, result.Players[0].Equity);
        Near(0.172065, result.Players[1].Equity);
        Near(0.123099, result.Players[2].Equity);
    }

    [Fact]
    public void A_split_pot_on_the_river_is_half_each()
    {
        EquityResult result = EquityCalculator.CalculateExact(
            Request("2c7d9hJsQs", PlayerSpec.FromHand("AsKd"), PlayerSpec.FromHand("AhKc")));

        Assert.Equal(1, result.Trials);
        Assert.Equal(1.0, result.Players[0].Tie);
        Assert.Equal(0.5, result.Players[0].Equity);
        Assert.Equal(0.5, result.Players[1].Equity);
    }

    [Fact]
    public void Monte_carlo_lands_close_to_the_exact_answer()
    {
        EquityResult result = EquityCalculator.CalculateMonteCarlo(
            Request("", PlayerSpec.FromHand("AsAh"), PlayerSpec.FromHand("KsKh")), trials: 200_000, seed: 12345);

        Assert.False(result.IsExact);
        Assert.Equal(200_000, result.Trials);
        Near(0.826366, result.Players[0].Equity, tolerance: 0.01);
        Near(1.0, result.Players[0].Equity + result.Players[1].Equity, tolerance: 0.000001);
    }

    [Fact]
    public void Monte_carlo_handles_range_against_range()
    {
        // A range is exactly even against itself.
        EquityResult result = EquityCalculator.CalculateMonteCarlo(
            Request("", PlayerSpec.FromRange("TT+, AQs+, AKo"), PlayerSpec.FromRange("TT+, AQs+, AKo")),
            trials: 200_000, seed: 7);

        Near(0.5, result.Players[0].Equity, tolerance: 0.01);
    }

    [Fact]
    public void Calculate_counts_small_jobs_and_simulates_big_ones()
    {
        EquityRequest flop = Request("7s2s9d", PlayerSpec.FromHand("AsKs"), PlayerSpec.FromHand("7h7d"));
        EquityRequest preflop = Request("", PlayerSpec.FromHand("AsKs"), PlayerSpec.FromHand("7h7d"));

        Assert.True(EquityCalculator.Calculate(flop).IsExact);
        Assert.False(EquityCalculator.Calculate(preflop, monteCarloTrials: 5_000).IsExact);
    }

    [Fact]
    public void Rejects_impossible_setups()
    {
        // The ace of spades can't be in two hands.
        Assert.Throws<ArgumentException>(() => EquityCalculator.CalculateExact(
            Request("", PlayerSpec.FromHand("AsAh"), PlayerSpec.FromHand("AsKh"))));

        // One player is not a contest.
        Assert.Throws<ArgumentException>(() => EquityCalculator.CalculateExact(
            Request("", PlayerSpec.FromHand("AsAh"))));

        // A range of only aces has nothing left when all four aces are accounted for.
        Assert.Throws<ArgumentException>(() => EquityCalculator.CalculateExact(
            Request("AdAc5s", PlayerSpec.FromHand("AsAh"), PlayerSpec.FromRange("AA"))));
    }
}
