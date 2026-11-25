# Chest

Chest logs clue casket rewards for Kavros. It watches the game window, reads the reward panel from the screen, and keeps one combined chest of everything you have opened.

It is a Windows .NET Framework 4.8 program. Download **Setup.exe** from [Releases](https://github.com/Zenity-sys/Chest/releases) and run it. Chest updates itself from that page.

## Using it

1. Start Kavros, then start Chest.
2. Pick your game window. If you renamed it in Companion, that name shows up here.
3. Open caskets. When the reward window is on screen, Chest reads the items and the reward value. A reroll replaces that reading. The casket is added when the window closes.
4. The grid is one chest for every tier. Numbers on the icons are how many you have logged. **Caskets opened** and **Reward value** sit under the grid.
5. **Reset** clears the chest. Click it twice so a misclick does not wipe a session.
6. Totals are saved in `%LocalAppData%\Chest`.

Items Chest does not recognise yet show as **Unknown**. The cropped icon is saved in `%LocalAppData%\Chest\unknown`. Send those pictures back and they can be named in a later update.

**Save capture** writes a picture of the game window to `%LocalAppData%\Chest\captures` if a reward is not being picked up.

There are no popup windows. Status text along the top is the only message.

Chest only looks at the screen. It does not read game memory and it does not click or type for you.

## Layout

`assets/chest-layout.txt` sets the grid columns, icon size, gaps, and window size. Change it and restart. That file is there so a mockup can be matched without digging through code.

## This build

v0.1.0 is tuned on a modern interface, about a 2560×1369 client, at 96 DPI. The reward panel in those shots is 396×239 and sits just left of centre. The reader looks for the red **TRAIL COMPLETE!** tab, the item strip, the yellow stack numbers, and the **CURRENT REWARD VALUE** line.

Stack digits confirmed from those shots are 1, 2, 3, 4 and 7. A stack that uses another digit is counted as 1 until a shot of that digit is added. Reward values use 0–7 and 9. An 8 in the value line is not templated yet.

The icon folder is a starting set of common reward sprites. Kavros has its own items, so most master-casket icons will land in Unknown until real icons are named and dropped into `assets/icons` (file name, plus a line in `catalog.txt`: `file|Name|group`).

## Releases

Push a tag that matches the first three parts of the version in `source/app.manifest`. The current tag is `v0.1.0`. The window shows v0.1. The Windows workflow builds with `source/build.ps1`, packs the exe and the `assets` folder, and publishes:

- `Chest-win-Setup.exe`
- `Chest-win-Portable.zip`
- `Chest-<version>-full.nupkg`
- `releases.win.json`
