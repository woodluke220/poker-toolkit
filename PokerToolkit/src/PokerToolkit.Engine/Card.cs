namespace PokerToolkit.Engine;

/// <summary>
/// One playing card, stored as a single number from 0 to 51.
/// Rank runs 0..12 (Two..Ace) and suit runs 0..3 (clubs, diamonds, hearts, spades).
/// </summary>
public readonly struct Card : IEquatable<Card>
{
    public const string RankChars = "23456789TJQKA";
    public const string SuitChars = "cdhs";

    /// <summary>0..51, equal to Rank * 4 + Suit.</summary>
    public int Index { get; }

    public Card(int index)
    {
        if (index < 0 || index > 51)
            throw new ArgumentOutOfRangeException(nameof(index), "Card index must be 0..51.");
        Index = index;
    }

    public static Card From(int rank, int suit)
    {
        if (rank < 0 || rank > 12) throw new ArgumentOutOfRangeException(nameof(rank));
        if (suit < 0 || suit > 3) throw new ArgumentOutOfRangeException(nameof(suit));
        return new Card(rank * 4 + suit);
    }

    /// <summary>0 = Two ... 12 = Ace.</summary>
    public int Rank => Index >> 2;

    /// <summary>0 = clubs, 1 = diamonds, 2 = hearts, 3 = spades.</summary>
    public int Suit => Index & 3;

    /// <summary>
    /// The card as a single bit in a 64-bit number. Each suit owns 16 bits and the
    /// rank picks the bit inside that block, so a whole hand is just several cards
    /// OR-ed together. The evaluator and the equity calculator both work on these masks.
    /// </summary>
    public ulong Mask => 1UL << (Suit * 16 + Rank);

    public char RankChar => RankChars[Rank];
    public char SuitChar => SuitChars[Suit];

    public static bool TryParse(string? text, out Card card)
    {
        card = default;
        if (text is null) return false;
        text = text.Trim();
        if (text.Length != 2) return false;

        int rank = RankChars.IndexOf(char.ToUpperInvariant(text[0]));
        int suit = SuitChars.IndexOf(char.ToLowerInvariant(text[1]));
        if (rank < 0 || suit < 0) return false;

        card = From(rank, suit);
        return true;
    }

    public static Card Parse(string text)
    {
        if (!TryParse(text, out Card card))
            throw new FormatException($"'{text}' is not a card. Use a rank and a suit, like As or Td.");
        return card;
    }

    /// <summary>Parses several cards written together or separated, e.g. "AhKd" or "Ah Kd, 7c".</summary>
    public static List<Card> ParseMany(string? text)
    {
        var cards = new List<Card>();
        if (string.IsNullOrWhiteSpace(text)) return cards;

        string compact = text.Replace(" ", "").Replace(",", "");
        if (compact.Length % 2 != 0)
            throw new FormatException($"'{text}' is not a list of cards. Each card needs a rank and a suit.");

        for (int i = 0; i < compact.Length; i += 2)
            cards.Add(Parse(compact.Substring(i, 2)));
        return cards;
    }

    /// <summary>Combines a set of cards into one mask.</summary>
    public static ulong MaskOf(IEnumerable<Card> cards)
    {
        ulong mask = 0;
        foreach (Card c in cards) mask |= c.Mask;
        return mask;
    }

    public override string ToString() => $"{RankChar}{SuitChar}";

    public bool Equals(Card other) => Index == other.Index;
    public override bool Equals(object? obj) => obj is Card other && Equals(other);
    public override int GetHashCode() => Index;
    public static bool operator ==(Card a, Card b) => a.Index == b.Index;
    public static bool operator !=(Card a, Card b) => a.Index != b.Index;
}
