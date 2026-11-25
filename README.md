# Chest

Chest logs clue casket rewards for Kavros. It watches the game window, reads the reward panel from the screen, and keeps one combined chest of everything you have opened.

It is a Windows .NET Framework 4.8 program. Download **Setup.exe** from [Releases](https://github.com/Zenity-sys/Chest/releases) and run it. Chest updates itself from that page.

## Using it

1. Start Kavros, then start Chest.
2. Pick your game window. If you renamed it in Companion, that name shows up here.
3. Open caskets. Chest counts a casket as soon as the TRAIL COMPLETE window is on screen. It replaces that casket only when the reroll button's number drops by one. If the number stays the same or goes back up, the next window is a new casket.
4. The chest is one grid for every tier. Icons sit on the panel at their own size. Yellow numbers are how many you have logged. Rest the pointer on an icon to see its name and count.
5. **CURRENT REWARD VALUE** adds up the value shown on each casket. A reroll replaces that casket's value. **OPENED CLUES** is how many caskets are logged.
6. **UNDO** removes the last casket. **UNKNOWN** opens the folder of icons Chest could not name. **Reset**, in the row above the chest, clears everything. Click Undo or Reset twice. Totals are saved in `%LocalAppData%\Chest`.

The panel sits on the game window you picked. It stays above that window, and it gets out of the way when another program is in front. Drag the title bar to move it. **−** collapses it to the title bar.

Items Chest does not recognise yet show as **Unknown**. The cropped icon is saved in `%LocalAppData%\Chest\unknown`. Send those pictures back and they can be named in a later update.

**Save capture** writes a picture of the game window to `%LocalAppData%\Chest\captures` if a reward is not being picked up.

There are no popup windows. Status text along the top is the only message.

Chest only looks at the screen. It does not read game memory and it does not click or type for you.

## Layout

`assets/chest-layout.txt` sets the grid columns, icon size, gaps, and window size. Change it and restart. That file is there so a mockup can be matched without digging through code.

## This build

v0.4.0 is tuned on a modern interface, about a 2560×1369 client, at 96 DPI. The reward panel in those shots is 396×239 and sits just left of centre. The reader checks the gold **TRAIL COMPLETE!** title, not just the red tab, then reads the item strip, the yellow stack numbers, and the reward value line. A noted reward is stored as the plain item.

Stack digits confirmed from those shots are 1, 2, 3, 4 and 7. A stack that uses another digit is counted as 1 until a shot of that digit is added. Reward values use 0–7 and 9. An 8 in the value line is not templated yet.

The icon folder is seeded from common reward sprites and from collection log screenshots. Icons that could not be named confidently are stored with placeholder names. Kavros still has its own items, so anything new lands in Unknown until a crop is named in `assets/icons` (`file|Name|group` in `catalog.txt`).

## Releases

Push a tag that matches the first three parts of the version in `source/app.manifest`. The current tag is `v0.4.1`. The window shows v0.4.1. The Windows workflow builds with `source/build.ps1`, packs the exe and the `assets` folder, and publishes:

- `Chest-win-Setup.exe`
- `Chest-win-Portable.zip`
- `Chest-<version>-full.nupkg`
- `releases.win.json`
