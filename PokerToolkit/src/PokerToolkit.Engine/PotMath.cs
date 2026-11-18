namespace PokerToolkit.Engine;

/// <summary>
/// The arithmetic behind calling, betting and bluffing decisions.
/// Percent-style values are fractions (0.25 means 25%). Money can be in any unit
/// as long as every input uses the same one (chips, dollars, big blinds).
/// </summary>
public static class PotMath
{
    /// <summary>
    /// The equity you need for a call to break even.
    /// You risk the call to win the pot plus the bet, so: call / (pot + bet + call).
    /// </summary>
    /// <param name="pot">The pot before your opponent bet.</param>
    /// <param name="bet">The bet you are facing.</param>
    public static double RequiredEquity(double pot, double bet)
    {
        double finalPot = pot + bet + bet;
        return finalPot <= 0 ? 0 : bet / finalPot;
    }

    /// <summary>Pot odds as "X to 1": what you stand to win for each unit you call.</summary>
    public static double PotOddsRatio(double pot, double bet) => bet <= 0 ? 0 : (pot + bet) / bet;

    /// <summary>
    /// Expected value of calling: win the pot and the bet with probability
    /// <paramref name="equity"/>, lose the call otherwise.
    /// </summary>
    public static double CallEv(double equity, double pot, double bet)
        => equity * (pot + bet) - (1 - equity) * bet;

    /// <summary>
    /// How much more you must win on later streets, on average when you hit, for a call
    /// that lacks the direct odds to break even. Zero or less means the call is already fine.
    /// </summary>
    public static double ImpliedOddsNeeded(double equity, double pot, double bet)
    {
        if (equity <= 0) return double.PositiveInfinity;
        return (1 - equity) * bet / equity - (pot + bet);
    }

    /// <summary>
    /// How often a pure bluff must work to break even: you risk the bet to win the pot,
    /// so bet / (pot + bet).
    /// </summary>
    public static double BluffBreakEven(double pot, double bet)
        => pot + bet <= 0 ? 0 : bet / (pot + bet);

    /// <summary>
    /// Minimum defense frequency: how often the player facing the bet must continue
    /// so that a bluff with no equity cannot profit. Equal to 1 - BluffBreakEven.
    /// </summary>
    public static double MinimumDefenseFrequency(double pot, double bet)
        => pot + bet <= 0 ? 0 : pot / (pot + bet);

    /// <summary>
    /// Expected value of betting when the opponent folds <paramref name="foldFrequency"/>
    /// of the time and you have <paramref name="equityWhenCalled"/> the rest of the time.
    /// </summary>
    public static double BetEv(double pot, double bet, double foldFrequency, double equityWhenCalled)
    {
        double whenCalled = equityWhenCalled * (pot + bet) - (1 - equityWhenCalled) * bet;
        return foldFrequency * pot + (1 - foldFrequency) * whenCalled;
    }

    /// <summary>
    /// Exact chance of hitting at least one of your outs.
    /// </summary>
    /// <param name="outs">Cards that improve you to the winning hand.</param>
    /// <param name="cardsToCome">1 (turn only, or river only) or 2 (turn and river).</param>
    /// <param name="unseenCards">Cards you can't see: 47 on the flop, 46 on the turn.</param>
    public static double OutsToEquity(int outs, int cardsToCome, int unseenCards)
    {
        if (outs <= 0 || unseenCards <= 0) return 0;
        if (outs >= unseenCards) return 1;

        double missAll = 1;
        for (int i = 0; i < cardsToCome; i++)
            missAll *= (double)(unseenCards - outs - i) / (unseenCards - i);
        return 1 - Math.Max(missAll, 0);
    }

    /// <summary>
    /// The table shortcut: outs x 2 for one card to come, outs x 4 for two.
    /// Returned as a fraction so it can be compared with <see cref="OutsToEquity"/>.
    /// </summary>
    public static double RuleOfTwoAndFour(int outs, int cardsToCome)
        => Math.Min(outs * (cardsToCome >= 2 ? 4 : 2) / 100.0, 1);
}
