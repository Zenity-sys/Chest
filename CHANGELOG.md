## Unreleased

- Opening the next casket without closing the reward window counts as a new casket once the new items settle. A one-step reroll still replaces the last casket.
- Saved icons from an older version are matched by name. A missing picture stays visible.
- Reset on the reward panel asks for a second click.
- Each client uses its own resolution and DPI. After the reward window is found, later reads stay on that region.
- Chest paints its own panel out of the capture. Companion can open it with a client handle, a process id, or bind.txt.

## 0.5.0

- The window matches the Kavros Chest design: navy and gold, the Kavros crest, a splash screen, and a settings window.
- The chest lists every item from the current session. Grid and List switch in the overlay and in Settings.
- Reward icons are the clean wiki sprites. A screen crop is kept only when nothing on the reward lists matches.
- Grid icons sit on the dark panel with a yellow count. List rows show the icon, the name, and the quantity. Hovering an icon shows its name and count.
- A screen crop, used only when no sprite exists, drops the slot behind it and the yellow stack digit in the corner.
- History lists each logged casket. Undo, Unknown, and Debug stay on the panel.
- Event Token uses a clean sprite. The slot and the yellow count are not part of the picture.

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
