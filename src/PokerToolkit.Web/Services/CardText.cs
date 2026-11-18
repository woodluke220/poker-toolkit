using PokerToolkit.Engine;

namespace PokerToolkit.Web.Services;

/// <summary>Small display helpers shared by the pages.</summary>
public static class CardText
{
    public static string SuitSymbol(Card card) => card.Suit switch
    {
        0 => "♣",
        1 => "♦",
        2 => "♥",
        _ => "♠"
    };

    /// <summary>CSS class that colours a card by suit (a four-colour deck).</summary>
    public static string SuitClass(Card card) => card.Suit switch
    {
        0 => "clubs",
        1 => "diamonds",
        2 => "hearts",
        _ => "spades"
    };

    /// <summary>Formats a fraction such as 0.4567 as "45.7%".</summary>
    public static string Percent(double fraction, int decimals = 1)
        => (fraction * 100).ToString("F" + decimals) + "%";
}
