using PokerToolkit.Engine;

namespace PokerToolkit.Engine.Tests;

public class PotMathTests
{
    private const int Digits = 6;

    [Fact]
    public void Half_pot_bet_needs_twenty_five_percent()
    {
        Assert.Equal(0.25, PotMath.RequiredEquity(pot: 100, bet: 50), Digits);
        Assert.Equal(3.0, PotMath.PotOddsRatio(pot: 100, bet: 50), Digits);
    }

    [Fact]
    public void Calling_with_exactly_the_required_equity_breaks_even()
    {
        Assert.Equal(0.0, PotMath.CallEv(equity: 0.25, pot: 100, bet: 50), Digits);
        Assert.True(PotMath.CallEv(0.30, 100, 50) > 0);
        Assert.True(PotMath.CallEv(0.20, 100, 50) < 0);
    }

    [Fact]
    public void Implied_odds_make_up_the_shortfall()
    {
        // 20% equity facing 50 into 100: winning 50 more when you hit makes the call break even.
        double extra = PotMath.ImpliedOddsNeeded(equity: 0.20, pot: 100, bet: 50);
        Assert.Equal(50.0, extra, Digits);
        Assert.Equal(0.0, 0.20 * (150 + extra) - 0.80 * 50, Digits);
    }

    [Fact]
    public void Bluff_and_defense_frequencies_add_to_one()
    {
        Assert.Equal(0.5, PotMath.BluffBreakEven(pot: 100, bet: 100), Digits);
        Assert.Equal(2.0 / 3.0, PotMath.MinimumDefenseFrequency(pot: 100, bet: 50), Digits);
        Assert.Equal(1.0, PotMath.BluffBreakEven(80, 60) + PotMath.MinimumDefenseFrequency(80, 60), Digits);
    }

    [Fact]
    public void Bet_ev_blends_folds_and_calls()
    {
        Assert.Equal(100.0, PotMath.BetEv(pot: 100, bet: 50, foldFrequency: 1, equityWhenCalled: 0), Digits);
        Assert.Equal(-50.0, PotMath.BetEv(pot: 100, bet: 50, foldFrequency: 0, equityWhenCalled: 0), Digits);

        // A pure bluff that works exactly at the break-even rate earns nothing.
        double breakEven = PotMath.BluffBreakEven(100, 50);
        Assert.Equal(0.0, PotMath.BetEv(100, 50, breakEven, 0), Digits);
    }

    [Fact]
    public void Outs_convert_to_exact_probabilities()
    {
        Assert.Equal(9.0 / 47, PotMath.OutsToEquity(outs: 9, cardsToCome: 1, unseenCards: 47), Digits);
        Assert.Equal(9.0 / 46, PotMath.OutsToEquity(outs: 9, cardsToCome: 1, unseenCards: 46), Digits);
        Assert.Equal(1 - (38.0 / 47) * (37.0 / 46), PotMath.OutsToEquity(outs: 9, cardsToCome: 2, unseenCards: 47), Digits);
        Assert.Equal(0.0, PotMath.OutsToEquity(0, 2, 47), Digits);

        Assert.Equal(0.36, PotMath.RuleOfTwoAndFour(outs: 9, cardsToCome: 2), Digits);
        Assert.Equal(0.18, PotMath.RuleOfTwoAndFour(outs: 9, cardsToCome: 1), Digits);
    }
}
