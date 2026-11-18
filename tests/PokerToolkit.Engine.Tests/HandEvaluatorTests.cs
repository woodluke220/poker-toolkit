using PokerToolkit.Engine;

namespace PokerToolkit.Engine.Tests;

public class HandEvaluatorTests
{
    private static int Score(string cards) => HandEvaluator.Evaluate(Card.ParseMany(cards));

    [Theory]
    [InlineData("AsKsQsJsTs", HandCategory.StraightFlush)]
    [InlineData("AsAhAdAc2s", HandCategory.FourOfAKind)]
    [InlineData("KsKhKd2c2s", HandCategory.FullHouse)]
    [InlineData("As9s7s4s2s", HandCategory.Flush)]
    [InlineData("5h4s3d2cAs", HandCategory.Straight)]
    [InlineData("7s7h7d2cKs", HandCategory.ThreeOfAKind)]
    [InlineData("7s7hKdKc2s", HandCategory.TwoPair)]
    [InlineData("7s7hKdQc2s", HandCategory.Pair)]
    [InlineData("As9h7d4c2s", HandCategory.HighCard)]
    // Seven cards: the best five must be found.
    [InlineData("AsKsQsJsTs2h2d", HandCategory.StraightFlush)]
    [InlineData("KsKhKdQsQhQd2c", HandCategory.FullHouse)]
    [InlineData("2s2h3d3c4s4hAd", HandCategory.TwoPair)]
    [InlineData("2s3s4s5s9sAhAd", HandCategory.Flush)]
    public void Finds_the_right_category(string cards, HandCategory expected)
    {
        Assert.Equal(expected, HandEvaluator.GetCategory(Score(cards)));
    }

    [Theory]
    // better hand, worse hand
    [InlineData("6h5s4d3c2s", "5h4s3d2cAs")]   // six-high straight beats the wheel
    [InlineData("3s3h4d5c7s", "2s2hAdKcQs")]   // a bigger pair beats better kickers
    [InlineData("AsAhKd7c2s", "AsAhQd7c2s")]   // same pair, kicker decides
    [InlineData("AsAh3d3c2s", "KsKhQdQcAs")]   // top pair decides two pair
    [InlineData("AsKs9s4s2s", "AsQsJsTs2s")]   // flushes compare card by card
    [InlineData("3s3h3d2c2s", "2s2h2dAcAs")]   // trips decide a full house
    [InlineData("2s2h2d2cAs", "AsAhAdKcKs")]   // quads beat a full house
    public void Ranks_hands_in_the_right_order(string better, string worse)
    {
        Assert.True(Score(better) > Score(worse));
    }

    [Fact]
    public void Same_hand_in_different_suits_ties()
    {
        Assert.Equal(Score("AsKhQd7c2s"), Score("AhKdQc7s2h"));
    }

    [Fact]
    public void Seven_cards_use_only_the_best_five()
    {
        // Both players play the board's ace-high straight; hole cards don't matter.
        Assert.Equal(Score("AsKhQdJcTs2h3d"), Score("AsKhQdJcTs4h4d"));
    }

    [Fact]
    public void Counts_every_five_card_hand_correctly()
    {
        // The number of five-card hands in each category is a known result,
        // so dealing all 2,598,960 of them checks the evaluator end to end.
        var counts = new int[9];
        ulong[] masks = Enumerable.Range(0, 52).Select(i => new Card(i).Mask).ToArray();

        for (int a = 0; a < 52; a++)
            for (int b = a + 1; b < 52; b++)
                for (int c = b + 1; c < 52; c++)
                    for (int d = c + 1; d < 52; d++)
                        for (int e = d + 1; e < 52; e++)
                        {
                            int score = HandEvaluator.Evaluate(masks[a] | masks[b] | masks[c] | masks[d] | masks[e]);
                            counts[(int)HandEvaluator.GetCategory(score)]++;
                        }

        Assert.Equal(1_302_540, counts[(int)HandCategory.HighCard]);
        Assert.Equal(1_098_240, counts[(int)HandCategory.Pair]);
        Assert.Equal(123_552, counts[(int)HandCategory.TwoPair]);
        Assert.Equal(54_912, counts[(int)HandCategory.ThreeOfAKind]);
        Assert.Equal(10_200, counts[(int)HandCategory.Straight]);
        Assert.Equal(5_108, counts[(int)HandCategory.Flush]);
        Assert.Equal(3_744, counts[(int)HandCategory.FullHouse]);
        Assert.Equal(624, counts[(int)HandCategory.FourOfAKind]);
        Assert.Equal(40, counts[(int)HandCategory.StraightFlush]);
    }

    [Theory]
    [InlineData("AsKsQsJsTs", "Royal Flush")]
    [InlineData("KsKhKd2c2s", "Full House, Kings full of Twos")]
    [InlineData("6s6h2d3c9s", "Pair of Sixes")]
    [InlineData("5h4s3d2cAs", "Straight, Five-high")]
    public void Describes_hands_in_words(string cards, string expected)
    {
        Assert.Equal(expected, HandEvaluator.Describe(Score(cards)));
    }
}
