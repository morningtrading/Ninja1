# Ninja1
Ninja automation

Features & Logic
Indicators:
Uses EMAfast, EMAslow, and ATR.
Parameters:
All key parameters (takeprofit, stoploss, EMA periods, ATR settings, entry delay, minimum EMA difference) are user-configurable.
Entry Logic:
Entry is allowed only if:
Enough time has passed since last exit (Time[0] >= nextEntryTime)
Not waiting for a new cross (!waitForNewCross)
EMA cross detected (crossAbove for long, crossBelow for short)
ATR above threshold
Flat position
EMA difference above MinEMADiff
After entry, sets waitForNewCross = true to block further entries until a new cross.
Exit Logic:
Exits long on crossBelow, exits short on crossAbove.
Updates totalPnL on exit.
Sets waitForNewCross = true and updates nextEntryTime to block entries for EntryDelayMinutes.
Flag Reset:
When flat and waitForNewCross is true, resets flag only if a new cross occurs (prevents re-entry on the same crossing).
PnL Display:
Shows current trade PnL and total PnL on the chart using Draw.TextFixed.