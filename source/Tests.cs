using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace Chest
{
    public static class Tests
    {
        static int failed;

        public static int Run(string unused)
        {
            failed = 0;
            string root = FindRoot();
            Check(root != null, "fixture root");
            if (root == null)
            {
                Console.WriteLine("FAILED to find fixtures");
                return 1;
            }
            Latch();
            Book();
            Icons(root);
            Fixtures(root);
            Preview(root);
            RealRewards(root);
            Pipeline(root);
            Console.WriteLine(failed == 0 ? "ALL PASSED" : failed + " FAILED");
            return failed == 0 ? 0 : 1;
        }

        static int Count(params string[] actions)
        {
            int n = 0;
            foreach (string action in actions) if (action == "opened") n++;
            return n;
        }

        static void Latch()
        {
            RewardSession fresh = new RewardSession();
            fresh.MissesBeforeClose = 3;
            int opened = 0;
            string[] marks = new string[] { "one", "two", "three" };
            for (int i = 0; i < 3; i++)
            {
                if (i > 0) { fresh.See(false, -1, null); fresh.See(false, -1, null); fresh.See(false, -1, null); }
                if (fresh.See(true, 5, marks[i]) == "opened") opened++;
            }
            Check(opened == 3, "three new caskets count as three");

            RewardSession held = new RewardSession();
            held.MissesBeforeClose = 3;
            int repeats = 0;
            for (int i = 0; i < 100; i++)
                if (held.See(true, 5, "same-frame") == "opened") repeats++;
            Check(repeats == 1 && held.See(true, 5, "jitter-" + repeats) == "none", "the same open window logged 100 times is one casket");

            RewardSession again = new RewardSession();
            again.MissesBeforeClose = 3;
            string firstOpen = again.See(true, 5, "alpha");
            again.See(false, -1, null); again.See(false, -1, null); again.See(false, -1, null);
            string secondOpen = again.See(true, 5, "beta");
            Check(firstOpen == "opened" && secondOpen == "opened", "a window that closes and reopens is a second casket");

            RewardSession rolled = new RewardSession();
            string open = rolled.See(true, 5, "loot");
            string swap = rolled.See(true, 4, "loot-changed");
            string hold = rolled.See(true, 4, "loot-changed");
            Check(open == "opened" && swap == "replaced" && hold == "none" && Count(open, swap, hold) == 1, "a 5 to 4 reroll replaces the same casket");

            RewardSession refilled = new RewardSession();
            string low = refilled.See(true, 3, "old");
            refilled.See(false, -1, null);
            refilled.See(false, -1, null);
            refilled.See(false, -1, null);
            string up = refilled.See(true, 4, "new");
            Check(low == "opened" && up == "opened" && Count(low, up) == 2, "n going from 3 to 4 is a new casket");
            Rectangle game = new Rectangle(100, 80, 900, 600);
            Rectangle placed = FloatingPanelLayout.Place(game, new Size(200, 120), new Point(800, 40), new Rectangle(0, 0, 2000, 1200));
            Check(placed.X == 800 && placed.Y == 120 && placed.Right <= game.Right && placed.Bottom <= game.Bottom, "panel stays inside the client");
            Check(FloatingPanelLayout.WantTopMost(true, false, true, false), "topmost while the game is in front");
            Check(!FloatingPanelLayout.WantTopMost(true, false, false, false), "not topmost over another app");
            Check(FloatingPanelLayout.Shown(true, false, false, true), "the panel stays while it is being used");
            Check(!FloatingPanelLayout.Shown(true, true, true, true), "a minimised client hides the panel");
            Check(StackText.Format(1089) == "1089" && StackText.Format(10000) == "10K" && StackText.Format(100000) == "100K" && StackText.Format(1200000) == "1.2M", "stack numbers use K and M");
            int columns = ChestFace.Columns(440, 32, 12);
            Check(columns >= 8 && columns * (32 + 12) <= 440, "every column fits the width (" + columns + ")");
            Check(ChestFace.ValueText(6929679) == "CURRENT REWARD VALUE: 6,929,679", "value line uses the summed casket total");
            byte[] note = new byte[40 * 40 * 4];
            for (int i = 0; i < 40 * 40; i++) { note[i * 4] = 140; note[i * 4 + 1] = 180; note[i * 4 + 2] = 210; note[i * 4 + 3] = 255; }
            for (int y = 12; y < 24; y++) for (int x = 12; x < 24; x++) { int o = (y * 40 + x) * 4; note[o] = 40; note[o + 1] = 40; note[o + 2] = 180; note[o + 3] = 255; }
            Check(Noted.LooksNoted(note, 40, 40), "a certificate reads as noted");
            byte[] bare = Noted.WithoutPaper(note, 40, 40);
            Check(bare[(16 * 40 + 16) * 4 + 3] == 255 && bare[0] == 140 && bare[3] == 0, "the paper is dropped and the item stays");
            byte[] solid = new byte[32 * 32 * 4];
            for (int i = 0; i < solid.Length; i += 4) { solid[i] = 20; solid[i + 1] = 40; solid[i + 2] = 180; solid[i + 3] = 255; }
            Check(!Noted.LooksNoted(solid, 32, 32), "a plain item is not noted");
            byte[] crop = new byte[40 * 40 * 4];
            for (int i = 0; i < 40 * 40; i++) { crop[i * 4] = 40; crop[i * 4 + 1] = 48; crop[i * 4 + 2] = 56; crop[i * 4 + 3] = 255; }
            for (int y = 14; y < 28; y++) for (int x = 14; x < 28; x++) { int o = (y * 40 + x) * 4; crop[o] = 20; crop[o + 1] = 20; crop[o + 2] = 180; }
            for (int y = 2; y < 9; y++) for (int x = 2; x < 8; x++) { int o = (y * 40 + x) * 4; crop[o] = 30; crop[o + 1] = 220; crop[o + 2] = 255; }
            byte[] cleaned = Noted.CleanForDisplay(crop, 40, 40);
            Check(cleaned[(4 * 40 + 4) * 4 + 3] == 0, "a corner stack digit is removed from a crop");
            Check(cleaned[(20 * 40 + 20) * 4 + 3] == 255 && cleaned[(20 * 40 + 20) * 4 + 2] == 180, "cleaning a crop keeps the item pixels");
        }

        static void Book()
        {
            string dir = Path.Combine(Path.GetTempPath(), "chest-test-" + Guid.NewGuid().ToString("N"));
            LootBook book = new LootBook(dir);
            book.Commit(new LootLine[] {
                new LootLine { Id = "coins", Name = "Coins", Quantity = 100 },
                new LootLine { Id = "unknown-aa", Name = "Unknown", Quantity = 1, Unknown = true }
            }, 5000);
            book.Commit(new LootLine[] { new LootLine { Id = "coins", Name = "Coins", Quantity = 20 } }, 100);
            LootBook again = new LootBook(dir);
            again.Load();
            Check(again.Caskets == 2, "caskets persisted");
            Check(again.Value == 5100, "value persisted");
            Check(again.Lines.Count == 2 && again.Lines[0].Quantity == 120, "coins stacked");
            again.Reset();
            LootBook empty = new LootBook(dir);
            empty.Load();
            Check(empty.Caskets == 0 && empty.Lines.Count == 0, "reset clears");
            LootBook roll = new LootBook(dir);
            roll.Commit(new LootLine[] { new LootLine { Id = "coins", Name = "Coins", Quantity = 100 } }, 5000);
            roll.ReplaceLast(new LootLine[] { new LootLine { Id = "coins", Name = "Coins", Quantity = 5 } }, 50);
            Check(roll.Caskets == 1 && roll.Lines.Count == 1 && roll.Lines[0].Quantity == 5 && roll.Value == 50, "a reroll replaces the logged casket");
            Check(roll.Opens.Count == 1 && roll.Opens[0] == 50, "a reroll keeps one history value");
            Check(roll.UndoLast() && roll.Caskets == 0 && roll.Lines.Count == 0 && roll.Value == 0 && roll.Opens.Count == 0, "undo removes the last casket");
            Check(!roll.UndoLast(), "a second undo does nothing");
            try { Directory.Delete(dir, true); } catch (Exception) { }
        }

        static void Icons(string root)
        {
            List<IconRef> icons = IconLibrary.Load(Path.Combine(root, "assets", "icons"), Path.Combine(root, "assets", "icons", "catalog.txt"));
            Check(icons.Count >= 60, "icon library seeded");
            IconRef coins = null;
            foreach (IconRef icon in icons) if (icon.FileName == "coins-10000.png") coins = icon;
            Check(coins != null, "coins icon");
            if (coins == null) return;
            IconHit hit = IconLibrary.Match(icons, coins.Bgra, coins.Width, coins.Height, 0.90);
            Check(hit.Known && hit.Name == "Coins", "coins matches itself (" + hit.Score.ToString("0.00") + ")");
            Frame tokenShot = FramePng.Decode(Path.Combine(root, "fixtures", "event-token.png"));
            Check(tokenShot != null, "event token reference");
            if (tokenShot != null)
            {
                IconHit token = IconLibrary.Match(icons, tokenShot.Bgra, tokenShot.Width, tokenShot.Height, 0.90);
                Check(token.Known && token.Name == "Event Token", "event token matches (" + token.Candidate + " " + token.Score.ToString("0.00") + "/" + token.Second.ToString("0.00") + ")");
            }
            byte[] red = new byte[32 * 32 * 4];
            for (int i = 0; i < red.Length; i += 4) { red[i] = 20; red[i + 1] = 20; red[i + 2] = 220; red[i + 3] = 255; }
            IconHit unknown = IconLibrary.Match(icons, red, 32, 32, 0.94);
            Check(!unknown.Known && unknown.Name == "Unknown", "a plain red square stays unknown");
        }

        static void Fixtures(string root)
        {
            List<Glyph> stack = RewardReader.LoadGlyphs(Path.Combine(root, "assets", "digits", "stack.txt"));
            List<Glyph> value = RewardReader.LoadGlyphs(Path.Combine(root, "assets", "digits", "value.txt"));
            Check(stack.Count >= 5 && value.Count >= 10, "digit templates load");
            Check(StampValue(value, "211,817 COINS!") == 211817, "211,817 COINS reads 211817");
            Check(StampValue(value, "4,273 COINS!") == 4273, "4,273 COINS reads 4273");
            Check(StampValue(value, "367,467 COINS!") == 367467, "367,467 COINS reads 367467");
            RewardReader.LoadTitle(Path.Combine(root, "assets", "reward", "trail-title.png"));
            int[][] quantities = new int[][] {
                new int[] { 1, 1, 1, 1, 2 },
                new int[] { 1, 1, 1, 1, 2 },
                new int[] { 2, 2, 4, 4, 1 },
                new int[] { 3, 4, 2, 1, 1 },
                new int[] { 1, 1, 3, 2, 7 },
                new int[] { 1, 1, 1, 1, 1, 1 }
            };
            int[] values = new int[] { 210202, 210202, 253106, 69414, 23215, 97021 };
            RewardShot first = null;
            for (int i = 0; i < 6; i++)
            {
                Frame frame = FramePng.Decode(Path.Combine(root, "fixtures", "master-" + i + ".png"));
                Check(frame != null, "load master-" + i);
                if (frame == null) continue;
                Rectangle panel = RewardReader.Find(frame);
                Check(!panel.IsEmpty, "panel in master-" + i);
                if (panel.IsEmpty) continue;
                if (i == 0)
                {
                    Check(panel.X > 900 && panel.X < 1300 && panel.Y > 500 && panel.Y < 700, "full client panel is centre-left (" + panel.X + "," + panel.Y + ")");
                    Check(panel.Width == 396 && panel.Height == 239, "panel size");
                }
                RewardShot shot = RewardReader.Read(frame, panel, stack, value);
                string got = "";
                foreach (SeenItem item in shot.Items) got += (got.Length == 0 ? "" : ",") + item.Quantity;
                string expect = "";
                foreach (int q in quantities[i]) expect += (expect.Length == 0 ? "" : ",") + q;
                Check(got == expect, "master-" + i + " quantities " + got + " wanted " + expect);
                Check(shot.Value == values[i], "master-" + i + " value " + shot.Value + " wanted " + values[i]);
                if (i == 0) Check(RewardReader.Read(frame, panel, stack, value).Value == 210202, "master-0 keeps the comma and COINS");
                Check(RewardReader.TitleMatches(frame, panel), "master-" + i + " title is TRAIL COMPLETE");
                if (i == 1) first = shot;
            }
            Frame titled = FramePng.Decode(Path.Combine(root, "fixtures", "master-1.png"));
            Frame other = FramePng.Decode(Path.Combine(root, "fixtures", "master-4.png"));
            Frame later = FramePng.Decode(Path.Combine(root, "fixtures", "master-5.png"));
            if (titled != null && other != null && later != null)
            {
                Rectangle a = RewardReader.Find(titled);
                Rectangle b = RewardReader.Find(other);
                Rectangle c = RewardReader.Find(later);
                Check(!b.IsEmpty && !c.IsEmpty, "later fixtures still have a reward panel");
                int[] charges = new int[6];
                for (int n = 0; n < 6; n++)
                {
                    Frame shot = FramePng.Decode(Path.Combine(root, "fixtures", "master-" + n + ".png"));
                    charges[n] = RewardReader.RerollCount(shot, RewardReader.Find(shot));
                }
                Check(charges[0] == 5 && charges[1] == 5 && charges[2] == 5 && charges[3] == 5, "reroll charges read 5 (" + charges[0] + "," + charges[1] + "," + charges[2] + "," + charges[3] + ")");
                Check(charges[4] == 4 && charges[5] == 3, "reroll charges read 4 then 3 (" + charges[4] + "," + charges[5] + ")");
                Frame stripped = titled.Crop(new Rectangle(0, 0, titled.Width, titled.Height));
                for (int y = a.Y + 4; y < a.Y + 28; y++)
                    for (int x = a.X + 10; x < a.X + 170; x++)
                    {
                        int i = (y * stripped.Width + x) * 4;
                        if (i >= 0 && i + 2 < stripped.Bgra.Length) { stripped.Bgra[i] = 0; stripped.Bgra[i + 1] = 0; stripped.Bgra[i + 2] = 40; }
                    }
                Check(!RewardReader.TitleMatches(stripped, a), "a red tab without TRAIL COMPLETE is ignored");
            }
            Frame plain = new Frame(new byte[800 * 400 * 4], 800, 400);
            Check(RewardReader.Find(plain).IsEmpty, "a blank frame is not a reward window");
            if (first != null)
            {
                string dir = Path.Combine(Path.GetTempPath(), "chest-unknown-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(dir);
                Dictionary<string, bool> hashes = new Dictionary<string, bool>();
                foreach (SeenItem item in first.Items)
                {
                    string hash = IconLibrary.HashCrop(item.Bgra, item.Width, item.Height);
                    hashes[hash] = true;
                    FramePng.Save(new Frame(item.Bgra, item.Width, item.Height), Path.Combine(dir, hash + ".png"));
                }
                int saved = Directory.GetFiles(dir, "*.png").Length;
                Check(saved == hashes.Count && saved > 0, "unknown crops save (" + saved + " files, " + hashes.Count + " distinct)");
                try { Directory.Delete(dir, true); } catch (Exception) { }
            }
        }

        static void Preview(string root)
        {
            string[] names = new string[] {
                "coins-10000.png", "ancient-bones.png", "box-of-clue-scrolls.png", "saradomin-page-1.png",
                "guthix-page-1.png", "zamorak-page-1.png", "air-rune.png", "death-rune.png",
                "purple-firelighter.png", "holy-biscuits.png", "uncut-dragonstone.png", "prayer-potion-4.png",
                "super-restore-4.png", "blue-dragon-mask.png", "red-firelighter.png", "green-firelighter.png"
            };
            long[] qty = new long[] { 34, 32, 346, 16, 11, 710, 9, 14, 8, 1089, 23, 2, 3, 2, 5, 1 };
            List<SlotView> slots = new List<SlotView>();
            string dir = Path.Combine(root, "assets", "icons");
            for (int i = 0; i < names.Length; i++)
            {
                string path = Path.Combine(dir, names[i]);
                if (!File.Exists(path)) continue;
                Frame frame = FramePng.Decode(path);
                if (frame == null) continue;
                Bitmap image = new Bitmap(frame.Width, frame.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                System.Drawing.Imaging.BitmapData data = image.LockBits(new Rectangle(0, 0, frame.Width, frame.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try { System.Runtime.InteropServices.Marshal.Copy(frame.Bgra, 0, data.Scan0, Math.Min(frame.Bgra.Length, data.Stride * frame.Height)); }
                finally { image.UnlockBits(data); }
                SlotView slot = new SlotView();
                slot.Name = Path.GetFileNameWithoutExtension(names[i]);
                slot.Quantity = qty[i];
                slot.Image = image;
                slots.Add(slot);
            }
            Check(slots.Count >= 8, "preview icons");
            ChestFace.LoadCrest(Path.Combine(root, "assets", "branding", "kavros-crest.png"));
            RenderOverlay(slots, "grid", 6929679, 74, Path.Combine(Path.GetTempPath(), "chest-preview", "clue-rewards.png"));
            RenderOverlay(slots, "grid", 6929679, 74, "/opt/cursor/artifacts/clue-rewards.png");
            foreach (SlotView slot in slots) if (slot.Image != null) slot.Image.Dispose();
            Check(true, "grid preview drawn");
        }

        static void Pipeline(string root)
        {
            RewardReader.LoadTitle(Path.Combine(root, "assets", "reward", "trail-title.png"));
            List<Glyph> stack = RewardReader.LoadGlyphs(Path.Combine(root, "assets", "digits", "stack.txt"));
            List<Glyph> value = RewardReader.LoadGlyphs(Path.Combine(root, "assets", "digits", "value.txt"));
            string dir = Path.Combine(Path.GetTempPath(), "chest-pipe-" + Guid.NewGuid().ToString("N"));
            LootBook book = new LootBook(dir);
            RewardSession session = new RewardSession();
            session.MissesBeforeClose = 3;
            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
            long first = 0;
            for (int i = 0; i < 6; i++)
            {
                if (i == 1) first = clock.ElapsedMilliseconds;
                Frame frame = FramePng.Decode(Path.Combine(root, "fixtures", "master-" + i + ".png"));
                Check(frame != null, "pipeline load master-" + i);
                if (frame == null) continue;
                Rectangle panel = RewardReader.Find(frame);
                Check(!panel.IsEmpty && RewardReader.TitleMatches(frame, panel), "pipeline sees TRAIL COMPLETE in master-" + i);
                if (panel.IsEmpty) continue;
                RewardShot shot = RewardReader.Read(frame, panel, stack, value);
                Check(shot.Items.Count > 0, "pipeline reads items in master-" + i + " (" + shot.Items.Count + ")");
                if (i > 0)
                {
                    session.See(false, -1, null);
                    session.See(false, -1, null);
                    session.See(false, -1, null);
                }
                string action = session.See(true, RewardReader.RerollCount(frame, panel), shot.Fingerprint);
                Check(action == "opened", "pipeline logs master-" + i + " as a new casket (" + action + ")");
                if (action == "opened")
                    book.Commit(new LootLine[] { new LootLine { Id = "pipe-" + i, Name = "Casket", Quantity = 1 } }, shot.Value);
            }
            clock.Stop();
            Check(book.Caskets == 6, "pipeline logged six caskets (" + book.Caskets + ")");
            Check(first > 0 && first < 800, "full client master-0 is read in " + first + " ms");
            Check(clock.ElapsedMilliseconds < 2500, "all six reward shots are read in " + clock.ElapsedMilliseconds + " ms");
            try { Directory.Delete(dir, true); } catch (Exception) { }
        }

        static void RealRewards(string root)
        {
            List<IconRef> icons = IconLibrary.Load(Path.Combine(root, "assets", "icons"), Path.Combine(root, "assets", "icons", "catalog.txt"));
            List<Glyph> stack = RewardReader.LoadGlyphs(Path.Combine(root, "assets", "digits", "stack.txt"));
            List<Glyph> value = RewardReader.LoadGlyphs(Path.Combine(root, "assets", "digits", "value.txt"));
            RewardReader.LoadTitle(Path.Combine(root, "assets", "reward", "trail-title.png"));
            List<SlotView> all = new List<SlotView>();
            List<SlotView>[] steps = new List<SlotView>[] { new List<SlotView>(), new List<SlotView>(), new List<SlotView>() };
            long[] stepValue = new long[3];
            int step = 0;
            int named = 0, unknown = 0;
            System.Text.StringBuilder misses = new System.Text.StringBuilder();
            for (int i = 0; i < 6; i++)
            {
                Frame frame = FramePng.Decode(Path.Combine(root, "fixtures", "master-" + i + ".png"));
                if (frame == null) continue;
                Rectangle panel = RewardReader.Find(frame);
                if (panel.IsEmpty || !RewardReader.TitleMatches(frame, panel)) continue;
                RewardShot shot = RewardReader.Read(frame, panel, stack, value);
                if (step < 3)
                {
                    stepValue[step] = shot.Value;
                    foreach (SeenItem item in shot.Items)
                    {
                        int ignoreA, ignoreB;
                        Absorb(steps[step], Shown(icons, item, out ignoreA, out ignoreB));
                    }
                    step++;
                }
                foreach (SeenItem item in shot.Items)
                {
                    int gotName = 0, gotUnknown = 0;
                    SlotView shown = Shown(icons, item, out gotName, out gotUnknown);
                    named += gotName;
                    unknown += gotUnknown;
                    if (gotUnknown > 0)
                    {
                        IconHit missed = IconLibrary.Match(icons, item.Bgra, item.Width, item.Height, 0.90);
                        misses.Append(" m").Append(i).Append(":").Append(missed.Candidate).Append("@").Append(missed.Score.ToString("0.00")).Append("/").Append(missed.Second.ToString("0.00"));
                    }
                    Absorb(all, shown);
                }
            }
            Check(named >= 28 && unknown <= 3, "master rewards use wiki names (" + named + " named, " + unknown + " unknown" + misses + ")");
            Check(all.Count >= 5, "real reward crops matched into the grid (" + all.Count + ")");
            long session = 0;
            int[] values = new int[] { 210202, 210202, 253106, 69414, 23215, 97021 };
            List<long> opens = new List<long>();
            for (int i = 0; i < values.Length; i++) { session += values[i]; opens.Add(values[i]); }
            ChestFace.LoadCrest(Path.Combine(root, "assets", "branding", "kavros-crest.png"));
            RenderOverlay(all, "grid", session, 6, "/opt/cursor/artifacts/overlay-grid.png", opens, 560);
            RenderOverlay(all, "list", session, 6, "/opt/cursor/artifacts/overlay-list.png", opens, 860);
            RenderOverlay(all, "grid", session, 6, Path.Combine(Path.GetTempPath(), "chest-preview", "real-grid.png"), opens, 560);
            RenderChrome(root, session, opens);
            if (steps[0].Count > 0 && steps[1].Count > 0 && steps[2].Count > 0)
            {
                using (Bitmap strip = new Bitmap(640 * 3, 300))
                using (Graphics g = Graphics.FromImage(strip))
                {
                    long running = 0;
                    List<SlotView> carried = new List<SlotView>();
                    for (int i = 0; i < 3; i++)
                    {
                        running += stepValue[i];
                        foreach (SlotView slot in steps[i]) Absorb(carried, slot);
                        using (Bitmap one = new Bitmap(640, 300))
                        using (Graphics face = Graphics.FromImage(one))
                        {
                            ChestFace.DrawOverlay(face, new Rectangle(0, 0, 640, 300), "Kavros", carried, 0, running, i + 1, null, 0, 0, "grid", false, null, "Opening " + (i + 1), true);
                            g.DrawImage(one, i * 640, 0);
                        }
                    }
                    WritePng(strip, Path.Combine(Path.GetTempPath(), "chest-preview", "count-sequence.png"));
                    WritePng(strip, "/opt/cursor/artifacts/clue-rewards-count.png");
                }
                Check(true, "opened-clues sequence drawn");
            }
            else Check(false, "opened-clues sequence drawn");
        }

        static SlotView Shown(List<IconRef> icons, SeenItem item, out int named, out int unknown)
        {
            named = 0;
            unknown = 0;
            byte[] pixels = item.Bgra;
            IconHit hit = IconLibrary.Match(icons, pixels, item.Width, item.Height, 0.90);
            SlotView slot = new SlotView();
            slot.Quantity = item.Quantity;
            byte[] display = pixels;
            int dw = item.Width, dh = item.Height;
            if (hit.Known)
            {
                named = 1;
                slot.Name = hit.Name;
                string group = string.IsNullOrEmpty(hit.Group) ? hit.Name : hit.Group;
                foreach (IconRef icon in icons)
                {
                    string iconGroup = string.IsNullOrEmpty(icon.Group) ? icon.Name : icon.Group;
                    if (!icon.Show || iconGroup != group) continue;
                    display = icon.Bgra;
                    dw = icon.Width;
                    dh = icon.Height;
                    break;
                }
            }
            else
            {
                unknown = 1;
                slot.Name = "Unknown";
                slot.Unknown = true;
                display = Noted.CleanForDisplay(pixels, item.Width, item.Height);
            }
            slot.Image = BitmapOf(display, dw, dh);
            slot.Look = IconLibrary.Signature(Noted.CleanForDisplay(pixels, item.Width, item.Height), item.Width, item.Height);
            return slot;
        }

        static void Absorb(List<SlotView> slots, SlotView slot)
        {
            if (slot == null || slot.Image == null) return;
            foreach (SlotView have in slots)
            {
                bool sameName = slot.Name != "Unknown" && have.Name == slot.Name;
                if (sameName || IconLibrary.SameLook(have.Look, slot.Look))
                {
                    have.Quantity += slot.Quantity;
                    return;
                }
            }
            slots.Add(slot);
        }

        static Bitmap BitmapOf(byte[] bgra, int width, int height)
        {
            Bitmap image = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            System.Drawing.Imaging.BitmapData data = image.LockBits(new Rectangle(0, 0, width, height), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                if (stride == width * 4) System.Runtime.InteropServices.Marshal.Copy(bgra, 0, data.Scan0, Math.Min(bgra.Length, stride * height));
                else
                    for (int y = 0; y < height; y++)
                        System.Runtime.InteropServices.Marshal.Copy(bgra, y * width * 4, IntPtr.Add(data.Scan0, y * stride), width * 4);
            }
            finally { image.UnlockBits(data); }
            return image;
        }

        static void RenderOverlay(List<SlotView> slots, string mode, long value, int caskets, string path, List<long> opens, int height)
        {
            try
            {
                using (Bitmap bitmap = new Bitmap(420, height))
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    string detail = "Opening " + caskets;
                    ChestFace.DrawOverlay(g, new Rectangle(0, 0, bitmap.Width, bitmap.Height), "Kavros", slots, 0, value, caskets, null, 0, 0, mode, false, opens, detail, true);
                    WritePng(bitmap, path);
                }
            }
            catch (Exception) { }
        }

        static void RenderOverlay(List<SlotView> slots, string mode, long value, int caskets, string path)
        {
            RenderOverlay(slots, mode, value, caskets, path, null, 560);
        }

        static void RenderChrome(string root, long value, List<long> opens)
        {
            try
            {
                using (Bitmap settings = new Bitmap(512, 680))
                using (Graphics g = Graphics.FromImage(settings))
                {
                    ChestFace.DrawSettings(g, new Rectangle(0, 0, settings.Width, settings.Height), "Kavros", true, 6, value, opens);
                    WritePng(settings, "/opt/cursor/artifacts/settings.png");
                    WritePng(settings, Path.Combine(Path.GetTempPath(), "chest-preview", "settings.png"));
                }
                using (Bitmap splash = new Bitmap(640, 420))
                using (Graphics g = Graphics.FromImage(splash))
                {
                    ChestFace.DrawSplash(g, new Rectangle(0, 0, splash.Width, splash.Height));
                    WritePng(splash, "/opt/cursor/artifacts/splash.png");
                    WritePng(splash, Path.Combine(root, "assets", "branding", "chest-splash.png"));
                }
            }
            catch (Exception) { }
        }

        static void WritePng(Bitmap bitmap, string path)
        {
            try
            {
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
            catch (Exception) { }
        }

        static int StampValue(List<Glyph> glyphs, string text)
        {
            Frame frame = new Frame(new byte[640 * 280 * 4], 640, 280);
            for (int i = 0; i < frame.Bgra.Length; i += 4)
            {
                frame.Bgra[i] = 16;
                frame.Bgra[i + 1] = 24;
                frame.Bgra[i + 2] = 32;
                frame.Bgra[i + 3] = 255;
            }
            int x = 40;
            int y = 116;
            for (int c = 0; c < text.Length; c++)
            {
                char ch = text[c];
                if (ch == ',') { x += 4; continue; }
                if (ch == ' ') { x += 8; continue; }
                if (ch < '0' || ch > '9') break;
                Glyph glyph = null;
                foreach (Glyph candidate in glyphs) if (candidate.Value == ch) glyph = candidate;
                if (glyph == null) continue;
                for (int yy = 0; yy < glyph.Height; yy++)
                    for (int xx = 0; xx < glyph.Width; xx++)
                    {
                        if (!glyph.On[yy * glyph.Width + xx]) continue;
                        int o = ((y + yy) * frame.Width + x + xx) * 4;
                        frame.Bgra[o] = 236;
                        frame.Bgra[o + 1] = 236;
                        frame.Bgra[o + 2] = 246;
                    }
                x += glyph.Width + 1;
            }
            RewardShot shot = RewardReader.Read(frame, new Rectangle(0, 0, 396, 239), new List<Glyph>(), glyphs);
            return shot.Value;
        }

        static string FindRoot()
        {
            string[] starts = new string[] { AppDomain.CurrentDomain.BaseDirectory, Directory.GetCurrentDirectory() };
            foreach (string start in starts)
            {
                if (string.IsNullOrEmpty(start)) continue;
                DirectoryInfo dir = new DirectoryInfo(start);
                for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
                    if (File.Exists(Path.Combine(dir.FullName, "fixtures", "master-1.png"))) return dir.FullName;
            }
            return null;
        }

        static void Check(bool ok, string name)
        {
            if (ok) Console.WriteLine("ok  " + name);
            else { Console.WriteLine("FAIL " + name); failed++; }
        }
    }
}
