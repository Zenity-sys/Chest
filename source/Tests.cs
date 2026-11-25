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
            string a = fresh.See(true, 5, "one");
            string b = fresh.See(true, 5, "two");
            string c = fresh.See(true, 5, "three");
            Check(a == "opened" && b == "opened" && c == "opened" && Count(a, b, c) == 3, "three new caskets count as three");

            RewardSession rolled = new RewardSession();
            string open = rolled.See(true, 5, "loot");
            string swap = rolled.See(true, 4, "loot-2");
            string hold = rolled.See(true, 4, "loot-2");
            Check(open == "opened" && swap == "replaced" && hold == "none" && Count(open, swap, hold) == 1, "a 5 to 4 reroll replaces the same casket");

            RewardSession same = new RewardSession();
            string first = same.See(true, 5, "alpha");
            string second = same.See(true, 5, "beta");
            Check(first == "opened" && second == "opened" && Count(first, second) == 2, "n staying at 5 across two caskets counts two");

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
            Check(roll.UndoLast() && roll.Caskets == 0 && roll.Lines.Count == 0 && roll.Value == 0, "undo removes the last casket");
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
            byte[] red = new byte[32 * 32 * 4];
            for (int i = 0; i < red.Length; i += 4) { red[i] = 20; red[i + 1] = 20; red[i + 2] = 220; red[i + 3] = 255; }
            IconHit unknown = IconLibrary.Match(icons, red, 32, 32, 0.94);
            Check(!unknown.Known && unknown.Name == "Unknown", "a plain red square stays unknown");
        }

        static void Fixtures(string root)
        {
            List<Glyph> stack = RewardReader.LoadGlyphs(Path.Combine(root, "assets", "digits", "stack.txt"));
            List<Glyph> value = RewardReader.LoadGlyphs(Path.Combine(root, "assets", "digits", "value.txt"));
            Check(stack.Count >= 5 && value.Count >= 9, "digit templates load");
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
            using (Bitmap bitmap = new Bitmap(640, 300))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                ChestFace.Draw(g, new Rectangle(0, 0, bitmap.Width, bitmap.Height), slots, 0, 6929679, 74, -1, -1, false, 32, 12, "Coins\n34", 80, 70);
                string folder = Path.Combine(Path.GetTempPath(), "chest-preview");
                Directory.CreateDirectory(folder);
                bitmap.Save(Path.Combine(folder, "clue-rewards.png"), System.Drawing.Imaging.ImageFormat.Png);
                string artifacts = "/opt/cursor/artifacts";
                try
                {
                    if (Directory.Exists("/opt/cursor"))
                    {
                        Directory.CreateDirectory(artifacts);
                        bitmap.Save(Path.Combine(artifacts, "clue-rewards.png"), System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                catch (Exception) { }
            }
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
                    foreach (SeenItem item in shot.Items) Absorb(steps[step], Shown(icons, item, out named, out unknown));
                    step++;
                }
                foreach (SeenItem item in shot.Items)
                {
                    int ignoreA = 0, ignoreB = 0;
                    Absorb(all, Shown(icons, item, out ignoreA, out ignoreB));
                }
            }
            Check(all.Count >= 5, "real reward crops matched into the grid (" + all.Count + ")");
            SaveFace(all, 210202, 6, Path.Combine(Path.GetTempPath(), "chest-preview", "real-grid.png"), "real grid");
            SaveFace(all, 210202, 6, "/opt/cursor/artifacts/clue-rewards-real.png", "real grid artifact");
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
                            ChestFace.Draw(face, new Rectangle(0, 0, 640, 300), carried, 0, running, i + 1, -1, -1, false, 32, 12, null, 0, 0);
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
            if (Noted.LooksNoted(pixels, item.Width, item.Height)) pixels = Noted.WithoutPaper(pixels, item.Width, item.Height);
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

        static void SaveFace(List<SlotView> slots, long value, int caskets, string path, string label)
        {
            try
            {
                using (Bitmap bitmap = new Bitmap(640, 360))
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    ChestFace.Draw(g, new Rectangle(0, 0, bitmap.Width, bitmap.Height), slots, 0, value, caskets, -1, -1, false, 32, 12, slots.Count > 0 ? slots[0].Name + "\n" + StackText.Format(slots[0].Quantity) : null, 70, 60);
                    WritePng(bitmap, path);
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
