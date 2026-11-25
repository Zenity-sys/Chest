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
            Console.WriteLine(failed == 0 ? "ALL PASSED" : failed + " FAILED");
            return failed == 0 ? 0 : 1;
        }

        static void Latch()
        {
            RewardSession session = new RewardSession();
            session.MissesBeforeClose = 3;
            Check(session.See(true, "a") == "opened", "the first window counts immediately");
            Check(session.See(true, "a") == "none", "the same window is not a second casket");
            Check(session.See(false, null) == "none", "one miss stays open");
            Check(session.See(true, "b") == "replaced", "a reroll replaces the open casket");
            Check(session.See(true, "b") == "none", "the new roll stays put");
            Check(session.See(false, null) == "none" && session.See(false, null) == "none" && session.See(false, null) == "closed", "three misses close it");
            Check(session.See(false, null) == "none", "a closed window does not count");
            Check(session.See(true, "b") == "opened", "the next window is a new casket");
            Rectangle game = new Rectangle(100, 80, 900, 600);
            Rectangle placed = FloatingPanelLayout.Place(game, new Size(200, 120), new Point(800, 40), new Rectangle(0, 0, 2000, 1200));
            Check(placed.X == 800 && placed.Y == 120 && placed.Right <= game.Right && placed.Bottom <= game.Bottom, "panel stays inside the client");
            Check(FloatingPanelLayout.WantTopMost(true, false, true, false), "topmost while the game is in front");
            Check(!FloatingPanelLayout.WantTopMost(true, false, false, false), "not topmost over another app");
            Check(FloatingPanelLayout.Shown(true, false, false, true), "the panel stays while it is being used");
            Check(!FloatingPanelLayout.Shown(true, true, true, true), "a minimised client hides the panel");
            Check(StackText.Format(1089) == "1089" && StackText.Format(10000) == "10k" && StackText.Format(15500) == "15.5k" && StackText.Format(2500000) == "2.5M", "stack numbers use k and M");
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
                string keyA = RewardReader.RerollKey(titled, a);
                Check(keyA == RewardReader.RerollKey(FramePng.Decode(Path.Combine(root, "fixtures", "master-0.png")), RewardReader.Find(FramePng.Decode(Path.Combine(root, "fixtures", "master-0.png")))), "the same reroll state shares a key");
                Check(RewardReader.RerollKey(other, b) != RewardReader.RerollKey(later, c), "a reroll button change is a new key");
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

        static string FindRoot()
        {
            string[] starts = new string[] { AppDomain.CurrentDomain.BaseDirectory, Directory.GetCurrentDirectory() };
            foreach (string start in starts)
            {
                DirectoryInfo dir = new DirectoryInfo(start);
                for (int i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
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
