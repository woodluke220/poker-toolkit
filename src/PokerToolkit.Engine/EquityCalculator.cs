namespace PokerToolkit.Engine;

/// <summary>One seat in an equity calculation: either two known cards or a range.</summary>
public sealed class PlayerSpec
{
    public Combo? Hand { get; }
    public HandRange? Range { get; }

    private PlayerSpec(Combo? hand, HandRange? range)
    {
        Hand = hand;
        Range = range;
    }

    public static PlayerSpec FromHand(Card first, Card second) => new(new Combo(first, second), null);

    public static PlayerSpec FromHand(string text)
    {
        List<Card> cards = Card.ParseMany(text);
        if (cards.Count != 2) throw new FormatException($"'{text}' is not a two-card hand.");
        return FromHand(cards[0], cards[1]);
    }

    public static PlayerSpec FromRange(HandRange range) => new(null, range);

    public static PlayerSpec FromRange(string notation) => new(null, HandRange.Parse(notation));
}

public sealed class EquityRequest
{
    public List<PlayerSpec> Players { get; } = new();

    /// <summary>Community cards already dealt: 0 to 5.</summary>
    public List<Card> Board { get; } = new();

    /// <summary>Cards known to be out of the deck, such as folded hands.</summary>
    public List<Card> Dead { get; } = new();
}

/// <summary>All values are fractions from 0 to 1.</summary>
public sealed class PlayerEquity
{
    /// <summary>How often this player wins the whole pot.</summary>
    public double Win { get; init; }

    /// <summary>How often this player splits the pot.</summary>
    public double Tie { get; init; }

    /// <summary>Average share of the pot: wins plus this player's share of the splits.</summary>
    public double Equity { get; init; }
}

public sealed class EquityResult
{
    public required IReadOnlyList<PlayerEquity> Players { get; init; }

    /// <summary>Boards counted (exact) or random deals simulated (Monte Carlo).</summary>
    public long Trials { get; init; }

    public bool IsExact { get; init; }

    /// <summary>
    /// Worst-case 95% margin of error on each percentage, as a fraction. Zero when exact.
    /// </summary>
    public double MarginOfError => IsExact || Trials == 0 ? 0 : 1.96 * Math.Sqrt(0.25 / Trials);
}

/// <summary>
/// Works out how often each player wins.
///
/// Two methods:
///   Exact       - deals every possible remaining board and counts. Always right,
///                 but preflop there are 1.7 million boards for just two hands.
///   Monte Carlo - deals random boards (and random hands from ranges) many times.
///                 Fast, and the error shrinks as the number of trials grows.
/// <see cref="Calculate"/> picks exact when the job is small enough.
/// </summary>
public static class EquityCalculator
{
    /// <summary>Largest number of evaluations <see cref="Calculate"/> will do exactly.</summary>
    public const double DefaultExactLimit = 150_000;

    public static EquityResult Calculate(
        EquityRequest request,
        int monteCarloTrials = 100_000,
        double exactLimit = DefaultExactLimit)
    {
        return EstimateExactWork(request) <= exactLimit
            ? CalculateExact(request)
            : CalculateMonteCarlo(request, monteCarloTrials);
    }

    /// <summary>
    /// Upper bound on how many deals an exact calculation would look at:
    /// (hand combinations for each player multiplied together) x (possible boards).
    /// </summary>
    public static double EstimateExactWork(EquityRequest request)
    {
        var scenario = new Scenario(request);

        double work = 1;
        foreach (Combo[] combos in scenario.Combos) work *= combos.Length;

        int cardsLeft = 52 - request.Board.Count - request.Dead.Count - 2 * scenario.PlayerCount;
        return work * Choose(cardsLeft, scenario.CardsToCome);
    }

    public static EquityResult CalculateExact(EquityRequest request)
    {
        var runner = new ExactRunner(new Scenario(request));
        return runner.Run();
    }

    public static EquityResult CalculateMonteCarlo(EquityRequest request, int trials, int? seed = null)
    {
        var simulation = new MonteCarloSimulation(request, seed);
        simulation.Run(trials);
        return simulation.GetResult();
    }

    private static double Choose(int n, int k)
    {
        if (k < 0 || k > n) return 0;
        double result = 1;
        for (int i = 1; i <= k; i++) result = result * (n - k + i) / i;
        return result;
    }

    /// <summary>Holds the working arrays for one exact calculation.</summary>
    private sealed class ExactRunner
    {
        private readonly Scenario _scenario;
        private readonly Tally _tally;
        private readonly ulong[] _holes;
        private readonly int[] _scores;
        private readonly ulong[] _deck = new ulong[52];
        private readonly int[] _pick = new int[5];

        public ExactRunner(Scenario scenario)
        {
            _scenario = scenario;
            _tally = new Tally(scenario.PlayerCount);
            _holes = new ulong[scenario.PlayerCount];
            _scores = new int[scenario.PlayerCount];
        }

        public EquityResult Run()
        {
            AssignHands(0, _scenario.DeadMask);
            if (_tally.Trials == 0)
                throw new InvalidOperationException("These hands and ranges can't all be dealt at the same time.");
            return _tally.ToResult(isExact: true);
        }

        /// <summary>Gives each player, in turn, every hand from their range that is still available.</summary>
        private void AssignHands(int player, ulong used)
        {
            if (player == _scenario.PlayerCount)
            {
                RunBoards(used);
                return;
            }

            foreach (Combo combo in _scenario.Combos[player])
            {
                if ((combo.Mask & used) != 0) continue;
                _holes[player] = combo.Mask;
                AssignHands(player + 1, used | combo.Mask);
            }
        }

        /// <summary>Deals every possible run-out from the cards nobody holds.</summary>
        private void RunBoards(ulong used)
        {
            int need = _scenario.CardsToCome;
            if (need == 0)
            {
                ScoreBoard(_scenario.BoardMask);
                return;
            }

            int deckSize = 0;
            for (int i = 0; i < 52; i++)
            {
                ulong mask = Deck.Masks[i];
                if ((used & mask) == 0) _deck[deckSize++] = mask;
            }

            // _pick holds the positions of the chosen cards. Stepping it forward like an
            // odometer visits every way of choosing `need` cards exactly once.
            for (int j = 0; j < need; j++) _pick[j] = j;

            while (true)
            {
                ulong board = _scenario.BoardMask;
                for (int j = 0; j < need; j++) board |= _deck[_pick[j]];
                ScoreBoard(board);

                int p = need - 1;
                while (p >= 0 && _pick[p] == deckSize - need + p) p--;
                if (p < 0) break;

                _pick[p]++;
                for (int j = p + 1; j < need; j++) _pick[j] = _pick[j - 1] + 1;
            }
        }

        private void ScoreBoard(ulong board)
        {
            for (int i = 0; i < _holes.Length; i++)
                _scores[i] = HandEvaluator.Evaluate(_holes[i] | board);
            _tally.Add(_scores);
        }
    }
}

/// <summary>
/// A Monte Carlo equity run that can be advanced a batch at a time. The web app
/// uses this to keep the page responsive and show the numbers settling.
/// </summary>
public sealed class MonteCarloSimulation
{
    private const int MaxDealAttempts = 100_000;

    private readonly Scenario _scenario;
    private readonly Tally _tally;
    private readonly Random _random;
    private readonly ulong[] _holes;
    private readonly int[] _scores;

    public MonteCarloSimulation(EquityRequest request, int? seed = null)
    {
        _scenario = new Scenario(request);
        _tally = new Tally(_scenario.PlayerCount);
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
        _holes = new ulong[_scenario.PlayerCount];
        _scores = new int[_scenario.PlayerCount];
    }

    public long Trials => _tally.Trials;

    public void Run(int trials)
    {
        Combo[][] combos = _scenario.Combos;
        int players = combos.Length;
        int need = _scenario.CardsToCome;

        for (int t = 0; t < trials; t++)
        {
            // Deal every player a hand from their range. If two hands share a card the
            // whole deal is thrown away and redone, which keeps every valid deal equally likely.
            ulong used;
            int attempts = 0;
            while (true)
            {
                used = _scenario.DeadMask;
                bool clash = false;

                for (int p = 0; p < players; p++)
                {
                    Combo[] options = combos[p];
                    ulong mask = options.Length == 1
                        ? options[0].Mask
                        : options[_random.Next(options.Length)].Mask;

                    if ((mask & used) != 0)
                    {
                        clash = true;
                        break;
                    }

                    used |= mask;
                    _holes[p] = mask;
                }

                if (!clash) break;
                if (++attempts > MaxDealAttempts)
                    throw new InvalidOperationException("These hands and ranges can't all be dealt at the same time.");
            }

            // Deal the rest of the board from the cards that are left.
            ulong board = _scenario.BoardMask;
            for (int k = 0; k < need; k++)
            {
                ulong mask;
                do
                {
                    mask = Deck.Masks[_random.Next(52)];
                }
                while ((mask & used) != 0);

                used |= mask;
                board |= mask;
            }

            for (int p = 0; p < players; p++)
                _scores[p] = HandEvaluator.Evaluate(_holes[p] | board);
            _tally.Add(_scores);
        }
    }

    public EquityResult GetResult() => _tally.ToResult(isExact: false);
}

internal static class Deck
{
    /// <summary>The bitmask of each of the 52 cards, indexed by <see cref="Card.Index"/>.</summary>
    public static readonly ulong[] Masks = BuildMasks();

    private static ulong[] BuildMasks()
    {
        var masks = new ulong[52];
        for (int i = 0; i < 52; i++) masks[i] = new Card(i).Mask;
        return masks;
    }
}

/// <summary>A validated request, converted into the masks and arrays the calculators use.</summary>
internal sealed class Scenario
{
    /// <summary>The hands each player could hold. A known hand is a list of one.</summary>
    public Combo[][] Combos { get; }

    public ulong BoardMask { get; }

    /// <summary>Board cards plus dead cards. Hole cards are tracked separately per deal.</summary>
    public ulong DeadMask { get; }

    public int CardsToCome { get; }

    public int PlayerCount => Combos.Length;

    public Scenario(EquityRequest request)
    {
        int players = request.Players.Count;
        if (players < 2 || players > 9)
            throw new ArgumentException("An equity calculation needs between 2 and 9 players.");
        if (request.Board.Count > 5)
            throw new ArgumentException("The board can't have more than 5 cards.");

        // Every card that is pinned down: board, dead cards and known hands.
        ulong known = 0;
        foreach (Card card in request.Board) known = Claim(known, card);
        foreach (Card card in request.Dead) known = Claim(known, card);

        foreach (PlayerSpec player in request.Players)
        {
            if (player.Hand is Combo hand)
            {
                known = Claim(known, hand.First);
                known = Claim(known, hand.Second);
            }
        }

        BoardMask = Card.MaskOf(request.Board);
        DeadMask = BoardMask | Card.MaskOf(request.Dead);
        CardsToCome = 5 - request.Board.Count;

        Combos = new Combo[players][];
        for (int i = 0; i < players; i++)
        {
            PlayerSpec player = request.Players[i];
            if (player.Hand is Combo hand)
            {
                Combos[i] = new[] { hand };
            }
            else
            {
                Combos[i] = player.Range!.GetCombos(known).ToArray();
                if (Combos[i].Length == 0)
                {
                    throw new ArgumentException(
                        $"Player {i + 1}'s range has no hands left once the known cards are removed.");
                }
            }
        }
    }

    private static ulong Claim(ulong known, Card card)
    {
        if ((known & card.Mask) != 0)
            throw new ArgumentException($"{card} is used more than once.");
        return known | card.Mask;
    }
}

/// <summary>Running totals of wins, ties and pot share for each player.</summary>
internal sealed class Tally
{
    private readonly long[] _wins;
    private readonly long[] _ties;
    private readonly double[] _potShare;

    public long Trials { get; private set; }

    public Tally(int players)
    {
        _wins = new long[players];
        _ties = new long[players];
        _potShare = new double[players];
    }

    public void Add(int[] scores)
    {
        int best = -1, winners = 0;
        for (int i = 0; i < scores.Length; i++)
        {
            if (scores[i] > best)
            {
                best = scores[i];
                winners = 1;
            }
            else if (scores[i] == best)
            {
                winners++;
            }
        }

        double share = 1.0 / winners;
        for (int i = 0; i < scores.Length; i++)
        {
            if (scores[i] != best) continue;
            if (winners == 1) _wins[i]++;
            else _ties[i]++;
            _potShare[i] += share;
        }

        Trials++;
    }

    public EquityResult ToResult(bool isExact)
    {
        var players = new PlayerEquity[_wins.Length];
        for (int i = 0; i < players.Length; i++)
        {
            players[i] = new PlayerEquity
            {
                Win = Trials == 0 ? 0 : (double)_wins[i] / Trials,
                Tie = Trials == 0 ? 0 : (double)_ties[i] / Trials,
                Equity = Trials == 0 ? 0 : _potShare[i] / Trials
            };
        }

        return new EquityResult { Players = players, Trials = Trials, IsExact = isExact };
    }
}
