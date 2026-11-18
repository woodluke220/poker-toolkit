namespace PokerToolkit.Engine;

/// <summary>
/// All-in equity of each starting hand against one random hand, laid out like the
/// range grid (row and column 0 = Ace; suited above the diagonal, offsuit below).
///
/// These numbers were produced by simulation (1.5 million deals per hand), so each
/// is accurate to roughly +/- 0.1 percentage points. They are used to rank hands for
/// the "top X%" range slider. Equity against a random hand is a simple, well-defined
/// yardstick, but it is not the same thing as a good opening range: it rates
/// high-card strength above the playability of suited connectors.
/// </summary>
public static class PreflopEquity
{
    private static readonly double[] VsRandom =
    {
        //  A       K       Q       J       T       9       8       7       6       5       4       3       2
        0.8518, 0.6707, 0.6622, 0.6543, 0.6460, 0.6276, 0.6198, 0.6096, 0.5990, 0.5993, 0.5905, 0.5827, 0.5746,   // A
        0.6530, 0.8238, 0.6342, 0.6252, 0.6182, 0.6001, 0.5835, 0.5749, 0.5664, 0.5579, 0.5489, 0.5411, 0.5318,   // K
        0.6445, 0.6146, 0.7993, 0.6025, 0.5948, 0.5768, 0.5605, 0.5436, 0.5361, 0.5271, 0.5182, 0.5102, 0.5016,   // Q
        0.6359, 0.6056, 0.5815, 0.7748, 0.5758, 0.5565, 0.5404, 0.5231, 0.5058, 0.4999, 0.4907, 0.4824, 0.4739,   // J
        0.6272, 0.5978, 0.5726, 0.5523, 0.7497, 0.5401, 0.5235, 0.5069, 0.4894, 0.4729, 0.4656, 0.4569, 0.4486,   // T
        0.6081, 0.5786, 0.5532, 0.5317, 0.5153, 0.7206, 0.5086, 0.4912, 0.4746, 0.4579, 0.4390, 0.4328, 0.4241,   // 9
        0.5982, 0.5604, 0.5363, 0.5142, 0.4971, 0.4809, 0.6920, 0.4795, 0.4620, 0.4456, 0.4274, 0.4085, 0.4020,   // 8
        0.5884, 0.5514, 0.5179, 0.4973, 0.4794, 0.4626, 0.4501, 0.6623, 0.4541, 0.4364, 0.4189, 0.4000, 0.3814,   // 7
        0.5777, 0.5427, 0.5105, 0.4795, 0.4611, 0.4448, 0.4320, 0.4238, 0.6326, 0.4317, 0.4136, 0.3954, 0.3768,   // 6
        0.5769, 0.5328, 0.5009, 0.4722, 0.4423, 0.4268, 0.4143, 0.4050, 0.3997, 0.6035, 0.4145, 0.3978, 0.3784,   // 5
        0.5678, 0.5235, 0.4912, 0.4616, 0.4352, 0.4063, 0.3943, 0.3855, 0.3800, 0.3810, 0.5701, 0.3855, 0.3681,   // 4
        0.5585, 0.5141, 0.4829, 0.4530, 0.4258, 0.3996, 0.3752, 0.3660, 0.3613, 0.3630, 0.3515, 0.5372, 0.3599,   // 3
        0.5494, 0.5050, 0.4731, 0.4437, 0.4169, 0.3908, 0.3688, 0.3466, 0.3409, 0.3431, 0.3321, 0.3228, 0.5030,   // 2
    };

    /// <summary>Equity (0 to 1) of the hand type in a grid cell against a random hand.</summary>
    public static double Get(int row, int col) => VsRandom[row * HandRange.GridSize + col];

    /// <summary>Grid cells (row * 13 + col) ordered from the strongest hand type to the weakest.</summary>
    public static IReadOnlyList<int> CellsStrongestFirst { get; } =
        Enumerable.Range(0, VsRandom.Length).OrderByDescending(cell => VsRandom[cell]).ToArray();
}
