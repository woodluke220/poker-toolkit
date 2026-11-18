namespace PokerToolkit.Engine;

/// <summary>Two specific hole cards, e.g. Ah Kd.</summary>
public readonly struct Combo
{
    public Card First { get; }
    public Card Second { get; }
    public ulong Mask { get; }

    public Combo(Card first, Card second)
    {
        if (first == second) throw new ArgumentException("A hand needs two different cards.");
        First = first;
        Second = second;
        Mask = first.Mask | second.Mask;
    }

    public override string ToString() => $"{First}{Second}";
}

/// <summary>
/// A preflop range: which of the 169 starting-hand types a player could hold.
///
/// It is stored as the familiar 13x13 grid. Row and column 0 are the Ace and
/// row and column 12 are the Two. Pairs sit on the diagonal, suited hands are
/// above it (row &lt; col) and offsuit hands are below it (row &gt; col).
///
/// Text notation understood by <see cref="Parse"/>:
///   AA        one pair              TT+       tens and every higher pair
///   AKs, AKo  suited / offsuit      AK        both suited and offsuit
///   A2s+      A2s up to AKs         KTo+      KTo, KJo, KQo
///   22-66     a span of pairs       A2s-A5s   a span of kickers
/// </summary>
public sealed class HandRange
{
    public const int GridSize = 13;
    public const int TotalCombos = 1326;

    private const string GridRanks = "AKQJT98765432";

    private readonly bool[] _cells = new bool[GridSize * GridSize];

    public bool this[int row, int col]
    {
        get => _cells[row * GridSize + col];
        set => _cells[row * GridSize + col] = value;
    }

    // ----- Grid helpers ---------------------------------------------------

    /// <summary>The label shown in a grid cell: "AA", "AKs" or "AKo".</summary>
    public static string CellName(int row, int col)
    {
        if (row == col) return $"{GridRanks[row]}{GridRanks[col]}";
        return row < col
            ? $"{GridRanks[row]}{GridRanks[col]}s"
            : $"{GridRanks[col]}{GridRanks[row]}o";
    }

    /// <summary>How many two-card combinations a cell stands for: 6 pairs, 4 suited, 12 offsuit.</summary>
    public static int CellCombos(int row, int col) => row == col ? 6 : row < col ? 4 : 12;

    public int ComboCount
    {
        get
        {
            int total = 0;
            for (int row = 0; row < GridSize; row++)
                for (int col = 0; col < GridSize; col++)
                    if (this[row, col]) total += CellCombos(row, col);
            return total;
        }
    }

    /// <summary>Share of all 1,326 starting hands, as a number from 0 to 100.</summary>
    public double Percent => 100.0 * ComboCount / TotalCombos;

    public bool IsEmpty => Array.IndexOf(_cells, true) < 0;

    public void Clear() => Array.Fill(_cells, false);

    public void SelectAll() => Array.Fill(_cells, true);

    public void CopyFrom(HandRange other) => Array.Copy(other._cells, _cells, _cells.Length);

    public HandRange Clone()
    {
        var copy = new HandRange();
        copy.CopyFrom(this);
        return copy;
    }

    /// <summary>
    /// Every specific two-card hand in the range, skipping any that use a card in
    /// <paramref name="deadMask"/> (cards already on the board or in another hand).
    /// </summary>
    public List<Combo> GetCombos(ulong deadMask = 0)
    {
        var combos = new List<Combo>();

        void Add(Card first, Card second)
        {
            var combo = new Combo(first, second);
            if ((combo.Mask & deadMask) == 0) combos.Add(combo);
        }

        for (int row = 0; row < GridSize; row++)
        {
            for (int col = 0; col < GridSize; col++)
            {
                if (!this[row, col]) continue;

                if (row == col)
                {
                    int rank = 12 - row;
                    for (int a = 0; a < 4; a++)
                        for (int b = a + 1; b < 4; b++)
                            Add(Card.From(rank, a), Card.From(rank, b));
                }
                else if (row < col)
                {
                    int high = 12 - row, low = 12 - col;
                    for (int suit = 0; suit < 4; suit++)
                        Add(Card.From(high, suit), Card.From(low, suit));
                }
                else
                {
                    int high = 12 - col, low = 12 - row;
                    for (int a = 0; a < 4; a++)
                        for (int b = 0; b < 4; b++)
                            if (a != b) Add(Card.From(high, a), Card.From(low, b));
                }
            }
        }
        return combos;
    }

    /// <summary>
    /// The strongest hands making up roughly <paramref name="percent"/> of all starting hands,
    /// ranked by all-in equity against one random hand (see <see cref="PreflopEquity"/>).
    /// </summary>
    public static HandRange TopPercent(double percent)
    {
        var range = new HandRange();
        double target = Math.Clamp(percent, 0, 100) / 100.0 * TotalCombos;
        int taken = 0;

        foreach (int cell in PreflopEquity.CellsStrongestFirst)
        {
            int row = cell / GridSize, col = cell % GridSize;
            int combos = CellCombos(row, col);

            // Take a hand type only if doing so lands closer to the target than stopping.
            if (taken + combos / 2.0 > target) break;
            range[row, col] = true;
            taken += combos;
        }
        return range;
    }

    // ----- Text notation --------------------------------------------------

    /// <summary>A starting-hand type while parsing. Kind is 'p' pair, 's' suited, 'o' offsuit, 'b' both.</summary>
    private readonly record struct HandClass(int High, int Low, char Kind);

    private static readonly char[] Separators = { ',', ';', ' ', '\t', '\n', '\r' };

    public static HandRange Parse(string? text)
    {
        var range = new HandRange();
        if (string.IsNullOrWhiteSpace(text)) return range;

        foreach (string token in text.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
            range.AddToken(token);
        return range;
    }

    public static bool TryParse(string? text, out HandRange range, out string? error)
    {
        try
        {
            range = Parse(text);
            error = null;
            return true;
        }
        catch (FormatException ex)
        {
            range = new HandRange();
            error = ex.Message;
            return false;
        }
    }

    private void AddToken(string raw)
    {
        string token = raw.Trim().ToUpperInvariant();

        int dash = token.IndexOf('-');
        if (dash >= 0)
        {
            HandClass from = ParseClass(token.Substring(0, dash), raw);
            HandClass to = ParseClass(token.Substring(dash + 1), raw);
            AddSpan(from, to, raw);
            return;
        }

        bool plus = token.EndsWith('+');
        if (plus) token = token.Substring(0, token.Length - 1);

        HandClass hand = ParseClass(token, raw);
        if (!plus)
        {
            AddClass(hand);
        }
        else if (hand.Kind == 'p')
        {
            for (int rank = hand.High; rank <= 12; rank++)
                AddClass(new HandClass(rank, rank, 'p'));
        }
        else
        {
            // "ATs+" keeps the Ace and walks the kicker up to one below it.
            for (int low = hand.Low; low < hand.High; low++)
                AddClass(new HandClass(hand.High, low, hand.Kind));
        }
    }

    private static HandClass ParseClass(string token, string raw)
    {
        if (token.Length != 2 && token.Length != 3) throw Unreadable(raw);

        int first = Card.RankChars.IndexOf(token[0]);
        int second = Card.RankChars.IndexOf(token[1]);
        if (first < 0 || second < 0) throw Unreadable(raw);

        int high = Math.Max(first, second), low = Math.Min(first, second);

        if (high == low)
        {
            if (token.Length == 3)
                throw new FormatException($"'{raw}': a pair can't be suited or offsuit.");
            return new HandClass(high, low, 'p');
        }

        if (token.Length == 2) return new HandClass(high, low, 'b');

        return token[2] switch
        {
            'S' => new HandClass(high, low, 's'),
            'O' => new HandClass(high, low, 'o'),
            _ => throw Unreadable(raw)
        };
    }

    private static FormatException Unreadable(string raw) =>
        new($"Couldn't read '{raw}'. Examples: AA, TT+, AKs, AKo, A2s+, 22-66, A2s-A5s.");

    private void AddSpan(HandClass from, HandClass to, string raw)
    {
        if (from.Kind == 'p' && to.Kind == 'p')
        {
            for (int rank = Math.Min(from.High, to.High); rank <= Math.Max(from.High, to.High); rank++)
                AddClass(new HandClass(rank, rank, 'p'));
            return;
        }

        // "A2s-A5" is accepted: the right side borrows the suited/offsuit marker.
        char kind = from.Kind;
        if (to.Kind != 'b' && to.Kind != kind) kind = '?';

        if (from.Kind == 'p' || to.Kind == 'p' || from.High != to.High || kind == '?')
        {
            throw new FormatException(
                $"'{raw}': both ends of a span need the same high card and the same suitedness, like A2s-A5s.");
        }

        for (int low = Math.Min(from.Low, to.Low); low <= Math.Max(from.Low, to.Low); low++)
            AddClass(new HandClass(from.High, low, kind));
    }

    private void AddClass(HandClass hand)
    {
        if (hand.Kind == 'p' || hand.Kind == 's' || hand.Kind == 'b') Set(hand.High, hand.Low, suited: true);
        if (hand.Kind == 'o' || hand.Kind == 'b') Set(hand.High, hand.Low, suited: false);
    }

    /// <summary>Ranks here are card ranks (12 = Ace), so they are flipped to reach grid positions.</summary>
    private void Set(int high, int low, bool suited)
    {
        if (high == low) this[12 - high, 12 - high] = true;
        else if (suited) this[12 - high, 12 - low] = true;
        else this[12 - low, 12 - high] = true;
    }

    private bool IsSet(int high, int low, bool suited)
    {
        if (high == low) return this[12 - high, 12 - high];
        return suited ? this[12 - high, 12 - low] : this[12 - low, 12 - high];
    }

    /// <summary>Writes the range back out in compact notation, e.g. "TT+, AJs+, KQs, AKo".</summary>
    public override string ToString()
    {
        var parts = new List<string>();
        char R(int rank) => Card.RankChars[rank];

        AppendRuns(parts, 12, rank => IsSet(rank, rank, true), rank => $"{R(rank)}{R(rank)}");

        for (int high = 12; high >= 1; high--)
        {
            int h = high;
            AppendRuns(parts, h - 1, low => IsSet(h, low, true), low => $"{R(h)}{R(low)}s");
        }

        for (int high = 12; high >= 1; high--)
        {
            int h = high;
            AppendRuns(parts, h - 1, low => IsSet(h, low, false), low => $"{R(h)}{R(low)}o");
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Walks ranks from <paramref name="top"/> down to Two and groups neighbours that are
    /// all selected. A group touching the top becomes "X+", other groups become "X-Y".
    /// </summary>
    private static void AppendRuns(List<string> parts, int top, Func<int, bool> isSet, Func<int, string> name)
    {
        int rank = top;
        while (rank >= 0)
        {
            if (!isSet(rank))
            {
                rank--;
                continue;
            }

            int start = rank;
            while (rank > 0 && isSet(rank - 1)) rank--;
            int end = rank;

            if (start == end) parts.Add(name(start));
            else if (start == top) parts.Add(name(end) + "+");
            else parts.Add($"{name(start)}-{name(end)}");

            rank--;
        }
    }
}
