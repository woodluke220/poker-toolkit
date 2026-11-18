using System.Numerics;

namespace PokerToolkit.Engine;

public enum HandCategory
{
    HighCard = 0,
    Pair = 1,
    TwoPair = 2,
    ThreeOfAKind = 3,
    Straight = 4,
    Flush = 5,
    FullHouse = 6,
    FourOfAKind = 7,
    StraightFlush = 8
}

/// <summary>
/// Scores the best five-card poker hand that can be made from 5, 6 or 7 cards.
///
/// The result is a single int where a bigger number is always the better hand:
///   bits 20+   : the hand category (pair, flush, ...)
///   bits 0..19 : up to five ranks, 4 bits each, most important first
/// So comparing two hands is just comparing two ints.
///
/// It works on bitmasks instead of sorting cards. Each suit is a 13-bit number
/// with one bit per rank, and questions like "which ranks appear in all four
/// suits?" become a few AND/OR operations.
/// </summary>
public static class HandEvaluator
{
    private const int RankBits = 0x1FFF;      // thirteen 1-bits, one per rank
    private const int WheelMask = 0x100F;     // A,5,4,3,2

    public static int Evaluate(IEnumerable<Card> cards) => Evaluate(Card.MaskOf(cards));

    public static int Evaluate(ulong cardMask)
    {
        // Split the 64-bit mask into one 13-bit rank set per suit.
        int c = (int)(cardMask & RankBits);
        int d = (int)((cardMask >> 16) & RankBits);
        int h = (int)((cardMask >> 32) & RankBits);
        int s = (int)((cardMask >> 48) & RankBits);

        int ranks = c | d | h | s;   // every rank present, ignoring suits

        // A flush exists if any one suit holds five or more cards.
        int flush = 0;
        if (BitOperations.PopCount((uint)c) >= 5) flush = c;
        else if (BitOperations.PopCount((uint)d) >= 5) flush = d;
        else if (BitOperations.PopCount((uint)h) >= 5) flush = h;
        else if (BitOperations.PopCount((uint)s) >= 5) flush = s;

        if (flush != 0)
        {
            int straightFlushHigh = StraightHigh(flush);
            if (straightFlushHigh >= 0)
                return Score(HandCategory.StraightFlush, straightFlushHigh);
        }

        // Which ranks appear exactly four, three and two times.
        int quads = c & d & h & s;
        int threeOrMore = (c & d & h) | (c & d & s) | (c & h & s) | (d & h & s);
        int twoOrMore = (c & d) | (c & h) | (c & s) | (d & h) | (d & s) | (h & s);
        int trips = threeOrMore & ~quads;
        int pairs = twoOrMore & ~threeOrMore;

        if (quads != 0)
        {
            int quadRank = HighBit(quads);
            int kicker = HighBit(ranks & ~(1 << quadRank));
            return Score(HandCategory.FourOfAKind, quadRank, kicker);
        }

        if (trips != 0)
        {
            // A full house needs trips plus a pair. With seven cards the "pair"
            // can also come from a second, lower set of trips.
            int tripRank = HighBit(trips);
            int pairCandidates = (trips & ~(1 << tripRank)) | pairs;
            if (pairCandidates != 0)
                return Score(HandCategory.FullHouse, tripRank, HighBit(pairCandidates));
        }

        if (flush != 0)
            return ScoreTopRanks(HandCategory.Flush, flush, 5);

        int straightHigh = StraightHigh(ranks);
        if (straightHigh >= 0)
            return Score(HandCategory.Straight, straightHigh);

        if (trips != 0)
        {
            int tripRank = HighBit(trips);
            return ScoreTopRanks(HandCategory.ThreeOfAKind, ranks & ~(1 << tripRank), 2, tripRank);
        }

        if (BitOperations.PopCount((uint)pairs) >= 2)
        {
            int highPair = HighBit(pairs);
            int lowPair = HighBit(pairs & ~(1 << highPair));
            int kicker = HighBit(ranks & ~(1 << highPair) & ~(1 << lowPair));
            return Score(HandCategory.TwoPair, highPair, lowPair, kicker);
        }

        if (pairs != 0)
        {
            int pairRank = HighBit(pairs);
            return ScoreTopRanks(HandCategory.Pair, ranks & ~(1 << pairRank), 3, pairRank);
        }

        return ScoreTopRanks(HandCategory.HighCard, ranks, 5);
    }

    public static HandCategory GetCategory(int score) => (HandCategory)(score >> 20);

    /// <summary>Plain-English name for a score, e.g. "Full House, Kings full of Tens".</summary>
    public static string Describe(int score)
    {
        int a = (score >> 16) & 0xF;
        int b = (score >> 12) & 0xF;

        return GetCategory(score) switch
        {
            HandCategory.StraightFlush => a == 12 ? "Royal Flush" : $"Straight Flush, {RankName(a)}-high",
            HandCategory.FourOfAKind => $"Four of a Kind, {RankPlural(a)}",
            HandCategory.FullHouse => $"Full House, {RankPlural(a)} full of {RankPlural(b)}",
            HandCategory.Flush => $"Flush, {RankName(a)}-high",
            HandCategory.Straight => $"Straight, {RankName(a)}-high",
            HandCategory.ThreeOfAKind => $"Three of a Kind, {RankPlural(a)}",
            HandCategory.TwoPair => $"Two Pair, {RankPlural(a)} and {RankPlural(b)}",
            HandCategory.Pair => $"Pair of {RankPlural(a)}",
            _ => $"{RankName(a)}-high"
        };
    }

    private static readonly string[] RankNames =
    {
        "Two", "Three", "Four", "Five", "Six", "Seven", "Eight",
        "Nine", "Ten", "Jack", "Queen", "King", "Ace"
    };

    public static string RankName(int rank) => RankNames[rank];

    public static string RankPlural(int rank) => rank == 4 ? "Sixes" : RankNames[rank] + "s";

    /// <summary>
    /// Highest rank of a five-card straight inside a 13-bit rank set, or -1 if none.
    /// The wheel (A-2-3-4-5) counts as a five-high straight.
    /// </summary>
    private static int StraightHigh(int rankSet)
    {
        for (int high = 12; high >= 4; high--)
        {
            if (((rankSet >> (high - 4)) & 0x1F) == 0x1F) return high;
        }
        return (rankSet & WheelMask) == WheelMask ? 3 : -1;
    }

    private static int HighBit(int value) => BitOperations.Log2((uint)value);

    private static int Score(HandCategory category, int a = 0, int b = 0, int c = 0)
        => ((int)category << 20) | (a << 16) | (b << 12) | (c << 8);

    /// <summary>
    /// Builds a score from the highest <paramref name="count"/> ranks in a rank set.
    /// If <paramref name="lead"/> is given (the rank of the pair or trips), it takes the
    /// most important slot and the kickers fill the slots after it.
    /// </summary>
    private static int ScoreTopRanks(HandCategory category, int rankSet, int count, int lead = -1)
    {
        int score = (int)category << 20;
        int shift = 16;

        if (lead >= 0)
        {
            score |= lead << 16;
            shift = 12;
        }

        for (int i = 0; i < count && rankSet != 0; i++)
        {
            int rank = HighBit(rankSet);
            score |= rank << shift;
            rankSet &= ~(1 << rank);
            shift -= 4;
        }
        return score;
    }
}
