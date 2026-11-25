## 0.2.0

- The loading screen uses the five casket icons.
- A casket is counted as soon as TRAIL COMPLETE appears. A reroll replaces that casket instead of adding another. The next window, after this one closes, is a new casket.
- Only a window whose title reads TRAIL COMPLETE is counted.
- Undo removes the last casket. Click it twice.
- The panel stays on the game window, above that window only, and can be collapsed.
- Unknown folder opens the folder of icons Chest could not name.
- The reward grid is a clean navy panel titled Clue Rewards. Stack numbers stay with their icons when you scroll.
- More reward icons, taken from collection log shots.

## 0.1.0

First release of Chest, a Kavros casket reward logger.

- Watches the game window you pick and reads the reward panel from the screen.
- One combined chest: item icons, quantities, caskets opened, and reward value.
- A reroll updates the open reward. The casket is logged when the window closes.
- Unknown icons are kept and saved so they can be named later.
- Totals are stored in `%LocalAppData%\Chest`.
- Setup.exe updates itself from GitHub Releases.
