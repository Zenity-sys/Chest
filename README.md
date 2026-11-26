# Chest

Chest logs clue casket rewards for Kavros. It watches the game window, reads the reward panel from the screen, and keeps one combined chest of everything you have opened.

It is a Windows .NET Framework 4.8 program. Download **Setup.exe** from [Releases](https://github.com/Zenity-sys/Chest/releases) and run it. Chest updates itself from that page.

## Using it

1. Start Kavros, then start Chest.
2. Pick your game window. If you renamed it in Companion, that name shows up here.
3. Open caskets. Chest counts a casket as soon as the TRAIL COMPLETE window is on screen. It logs that window once. It replaces that casket only when the reroll number drops by one. The same window is not counted again while it stays open. The next window, after this one closes, is a new casket.
4. The chest is everything opened this session. **Grid** shows the item icons with a yellow count. **List** shows the icon, the name, and the quantity. Switch them on the panel or in Settings. Rest the pointer on an icon to see its name and count.
5. **Session value** adds up the value shown on each casket. A reroll replaces that casket's value. **Opened clues** is how many caskets are logged. Chest does not price each item.
6. **History** lists each casket. **Undo last** removes the last one. **Unknown** opens the folder of icons Chest could not name. **Debug** saves a zip of the current capture and the recent detection log. **Reset session**, in Settings, clears everything. Click Undo or Reset twice. Totals are saved in `%LocalAppData%\ChestData`.

The panel sits on the game window you picked. It stays above that window, and it gets out of the way when another program is in front. Drag the title bar to move it. **−** collapses it to the title bar. **Settings** picks the client, switches Grid and List, opens the splash screen, and checks for updates.

Items Chest does not recognise yet show as **Unknown** straight away. The cropped icon is saved in `%LocalAppData%\ChestData\unknown`. Chest then looks the picture up in the background. A confident match updates the name and the icon, and is kept in `%LocalAppData%\ChestData\wiki-cache` for next time. If there is no confident match, it stays Unknown.

There are no popup windows. The status line on the panel is the only message.

Chest only looks at the screen. It does not read game memory and it does not click or type for you.

## Layout

`assets/chest-layout.txt` sets the grid columns, icon size, gaps, and window size. Change it and restart. That file is there so a mockup can be matched without digging through code.

## This build

v0.6.4 is tuned on a modern interface, about a 2560×1369 client, at 96 DPI. The reward panel in those shots is 396×239 and sits just left of centre. The reader checks the gold **TRAIL COMPLETE!** title, not just the red tab, then reads the item strip, the yellow stack numbers, and the reward value line. A noted reward is stored as the plain item.

Stack digits are 0–9, including a yellow count drawn on a bright icon. Reward values use digits 0–9.

Named rewards use the clean item sprites. A screen crop is shown only when no sprite matches, and that crop drops the slot behind it and the yellow stack digit in the corner. The bundled set includes fallen stars, prismatic stars, XP lamps, prismatic lamps, crossbows, salvage, and common seeds and supplies. Anything still unknown is checked against a wider wiki index without slowing the live read. Kavros items that are not in either list stay Unknown until a sprite is added to `assets/icons` (`file|Name|group|role` in `catalog.txt`).

## Releases

Push a tag that matches the first three parts of the version in `source/app.manifest`. The current tag to publish is `v0.6.4`. The window shows v0.6.4. The Windows workflow builds with `source/build.ps1`, packs the exe and the `assets` folder, and publishes:

- `Chest-win-Setup.exe`
- `Chest-win-Portable.zip`
- `Chest-<version>-full.nupkg`
- `releases.win.json`
