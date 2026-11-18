using Microsoft.JSInterop;
using PokerToolkit.Engine;

namespace PokerToolkit.Web.Services;

/// <summary>
/// Named ranges kept in the browser's localStorage, so they survive a page reload.
/// They are stored as plain text, one "name[TAB]notation" per line.
/// </summary>
public sealed class RangeStore
{
    private const string StorageKey = "pokertoolkit.ranges";

    private readonly IJSRuntime _js;
    private SortedDictionary<string, string>? _ranges;

    public RangeStore(IJSRuntime js) => _js = js;

    public async Task<SortedDictionary<string, string>> LoadAsync()
    {
        if (_ranges is not null) return _ranges;

        string? saved = null;
        try
        {
            saved = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        }
        catch (JSException)
        {
            // Storage can be blocked (private browsing). The app still works, it just won't remember.
        }

        _ranges = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (saved is null)
        {
            // First visit: start with a couple of examples.
            _ranges["Premium"] = "QQ+, AKs, AKo";
            _ranges["Top 15%"] = HandRange.TopPercent(15).ToString();
        }
        else
        {
            foreach (string line in saved.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                int tab = line.IndexOf('\t');
                if (tab > 0) _ranges[line.Substring(0, tab)] = line.Substring(tab + 1);
            }
        }

        return _ranges;
    }

    public async Task SaveAsync(string name, string notation)
    {
        SortedDictionary<string, string> ranges = await LoadAsync();
        ranges[name.Replace('\t', ' ').Replace('\n', ' ').Trim()] = notation;
        await PersistAsync(ranges);
    }

    public async Task DeleteAsync(string name)
    {
        SortedDictionary<string, string> ranges = await LoadAsync();
        ranges.Remove(name);
        await PersistAsync(ranges);
    }

    private async Task PersistAsync(SortedDictionary<string, string> ranges)
    {
        string text = string.Join('\n', ranges.Select(pair => $"{pair.Key}\t{pair.Value}"));
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, text);
        }
        catch (JSException)
        {
        }
    }
}
