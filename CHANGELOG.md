## 0.4.2

- An open reward window is logged once. It is counted again only after it closes, or replaced when the reroll number drops by one.
- The reward value keeps commas and ignores the COINS text, including amounts with an 8.
- Item pictures keep their pixels. The count sits above the icon, drawn once.
- Debug saves a zip of the current capture and the recent detection log.

## 0.4.1

- A casket is logged as soon as TRAIL COMPLETE is on screen. Other red panels no longer block it.
- The same item is one stack. Each icon shows a single count.
- Crops lose the slot tile behind them. Clean sprites stay crisp.
- Checking for updates downloads the new version and restarts.
- Saved rewards sit outside the install folder so Setup can replace the program.

## 0.4.0

- A new casket is counted unless REROLL REWARD (n) drops by exactly one on the same window. n staying the same or going up is not a reroll.
- The grid shows clean item sprites. A screen crop is only a last resort, and its flat background is removed first.
- CLUE REWARDS sits in a gold-edged tab. Stack numbers use a dark outline and K/M for large amounts. Hovering an item shows its name and count on the panel.
- CURRENT REWARD VALUE and the OPENED CLUES, UNDO, and UNKNOWN buttons stay under the grid.

## 0.3.0

- The reward panel matches the in-game chest: gold CLUE REWARDS title, icons straight on the dark panel at their own size, and no boxes.
- CURRENT REWARD VALUE is the sum of the value read from each casket. A reroll replaces that casket's value.
- OPENED CLUES, UNDO, and UNKNOWN sit on the three buttons under the value line.
- Noted rewards are counted and shown as the unnoted item.
- Reward icons use the clean item sprites, including Ancient bones and Box of clue scrolls.

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
