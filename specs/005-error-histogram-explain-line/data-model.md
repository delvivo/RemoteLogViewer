# Data Model

In-memory only; nothing persisted or exported.

## ErrorHistogram (pure)

- `Add(LogLine line, DateTime defaultDate)`: updates `_lastMinute` from the line's own timestamp (markers ignored, no timestamp → inherit), and if the line counts as an error entry (`IsEntry && Level == Error` or `IsAlert`) and `_lastMinute` is set, increments that minute.
- `Reset()`: clears counts and `_lastMinute`.
- `Window(int maxBars) → IReadOnlyList<(DateTime Minute, int Count)>`: the last `maxBars` consecutive minutes ending at the latest minute seen (any line, not only errors), zero-filled; empty when no timestamp seen or `maxBars ≤ 0`.
- Invariant: sum of counts over all minutes = number of counted lines after the first timestamp.

## Minute bucket

`(DateTime Minute, int Count)`; `Minute` has seconds and ticks zero.

## Explain request

`ExplainRequested(LogView view, LogLine line)`; `AiInput.Excerpt(IReadOnlyList<string> visibleTexts, int index, int radius) → string[]`: lines `[max(0,index-radius), min(n-1,index+radius)]`, the target prefixed with `>>> `.

## AiPrompt

New entry in `AiPrompt.All()`: name "Spiega questa riga" (en "Explain this line"), text asking to explain the `>>>`-marked line using the surrounding lines.
