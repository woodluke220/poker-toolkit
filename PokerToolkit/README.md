# Poker Toolkit

A study tool for No-Limit Texas Hold'em, written in C#. It runs entirely in the browser (Blazor WebAssembly), so it works on a laptop or a phone with nothing to install and no server.

## What it does

**Equity calculator.** Give each player (2 to 6) either two specific cards or a range, add a flop, turn or river if you want, and get each player's win %, tie % and overall equity. Small problems are solved exactly by dealing every possible board. Large ones are simulated, with the margin of error shown.

**Range editor.** The standard 13×13 grid. Click or drag to select hands, type notation such as `TT+, AQs+, KQs, AKo`, or use a slider to take the strongest X% of hands. Shows the combo count and share of all hands. Ranges can be named and saved, and saved ranges appear in the equity calculator.

**Pot odds and EV.** Required equity and EV for a call (from a known equity or by counting outs), the implied odds needed when a call is short, break-even bluff frequency, minimum defense frequency, and the EV of a bet given how often it gets folds.

## How it works

The solution has three projects:

| Project | What it is |
| --- | --- |
| `src/PokerToolkit.Engine` | Class library with all the poker logic. No UI and no dependencies. |
| `src/PokerToolkit.Web` | Blazor WebAssembly front end that calls the engine. |
| `tests/PokerToolkit.Engine.Tests` | xUnit tests for the engine. |

A few design points:

- **Hands are bitmasks.** Each card is one bit in a 64-bit integer, 16 bits per suit. A seven-card hand is seven cards OR-ed together.
- **The evaluator doesn't sort or loop over cards.** Questions like "which ranks appear in all four suits?" are a handful of AND/OR operations on the four suit masks. The result is a single `int` where a bigger number is always the better hand, so comparing hands is comparing integers.
- **Two equity methods.** Exact enumeration walks every remaining board (and every hand in a range). Monte Carlo deals random hands and boards. The calculator estimates the size of the exact job first and only simulates when it is too big.
- **Simulation runs in batches.** WebAssembly in the browser is single-threaded, so the page runs a couple of thousand deals, redraws, and repeats. The numbers settle on screen and the run can be stopped.
- **Range-vs-range sampling is unbiased.** When two sampled hands share a card the whole deal is redrawn, which keeps every valid deal equally likely.

### Testing

The tests include one that deals all 2,598,960 five-card hands and checks the count in every category (40 straight flushes, 624 four-of-a-kinds, and so on) against the known totals. Equity results are checked against exact enumeration.

## Running it

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
# run the tests
dotnet test

# start the app, then open the address it prints
dotnet run --project src/PokerToolkit.Web
```

In VS Code, install the C# Dev Kit extension and open this folder.

## Publishing it

`.github/workflows/deploy.yml` tests and deploys the app to GitHub Pages on every push to `main`. After pushing the repository, turn it on once under **Settings → Pages → Source: GitHub Actions**. The app is then served at `https://<your-username>.github.io/<repository-name>/`.

## Roadmap

- Hand-history analyzer: import hand histories and report VPIP, PFR, win rate and common leaks
- Weighted ranges (play a hand some fraction of the time)
- Hand-category breakdown per player (how often each makes a flush, set, and so on)
- Ahead-of-time compilation for faster simulation in the browser

## A note on use

This is for studying away from the table. Most online poker sites prohibit using calculators like this while a hand is in progress.
