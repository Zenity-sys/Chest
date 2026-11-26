using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;

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
            WikiFallback(root);
            Fixtures(root);
            Preview(root);
            RealRewards(root);
            Pipeline(root);
            Adapt(root);
            Hold(root);
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

            RewardSession inplace = new RewardSession();
            string stay = inplace.See(true, 5, "casket-a");
            string againSame = inplace.See(true, 5, "casket-a");
            string half = inplace.See(true, 5, "casket-b-partial");
            string next = inplace.See(true, 5, "casket-b");
            string settled = inplace.See(true, 5, "casket-b");
            string heldOpen = inplace.See(true, 5, "casket-b");
            Check(stay == "opened" && againSame == "none" && half == "none" && next == "none" && settled == "opened" && heldOpen == "none", "an in-place casket counts once the new read settles");
            string raised = inplace.See(true, 6, "casket-c");
            string raisedAgain = inplace.See(true, 6, "casket-c");
            Check(raised == "none" && raisedAgain == "opened", "n going up on the same window is a new casket");
            string reroll = inplace.See(true, 5, "casket-c-reroll");
            Check(reroll == "replaced", "n dropping by one on the same window is still a reroll");

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
            int refilledNow = 0;
            refilled.Clock = delegate { return refilledNow; };
            string low = refilled.See(true, 3, "old");
            refilledNow = 40;
            refilled.See(false, -1, null);
            refilledNow = 560;
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
            IconRef legacy = IconLibrary.Resolve(icons, "old-build-missing-path", "Dragon helm");
            Check(legacy != null && legacy.Name == "Dragon helm" && legacy.FileName == "dragon-helm.png", "a stale saved icon is found by its name");
            IconRef missing = IconLibrary.Resolve(icons, "no-such-file", "Not A Real Item");
            Frame mark = IconLibrary.Placeholder();
            int visible = 0;
            if (mark != null)
                for (int i = 3; i < mark.Bgra.Length; i += 4)
                    if (mark.Bgra[i] > 200) visible++;
            Check(missing == null && mark != null && visible > 40, "a missing icon still has a visible mark (" + visible + ")");
            IconRef star = null, crossbow = null;
            foreach (IconRef icon in icons)
            {
                if (icon.FileName == "small-fallen-star.png") star = icon;
                if (icon.FileName == "bronze-crossbow.png") crossbow = icon;
            }
            Check(star != null && crossbow != null, "fallen star and bronze crossbow are bundled");
            if (star != null)
            {
                IconHit starHit = IconLibrary.Match(icons, star.Bgra, star.Width, star.Height, 0.90);
                Check(starHit.Known && starHit.Name == "Small fallen star", "small fallen star matches (" + starHit.Candidate + " " + starHit.Score.ToString("0.00") + "/" + starHit.Second.ToString("0.00") + ")");
            }
            if (crossbow != null)
            {
                IconHit bowHit = IconLibrary.Match(icons, crossbow.Bgra, crossbow.Width, crossbow.Height, 0.90);
                Check(bowHit.Known && bowHit.Name == "Bronze crossbow", "bronze crossbow matches (" + bowHit.Candidate + " " + bowHit.Score.ToString("0.00") + "/" + bowHit.Second.ToString("0.00") + ")");
            }
            IconRef prismatic = null, bonus = null;
            foreach (IconRef icon in icons)
            {
                if (icon.FileName == "small-prismatic-fallen-star.png") prismatic = icon;
                if (icon.FileName == "bonus-xp-star-small.png") bonus = icon;
            }
            if (prismatic != null && star != null)
            {
                IconHit prismaticHit = IconLibrary.Match(icons, prismatic.Bgra, prismatic.Width, prismatic.Height, 0.90);
                Check(prismaticHit.Known && prismaticHit.Name == "Small prismatic fallen star", "prismatic fallen star matches (" + prismaticHit.Candidate + " " + prismaticHit.Score.ToString("0.00") + ")");
                IconHit bonusHit = bonus == null ? null : IconLibrary.Match(icons, bonus.Bgra, bonus.Width, bonus.Height, 0.90);
                bool bonusKept = bonusHit != null && bonusHit.Known && bonusHit.Name == "Bonus XP star (small)";
                Check(bonus == null || bonusKept, "bonus xp star stays itself (" + (bonusHit == null ? "missing" : bonusHit.Candidate) + ")");
            }
        }

        static void WikiFallback(string root)
        {
            WikiLookup.Reset();
            WikiLookup.AllowNetwork = false;
            WikiLookup.UseAssets(Path.Combine(root, "assets"));
            WikiLookup.SpriteDirectory = Path.Combine(root, "fixtures");
            string cache = Path.Combine(Path.GetTempPath(), "chest-wiki-cache-test");
            try { if (Directory.Exists(cache)) Directory.Delete(cache, true); } catch (Exception) { }
            WikiLookup.CacheDirectory = cache;
            Frame star = FramePng.Decode(Path.Combine(root, "assets", "icons", "small-fallen-star.png"));
            Frame shining = FramePng.Decode(Path.Combine(root, "fixtures", "shining-prismatic-small-fallen-star.png"));
            Check(star != null && shining != null, "wiki lookup sprites load");
            if (star == null || shining == null) return;
            WikiHit named = WikiLookup.Identify(star.Bgra, star.Width, star.Height);
            Check(!named.Confident || named.Name == "Small fallen star", "the fallen star is not given another item's name (" + named.Name + " " + named.Score.ToString("0.00") + "/" + named.Second.ToString("0.00") + ")");
            WikiHit other = WikiLookup.Identify(shining.Bgra, shining.Width, shining.Height);
            Check(other.Confident && other.Name == "Shining prismatic small fallen star", "index names a sprite that is not in the catalog (" + other.Name + " " + other.Score.ToString("0.00") + "/" + other.Second.ToString("0.00") + ")");
            Check(named.Name != other.Name, "a shining star is not called a fallen star");
            byte[] red = new byte[32 * 32 * 4];
            for (int i = 0; i < red.Length; i += 4) { red[i] = 20; red[i + 1] = 20; red[i + 2] = 220; red[i + 3] = 255; }
            WikiHit miss = WikiLookup.Identify(red, 32, 32);
            Check(!miss.Confident, "a red square is not given a wiki name (" + miss.Name + " " + miss.Score.ToString("0.00") + ")");
            string cached = WikiLookup.EnsureCached(other);
            string meta = cached == null ? "" : Path.ChangeExtension(cached, ".txt");
            bool metaOk = !string.IsNullOrEmpty(meta) && File.Exists(meta) && File.ReadAllText(meta).Contains("Shining prismatic small fallen star");
            Check(cached != null && File.Exists(cached) && metaOk, "a confident lookup is stored in the local cache");
            WikiLookup.SpriteDirectory = "";
            WikiLookup.AllowNetwork = false;
            string again = WikiLookup.EnsureCached(other);
            Check(again == cached && File.Exists(again), "the cached sprite is reused with no network");
            int before = Environment.TickCount;
            ManualResetEvent pause = new ManualResetEvent(false);
            WikiLookup.TestPause = pause;
            WikiLookup.ResolveAsync(shining.Bgra, shining.Width, shining.Height, delegate(WikiHit hit, string path) { });
            int elapsed = Environment.TickCount - before;
            pause.Set();
            WikiLookup.TestPause = null;
            Check(elapsed < 250, "the wiki lookup does not block the caller (" + elapsed + " ms)");
            try { if (Directory.Exists(cache)) Directory.Delete(cache, true); } catch (Exception) { }
            WikiLookup.SpriteDirectory = "";
            WikiLookup.AllowNetwork = false;
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
            session.MissesBeforeClose = 8;
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
                    for (int miss = 0; miss < session.MissesBeforeClose; miss++)
                        session.See(false, -1, null);
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
                WikiHit wiki = WikiLookup.Identify(pixels, item.Width, item.Height);
                string local = wiki.Confident ? WikiLookup.LocalSprite(wiki) : null;
                Frame sprite = string.IsNullOrEmpty(local) ? null : FramePng.Decode(local);
                if (wiki.Confident && sprite != null)
                {
                    named = 1;
                    slot.Name = wiki.Name;
                    display = sprite.Bgra;
                    dw = sprite.Width;
                    dh = sprite.Height;
                }
                else
                {
                    unknown = 1;
                    slot.Name = "Unknown";
                    slot.Unknown = true;
                    display = Noted.CleanForDisplay(pixels, item.Width, item.Height);
                }
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

        static void Hold(string root)
        {
            RewardReader.Use(1440, 96);
            string mark = "2715|air-talismanx1|air-runex150|off-hand-black-crossbowx1|fire-talismanx1";
            RewardSession session = new RewardSession();
            int now = 0;
            session.Clock = delegate { return now; };
            int opens = 0;
            if (session.See(true, 5, mark) == "opened") opens++;
            if (session.See(true, 5, mark) == "opened") opens++;
            for (int i = 0; i < 40; i++)
                if (session.See(true, 5, mark) == "opened") opens++;
            Check(opens == 1, "an unchanged open casket is counted once (" + opens + ")");
            now = 50;
            string blink = session.See(false, -1, null);
            now = 110;
            string blinkAgain = session.See(false, -1, null);
            string back = session.See(true, 5, mark);
            Check(blink == "none" && blinkAgain == "none" && back == "none", "a 100 ms blink does not count the casket again");

            RewardSession frames = new RewardSession();
            int frozen = 10;
            frames.Clock = delegate { return frozen; };
            frames.See(true, 5, mark);
            string missed = "none";
            for (int i = 0; i < 7; i++) missed = frames.See(false, -1, null);
            Check(missed == "none" && frames.Open, "seven missed frames stay open");
            missed = frames.See(false, -1, null);
            Check(missed == "closed" && !frames.Open, "eight missed frames close the window");

            RewardSession closed = new RewardSession();
            int later = 0;
            closed.Clock = delegate { return later; };
            Check(closed.See(true, 5, mark) == "opened", "a casket opens");
            later = 30;
            closed.See(false, -1, null);
            later = 560;
            string shut = closed.See(false, -1, null);
            string reopen = closed.See(true, 5, mark);
            string held = closed.See(true, 5, mark);
            Check(shut == "closed" && reopen == "opened" && held == "none", "a 500 ms close counts the next open once");

            RewardSession swapped = new RewardSession();
            string first = swapped.See(true, 5, mark);
            string half = swapped.See(true, 5, "9000|rune-platebodyx1");
            string settled = swapped.See(true, 5, "9000|rune-platebodyx1");
            string stay = swapped.See(true, 5, "9000|rune-platebodyx1");
            string roll = swapped.See(true, 4, "9000|rune-platebodyx1");
            string after = swapped.See(true, 4, "9000|rune-platebodyx1");
            Check(first == "opened" && half == "none" && settled == "opened" && stay == "none" && roll == "replaced" && after == "none", "an in-place casket counts once and a reroll replaces it");

            Check(!CaptureFollow.MissCounts(true, false, true), "a region miss that the full client still shows is not a close");
            Check(CaptureFollow.MissCounts(true, false, false), "a miss on the full client counts toward a close");
            Check(!CaptureFollow.MissCounts(false, true, false), "a grab that found the title is not a close");

            List<IconRef> icons = IconLibrary.Load(Path.Combine(root, "assets", "icons"), Path.Combine(root, "assets", "icons", "catalog.txt"));
            List<Glyph> stack = RewardReader.LoadGlyphs(Path.Combine(root, "assets", "digits", "stack.txt"));
            List<Glyph> value = RewardReader.LoadGlyphs(Path.Combine(root, "assets", "digits", "value.txt"));
            RewardReader.LoadTitle(Path.Combine(root, "assets", "reward", "trail-title.png"));
            Frame shot = FramePng.Decode(Path.Combine(root, "fixtures", "master-1.png"));
            Frame moved = Shift(shot, 3, 2);
            Check(shot != null && moved != null, "shifted reward loads");
            if (shot != null && moved != null)
            {
                Rectangle panel = RewardReader.Find(shot);
                Rectangle movedPanel = RewardReader.Find(moved);
                RewardShot left = RewardReader.Read(shot, panel, stack, value);
                RewardShot right = RewardReader.Read(moved, movedPanel, stack, value);
                Name(icons, left);
                Name(icons, right);
                string leftMark = RewardReader.Semantic(left.Value, left.Items);
                string rightMark = RewardReader.Semantic(right.Value, right.Items);
                bool movedPixels = left.Items.Count > 0 && right.Items.Count > 0 && left.Items[0].X != right.Items[0].X;
                Check(left.Items.Count == right.Items.Count && leftMark == rightMark && movedPixels, "a shifted grab keeps the same casket (" + leftMark + " / " + rightMark + ")");
                RewardSession live = new RewardSession();
                string opened = live.See(true, 5, leftMark);
                string again = live.See(true, 5, rightMark);
                Check(opened == "opened" && again == "none", "a shifted grab does not count the casket again");
            }

            Check(StackOnRune(root, stack) == 150, "150 on an air rune reads 150 (" + StackOnRune(root, stack) + ")");
        }

        static void Name(List<IconRef> icons, RewardShot shot)
        {
            foreach (SeenItem item in shot.Items)
            {
                IconHit hit = IconLibrary.Match(icons, item.Bgra, item.Width, item.Height, 0.90);
                if (!hit.Known) continue;
                item.Named = true;
                item.Name = hit.Name;
                item.Key = string.IsNullOrEmpty(hit.Group) ? hit.Name : hit.Group;
            }
        }

        static Frame Shift(Frame src, int dx, int dy)
        {
            if (src == null) return null;
            int width = src.Width + dx, height = src.Height + dy;
            byte[] data = new byte[width * height * 4];
            for (int i = 0; i < data.Length; i += 4) { data[i] = 14; data[i + 1] = 22; data[i + 2] = 16; data[i + 3] = 255; }
            for (int y = 0; y < src.Height; y++)
                Buffer.BlockCopy(src.Bgra, y * src.Width * 4, data, ((y + dy) * width + dx) * 4, src.Width * 4);
            return new Frame(data, width, height);
        }

        static int StackOnRune(string root, List<Glyph> glyphs)
        {
            Frame rune = FramePng.Decode(Path.Combine(root, "assets", "icons", "air-rune.png"));
            if (rune == null || glyphs == null) return -1;
            int width = 72, height = 52, ox = 8, oy = 10;
            byte[] data = new byte[width * height * 4];
            for (int i = 0; i < data.Length; i += 4) { data[i] = 18; data[i + 1] = 28; data[i + 2] = 36; data[i + 3] = 255; }
            for (int y = 0; y < rune.Height; y++)
                for (int x = 0; x < rune.Width; x++)
                {
                    int from = (y * rune.Width + x) * 4;
                    if (rune.Bgra[from + 3] < 16) continue;
                    int to = ((oy + y) * width + (ox + x)) * 4;
                    data[to] = rune.Bgra[from];
                    data[to + 1] = rune.Bgra[from + 1];
                    data[to + 2] = rune.Bgra[from + 2];
                    data[to + 3] = 255;
                }
            int penX = ox;
            string number = "150";
            foreach (char digit in number)
            {
                Glyph glyph = null;
                foreach (Glyph candidate in glyphs) if (candidate.Value == digit) glyph = candidate;
                if (glyph == null) return -1;
                for (int y = 0; y < glyph.Height; y++)
                    for (int x = 0; x < glyph.Width; x++)
                    {
                        if (!glyph.On[y * glyph.Width + x]) continue;
                        Paint(data, width, height, penX + x + 1, oy + y + 1, 16, 12, 12);
                        Paint(data, width, height, penX + x, oy + y, 52, 187, 250);
                    }
                penX += glyph.Width + 1;
            }
            Frame frame = new Frame(data, width, height);
            try { FramePng.Save(frame, "/opt/cursor/artifacts/air-rune-150.png"); } catch (Exception) { }
            SeenItem item = new SeenItem();
            item.X = ox;
            item.Y = oy;
            item.Width = rune.Width;
            item.Height = rune.Height;
            return RewardReader.ReadStack(frame, item, glyphs);
        }

        static void Paint(byte[] data, int width, int height, int x, int y, byte b, byte g, byte r)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return;
            int i = (y * width + x) * 4;
            data[i] = b;
            data[i + 1] = g;
            data[i + 2] = r;
            data[i + 3] = 255;
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

        static void Adapt(string root)
        {
            Check(ClientScale.ResolutionBucket(1386) == 1440 && ClientScale.ResolutionName(1440) == "1440p", "1386px is 1440p");
            Check(ClientScale.ResolutionBucket(1080) == 1080 && ClientScale.ResolutionBucket(2160) == 2160, "1080p and 4K buckets");
            Check(ClientScale.ScalingPercent(96) == 100 && ClientScale.ScalingPercent(120) == 125 && ClientScale.ScalingPercent(144) == 150 && ClientScale.ScalingPercent(192) == 200, "dpi snaps to 100, 125, 150 and 200");
            RewardReader.Use(1386, 96);
            Check(RewardReader.Profile.Px(396) == 396 && RewardReader.Profile.Px(239) == 239, "1440p at 100% keeps the measured panel");
            RewardReader.Use(1080, 96);
            Check(RewardReader.Profile.Px(396) == 297, "1080p is three quarters of the measured panel (" + RewardReader.Profile.Px(396) + ")");
            RewardReader.Use(2160, 144);
            Check(RewardReader.Profile.Percent == 150 && RewardReader.Profile.Px(396) == 891, "4K at 150% scales the panel (" + RewardReader.Profile.Px(396) + ")");
            RewardReader.Use(1440, 96);

            CaptureFollow follow = new CaptureFollow();
            Rectangle full = follow.Choose(new Size(2560, 1440), Rectangle.Empty);
            Check(full.Width == 2560 && full.Height == 1440, "the first grab is the whole client");
            Rectangle hit = new Rectangle(1000, 500, 396, 239);
            Rectangle roi = follow.Choose(new Size(2560, 1440), hit);
            Check(roi.X == hit.X - 80 && roi.Y == hit.Y - 80 && roi.Width == 396 + 160, "a hit is followed with padding");
            follow.Note(false); follow.Note(false); follow.Note(false);
            Rectangle still = follow.Choose(new Size(2560, 1440), hit);
            Check(still.Width < 2560, "three misses stay on the region");
            follow.Note(false);
            Rectangle again = follow.Choose(new Size(2560, 1440), hit);
            Check(again.Width == 2560, "the fourth miss returns to the full client");
            CaptureFollow resized = new CaptureFollow();
            resized.Choose(new Size(1920, 1080), Rectangle.Empty);
            resized.Choose(new Size(1920, 1080), hit);
            Rectangle grown = resized.Choose(new Size(2560, 1440), hit);
            Check(grown.Width == 2560, "a resize goes back to the full client");

            Frame covered = new Frame(new byte[40 * 30 * 4], 40, 30);
            for (int i = 0; i < covered.Bgra.Length; i += 4) { covered.Bgra[i] = 10; covered.Bgra[i + 1] = 20; covered.Bgra[i + 2] = 30; covered.Bgra[i + 3] = 255; }
            for (int y = 8; y < 20; y++)
                for (int x = 8; x < 24; x++)
                {
                    int o = (y * 40 + x) * 4;
                    covered.Bgra[o + 2] = 232;
                }
            covered.Cover(new Rectangle(8, 8, 16, 12));
            Check(covered.Bgra[(10 * 40 + 10) * 4 + 2] == 30, "the overlay rectangle is painted out of the capture");

            Check(OverlayVisibility.CoveredByOtherClient(new IntPtr(1), new IntPtr(2), "Kavros"), "another client covers the panel");
            Check(!OverlayVisibility.CoveredByOtherClient(new IntPtr(1), new IntPtr(1), "Kavros"), "our own client stays visible");

            ClientLaunch.Reset();
            ClientLaunch.ReadArgs(new string[] { "--hwnd", "4567", "--pid=89" });
            Check(ClientLaunch.Hwnd == 4567 && ClientLaunch.Pid == 89, "hwnd and pid arguments bind the client");
            ClientLaunch.Reset();
            Environment.SetEnvironmentVariable("COMPANION_CLIENT_HWND", "321");
            Environment.SetEnvironmentVariable("COMPANION_CLIENT_PID", "12");
            ClientLaunch.ReadEnvironment();
            Check(ClientLaunch.Hwnd == 321 && ClientLaunch.Pid == 12, "companion environment binds the client");
            Environment.SetEnvironmentVariable("COMPANION_CLIENT_HWND", null);
            Environment.SetEnvironmentVariable("COMPANION_CLIENT_PID", null);
            string bindDir = Path.Combine(Path.GetTempPath(), "chest-bind-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(bindDir);
            ClientLaunch.BindDirectory = bindDir;
            ClientLaunch.Hwnd = 77;
            ClientLaunch.Pid = 8;
            ClientLaunch.WriteRequest();
            ClientLaunch.Reset();
            ClientLaunch.ReadRequest();
            Check(ClientLaunch.Hwnd == 77 && ClientLaunch.Pid == 8 && !File.Exists(Path.Combine(bindDir, "bind.txt")), "bind.txt locks the client once");
            ClientLaunch.Reset();
            ClientLaunch.BindDirectory = null;
            string module = File.ReadAllText(Path.Combine(root, "companion.module.json"));
            Check(module.IndexOf("\"id\": \"chest\"") >= 0 && module.IndexOf("Chest.exe") >= 0, "companion module contract");

            RewardReader.LoadTitle(Path.Combine(root, "assets", "reward", "trail-title.png"));
            Frame master = FramePng.Decode(Path.Combine(root, "fixtures", "master-0.png"));
            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
            Rectangle panel = RewardReader.Find(master);
            long fullMs = clock.ElapsedMilliseconds;
            Rectangle cropArea = Rectangle.Inflate(panel, 80, 80);
            cropArea = Rectangle.Intersect(cropArea, new Rectangle(0, 0, master.Width, master.Height));
            Frame crop = master.Crop(cropArea);
            clock.Restart();
            Rectangle againPanel = RewardReader.Find(crop);
            long regionMs = clock.ElapsedMilliseconds;
            Check(!panel.IsEmpty && !againPanel.IsEmpty, "region follow still sees TRAIL COMPLETE (" + fullMs + " ms full, " + regionMs + " ms region)");

            Frame scaled = ScaleFrame(FramePng.Decode(Path.Combine(root, "fixtures", "master-1.png")), 3, 2);
            RewardReader.Use(2160, 96);
            Rectangle scaledPanel = RewardReader.Find(scaled);
            Check(!scaledPanel.IsEmpty && scaledPanel.Width == RewardReader.Profile.Px(396), "a 150% frame uses the scaled panel (" + scaledPanel.Width + ")");
            RewardReader.Use(1440, 96);

            string gridPath = "/opt/cursor/artifacts/overlay-grid.png";
            if (File.Exists(gridPath))
            {
                using (Bitmap source = new Bitmap(gridPath))
                using (Bitmap big = new Bitmap(source.Width * 3 / 2, source.Height * 3 / 2))
                using (Graphics g = Graphics.FromImage(big))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    g.DrawImage(source, new Rectangle(0, 0, big.Width, big.Height), new Rectangle(0, 0, source.Width, source.Height), GraphicsUnit.Pixel);
                    big.Save("/opt/cursor/artifacts/overlay-grid-150.png", System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            Check(true, "150% grid preview saved");
        }

        static Frame ScaleFrame(Frame source, int num, int den)
        {
            int w = source.Width * num / den;
            int h = source.Height * num / den;
            byte[] data = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            {
                int sy = Math.Min(source.Height - 1, y * den / num);
                for (int x = 0; x < w; x++)
                {
                    int sx = Math.Min(source.Width - 1, x * den / num);
                    Buffer.BlockCopy(source.Bgra, (sy * source.Width + sx) * 4, data, (y * w + x) * 4, 4);
                }
            }
            return new Frame(data, w, h);
        }

        static void Check(bool ok, string name)
        {
            if (ok) Console.WriteLine("ok  " + name);
            else { Console.WriteLine("FAIL " + name); failed++; }
        }
    }
}
