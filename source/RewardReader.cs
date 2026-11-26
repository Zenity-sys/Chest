using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace Chest
{
    internal sealed class Glyph
    {
        public char Value;
        public int Width, Height;
        public bool[] On;
    }

    internal sealed class SeenItem
    {
        public int X, Y, Width, Height, Quantity;
        public byte[] Bgra;
    }

    internal sealed class RewardShot
    {
        public int Value;
        public string Fingerprint = "";
        public readonly List<SeenItem> Items = new List<SeenItem>();
    }

    // The reward panel on the measured client is 396 by 239. The red
    // "TRAIL COMPLETE!" tab is the anchor. Item icons sit in one strip.
    // A reroll keeps that panel open and only changes the strip.
    internal static class RewardReader
    {
        public const int PanelWidth = 396;
        public const int PanelHeight = 239;

        public static List<Glyph> LoadGlyphs(string path)
        {
            List<Glyph> glyphs = new List<Glyph>();
            if (!File.Exists(path)) return glyphs;
            string[] lines = File.ReadAllLines(path);
            int i = 0;
            while (i < lines.Length)
            {
                string header = lines[i].Trim();
                i++;
                if (header.Length == 0 || header.StartsWith("#")) continue;
                List<string> rows = new List<string>();
                while (i < lines.Length && lines[i].Trim().Length > 0)
                {
                    rows.Add(lines[i].TrimEnd());
                    i++;
                }
                if (rows.Count == 0 || header.Length == 0) continue;
                int width = 0;
                foreach (string row in rows) if (row.Length > width) width = row.Length;
                bool[] on = new bool[width * rows.Count];
                for (int y = 0; y < rows.Count; y++)
                    for (int x = 0; x < rows[y].Length; x++)
                        on[y * width + x] = rows[y][x] == '#';
                glyphs.Add(new Glyph { Value = header[0], Width = width, Height = rows.Count, On = on });
            }
            return glyphs;
        }

        public static Rectangle Find(Frame frame)
        {
            if (frame == null || frame.Bgra == null) return Rectangle.Empty;
            byte[] px = frame.Bgra;
            int w = frame.Width, h = frame.Height;
            // One pass, top to bottom. The first red tab whose title reads
            // TRAIL COMPLETE wins, so a popup above it cannot hide the casket.
            for (int y = 0; y < h; y++)
            {
                int row = y * w * 4;
                int x = 0;
                while (x < w)
                {
                    int i = row + x * 4;
                    if (DarkRedRaw(px, i))
                    {
                        int x0 = x;
                        while (x < w && DarkRedRaw(px, row + x * 4)) x++;
                        int length = x - x0;
                        if (length >= 110 && length <= 190 && GoldCount(frame, y, x0) >= 80)
                        {
                            int left = x0 - 2, top = y - 4;
                            if (left >= 0 && top >= 0 && left + PanelWidth <= w && top + PanelHeight <= h)
                            {
                                Rectangle panel = new Rectangle(left, top, PanelWidth, PanelHeight);
                                if (titleTemplate == null || TitleMatches(frame, panel)) return panel;
                            }
                        }
                    }
                    else x++;
                }
            }
            return Rectangle.Empty;
        }

        static bool DarkRedRaw(byte[] px, int i)
        {
            int b = px[i], g = px[i + 1], r = px[i + 2];
            return r >= 40 && r <= 150 && g <= 48 && b <= 48 && r > g + 22 && r > b + 22;
        }

        static int GoldCount(Frame frame, int y, int x0)
        {
            int gold = 0;
            int bottom = Math.Min(frame.Height, y + 18);
            int right = Math.Min(frame.Width, x0 + 160);
            for (int yy = y; yy < bottom; yy++)
                for (int xx = x0; xx < right; xx++)
                    if (Gold(frame, xx, yy)) gold++;
            return gold;
        }

        public static string Fingerprint(Frame frame, Rectangle panel)
        {
            ulong hash = 1469598103934665603UL;
            int top = panel.Y + 46, bottom = panel.Y + 90, left = panel.X + 16, right = panel.X + 378;
            for (int y = top; y < bottom; y += 3)
                for (int x = left; x < right; x += 3)
                {
                    int r, g, b;
                    frame.Get(x, y, out r, out g, out b);
                    hash = Mix(hash, (byte)(r / 32));
                    hash = Mix(hash, (byte)(g / 32));
                    hash = Mix(hash, (byte)(b / 32));
                }
            // The value line changes on a reroll even when a coarse item hash would not.
            for (int y = panel.Y + 112; y < panel.Y + 130; y += 2)
                for (int x = panel.X + 40; x < panel.X + 360; x += 3)
                {
                    int r, g, b;
                    frame.Get(x, y, out r, out g, out b);
                    hash = Mix(hash, (byte)(r / 16));
                    hash = Mix(hash, (byte)(g / 16));
                    hash = Mix(hash, (byte)(b / 16));
                }
            return hash.ToString("x16");
        }

        static ulong Mix(ulong hash, byte value)
        {
            unchecked { return (hash ^ value) * 1099511628211UL; }
        }

        public static RewardShot Read(Frame frame, Rectangle panel, List<Glyph> stack, List<Glyph> valueDigits)
        {
            RewardShot shot = new RewardShot();
            shot.Items.AddRange(ReadItems(frame, panel, stack));
            shot.Value = ReadValue(frame, panel, valueDigits);
            shot.Fingerprint = shot.Value.ToString() + ":" + Fingerprint(frame, panel);
            foreach (SeenItem item in shot.Items)
                shot.Fingerprint += ":" + item.Quantity + ":" + item.X + ":" + item.Y;
            return shot;
        }

        static List<SeenItem> ReadItems(Frame frame, Rectangle panel, List<Glyph> stack)
        {
            int top = panel.Y + 46, bottom = panel.Y + 90, left = panel.X + 16, right = panel.X + 378;
            int width = right - left, height = bottom - top;
            List<int> rs = new List<int>(), gs = new List<int>(), bs = new List<int>();
            for (int y = top; y < bottom; y += 2)
                for (int x = left; x < right; x += 4)
                {
                    int r, g, b;
                    frame.Get(x, y, out r, out g, out b);
                    rs.Add(r); gs.Add(g); bs.Add(b);
                }
            rs.Sort(); gs.Sort(); bs.Sort();
            int mid = rs.Count / 2;
            int br = rs[mid], bg = gs[mid], bb = bs[mid];
            byte[] mask = new byte[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int r, g, b;
                    frame.Get(left + x, top + y, out r, out g, out b);
                    if (Math.Abs(r - br) + Math.Abs(g - bg) + Math.Abs(b - bb) > 36) mask[y * width + x] = 1;
                }
            byte[] seen = new byte[width * height];
            List<int[]> comps = new List<int[]>();
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    if (mask[index] == 0 || seen[index] != 0) continue;
                    int minx = x, maxx = x, miny = y, maxy = y, count = 0;
                    Stack<Point> stackPoints = new Stack<Point>();
                    stackPoints.Push(new Point(x, y));
                    seen[index] = 1;
                    while (stackPoints.Count > 0)
                    {
                        Point p = stackPoints.Pop();
                        count++;
                        if (p.X < minx) minx = p.X;
                        if (p.X > maxx) maxx = p.X;
                        if (p.Y < miny) miny = p.Y;
                        if (p.Y > maxy) maxy = p.Y;
                        Push(mask, seen, stackPoints, width, height, p.X + 1, p.Y);
                        Push(mask, seen, stackPoints, width, height, p.X - 1, p.Y);
                        Push(mask, seen, stackPoints, width, height, p.X, p.Y + 1);
                        Push(mask, seen, stackPoints, width, height, p.X, p.Y - 1);
                    }
                    comps.Add(new int[] { minx, miny, maxx, maxy, count });
                }
            bool changed = true;
            while (changed)
            {
                changed = false;
                List<int[]> next = new List<int[]>();
                bool[] used = new bool[comps.Count];
                for (int i = 0; i < comps.Count; i++)
                {
                    if (used[i]) continue;
                    int ax0 = comps[i][0], ay0 = comps[i][1], ax1 = comps[i][2], ay1 = comps[i][3], an = comps[i][4];
                    for (int j = i + 1; j < comps.Count; j++)
                    {
                        if (used[j]) continue;
                        int bx0 = comps[j][0], by0 = comps[j][1], bx1 = comps[j][2], by1 = comps[j][3];
                        int xov = Math.Min(ax1, bx1) - Math.Max(ax0, bx0);
                        int ygap = Math.Min(ay1, by1) + 1 >= Math.Max(ay0, by0) ? 0 : Math.Max(ay0, by0) - Math.Min(ay1, by1) - 1;
                        if (xov >= -4 && ygap <= 6)
                        {
                            ax0 = Math.Min(ax0, bx0); ay0 = Math.Min(ay0, by0); ax1 = Math.Max(ax1, bx1); ay1 = Math.Max(ay1, by1);
                            an += comps[j][4];
                            used[j] = true;
                            changed = true;
                        }
                    }
                    next.Add(new int[] { ax0, ay0, ax1, ay1, an });
                }
                comps = next;
            }
            List<SeenItem> items = new List<SeenItem>();
            foreach (int[] box in comps)
            {
                int bw = box[2] - box[0] + 1, bh = box[3] - box[1] + 1;
                if (bw < 8 || bh < 8 || bw > 50 || bh > 40) continue;
                if (box[4] < bw * bh * 0.12) continue;
                SeenItem item = new SeenItem();
                item.X = left + box[0];
                item.Y = top + box[1];
                item.Width = bw;
                item.Height = bh;
                item.Quantity = ReadStack(frame, item, stack);
                item.Bgra = frame.Crop(new Rectangle(item.X, item.Y, item.Width, item.Height)).Bgra;
                items.Add(item);
            }
            items.Sort(delegate(SeenItem a, SeenItem b) { return a.X.CompareTo(b.X); });
            return items;
        }

        static void Push(byte[] mask, byte[] seen, Stack<Point> stack, int width, int height, int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return;
            int index = y * width + x;
            if (mask[index] == 0 || seen[index] != 0) return;
            seen[index] = 1;
            stack.Push(new Point(x, y));
        }

        static int ReadStack(Frame frame, SeenItem item, List<Glyph> glyphs)
        {
            int top = Math.Max(0, item.Y - 3);
            int bottom = Math.Min(frame.Height - 1, item.Y + 12);
            int left = Math.Max(0, item.X - 1);
            int right = Math.Min(frame.Width - 1, item.X + 16);
            int minx = int.MaxValue, maxx = -1, miny = int.MaxValue, maxy = -1, count = 0;
            for (int y = top; y <= bottom; y++)
                for (int x = left; x <= right; x++)
                    if (Yellow(frame, x, y))
                    {
                        count++;
                        if (x < minx) minx = x;
                        if (x > maxx) maxx = x;
                        if (y < miny) miny = y;
                        if (y > maxy) maxy = y;
                    }
            if (count < 4) return 1;
            bool[] col = new bool[maxx - minx + 1];
            for (int x = minx; x <= maxx; x++)
                for (int y = miny; y <= maxy; y++)
                    if (Yellow(frame, x, y)) col[x - minx] = true;
            string text = "";
            int i = 0;
            while (i < col.Length)
            {
                if (!col[i]) { i++; continue; }
                int j = i;
                while (j < col.Length && col[j]) j++;
                int gw = j - i, gh = maxy - miny + 1;
                bool[] bits = new bool[gw * gh];
                for (int y = 0; y < gh; y++)
                    for (int x = 0; x < gw; x++)
                        bits[y * gw + x] = Yellow(frame, minx + i + x, miny + y);
                Trim(ref bits, ref gw, ref gh);
                if (gw > 0 && gh > 0)
                {
                    if (gw * 2 < gh) text += "1";
                    else
                    {
                        char ch;
                        double score;
                        Best(glyphs, bits, gw, gh, out ch, out score);
                        text += score >= 0.80 ? ch.ToString() : "?";
                    }
                }
                i = j;
            }
            int quantity;
            if (text.Length == 0 || text.IndexOf('?') >= 0 || !int.TryParse(text, out quantity) || quantity < 1) return 1;
            return quantity;
        }

        static int ReadValue(Frame frame, Rectangle panel, List<Glyph> glyphs)
        {
            if (glyphs == null || glyphs.Count == 0) return 0;
            List<int[]> hits = new List<int[]>();
            int y = panel.Y + 116;
            int x = panel.X + 40;
            int limit = panel.X + 360;
            while (x < limit)
            {
                Glyph best = null;
                double bestScore = 0;
                foreach (Glyph glyph in glyphs)
                {
                    double score = Score(frame, x, y, glyph, delegate(int px, int py) { return ValueInk(frame, px, py); });
                    int wide = glyph.Width;
                    if (score >= 0.86 && (best == null || score > bestScore + 0.015 || (score >= bestScore - 0.015 && wide > best.Width)))
                    {
                        best = glyph;
                        bestScore = score;
                    }
                }
                if (best != null)
                {
                    hits.Add(new int[] { x, best.Value, best.Width });
                    x += best.Width;
                }
                else x++;
            }
            List<List<int[]>> runs = new List<List<int[]>>();
            List<int[]> current = new List<int[]>();
            foreach (int[] hit in hits)
            {
                if (current.Count > 0 && hit[0] > current[current.Count - 1][0] + current[current.Count - 1][2] + 6)
                {
                    runs.Add(current);
                    current = new List<int[]>();
                }
                current.Add(hit);
            }
            if (current.Count > 0) runs.Add(current);
            List<int[]> chosen = null;
            int chosenVaried = -1, chosenLength = -1;
            foreach (List<int[]> run in runs)
            {
                bool varied = false;
                foreach (int[] hit in run) if (hit[1] != '1') varied = true;
                int rank = varied ? 1 : 0;
                if (rank > chosenVaried || (rank == chosenVaried && run.Count > chosenLength))
                {
                    chosen = run;
                    chosenVaried = rank;
                    chosenLength = run.Count;
                }
            }
            if (chosen == null) return 0;
            string text = "";
            foreach (int[] hit in chosen) text += (char)hit[1];
            int value;
            return int.TryParse(text, out value) ? value : 0;
        }

        static double Score(Frame frame, int x, int y, Glyph glyph, Func<int, int, bool> ink)
        {
            if (x < 0 || y < 0 || x + glyph.Width > frame.Width || y + glyph.Height > frame.Height) return -1;
            int hit = 0, total = glyph.Width * glyph.Height;
            for (int yy = 0; yy < glyph.Height; yy++)
                for (int xx = 0; xx < glyph.Width; xx++)
                    if (ink(x + xx, y + yy) == glyph.On[yy * glyph.Width + xx]) hit++;
            return total == 0 ? 0 : hit / (double)total;
        }

        static void Best(List<Glyph> glyphs, bool[] bits, int width, int height, out char ch, out double score)
        {
            ch = '?';
            score = 0;
            if (glyphs == null) return;
            foreach (Glyph glyph in glyphs)
            {
                double s = Match(bits, width, height, glyph);
                if (s > score) { score = s; ch = glyph.Value; }
            }
        }

        static double Match(bool[] bits, int width, int height, Glyph glyph)
        {
            if (width < 1 || height < 1) return 0;
            int hit = 0, total = 0;
            for (int y = 0; y < height; y++)
            {
                int ty = Math.Min(glyph.Height - 1, y * glyph.Height / height);
                for (int x = 0; x < width; x++)
                {
                    int tx = Math.Min(glyph.Width - 1, x * glyph.Width / width);
                    total++;
                    if (bits[y * width + x] == glyph.On[ty * glyph.Width + tx]) hit++;
                }
            }
            return total == 0 ? 0 : hit / (double)total;
        }

        static void Trim(ref bool[] bits, ref int width, ref int height)
        {
            int top = 0, bottom = height - 1;
            while (top <= bottom && RowEmpty(bits, width, top)) top++;
            while (bottom >= top && RowEmpty(bits, width, bottom)) bottom--;
            if (top > bottom) { bits = new bool[0]; width = height = 0; return; }
            int left = 0, right = width - 1;
            while (left <= right && ColEmpty(bits, width, height, left)) left++;
            while (right >= left && ColEmpty(bits, width, height, right)) right--;
            int nw = right - left + 1, nh = bottom - top + 1;
            bool[] next = new bool[nw * nh];
            for (int y = 0; y < nh; y++)
                for (int x = 0; x < nw; x++)
                    next[y * nw + x] = bits[(top + y) * width + left + x];
            bits = next;
            width = nw;
            height = nh;
        }

        static bool RowEmpty(bool[] bits, int width, int y)
        {
            for (int x = 0; x < width; x++) if (bits[y * width + x]) return false;
            return true;
        }
        static bool ColEmpty(bool[] bits, int width, int height, int x)
        {
            for (int y = 0; y < height; y++) if (bits[y * width + x]) return false;
            return true;
        }

        static bool DarkRed(Frame frame, int x, int y)
        {
            int r, g, b;
            frame.Get(x, y, out r, out g, out b);
            return r >= 40 && r <= 150 && g <= 48 && b <= 48 && r > g + 22 && r > b + 22;
        }
        static bool Gold(Frame frame, int x, int y)
        {
            int r, g, b;
            frame.Get(x, y, out r, out g, out b);
            return r > 185 && g > 130 && b < 100 && (r - b) > 90 && g > b + 30;
        }
        static bool Yellow(Frame frame, int x, int y)
        {
            int r, g, b;
            frame.Get(x, y, out r, out g, out b);
            return r > 210 && g > 175 && b < 70 && (r - b) > 140;
        }
        static bool ValueInk(Frame frame, int x, int y)
        {
            int r, g, b;
            frame.Get(x, y, out r, out g, out b);
            return (r + g + b) / 3 > 145 && Math.Abs(r - g) < 60 && b > 110;
        }

        static Frame titleTemplate;

        public static void LoadTitle(string path)
        {
            titleTemplate = FramePng.Decode(path);
        }

        // The red tab alone is not enough. The gold title has to read TRAIL COMPLETE.
        public static bool TitleMatches(Frame frame, Rectangle panel)
        {
            if (titleTemplate == null || frame == null || panel.Width < 170) return false;
            long sad = 0;
            int n = 0;
            for (int y = 0; y < titleTemplate.Height; y++)
                for (int x = 0; x < titleTemplate.Width; x++)
                {
                    int r1, g1, b1, r2, g2, b2;
                    frame.Get(panel.X + 10 + x, panel.Y + 4 + y, out r1, out g1, out b1);
                    titleTemplate.Get(x, y, out r2, out g2, out b2);
                    sad += Math.Abs(r1 - r2) + Math.Abs(g1 - g2) + Math.Abs(b1 - b2);
                    n++;
                }
            return n > 0 && sad / n < 18;
        }

        // The number in REROLL REWARD (n). -1 when it cannot be read.
        // n caps at 5. A reroll is only n dropping by exactly one.
        public static int RerollCount(Frame frame, Rectangle panel)
        {
            if (frame == null || panel.Width < 200) return -1;
            int paren = -1;
            for (int x = panel.X + 255; x < panel.X + 278; x++)
            {
                int ink = 0;
                for (int y = panel.Y + 200; y < panel.Y + 216; y++)
                    if (ButtonInk(frame, x, y)) ink++;
                if (ink < 6) continue;
                int left = 0;
                for (int y = panel.Y + 200; y < panel.Y + 216; y++)
                    if (ButtonInk(frame, x - 1, y)) left++;
                if (left < 3) paren = x;
            }
            if (paren < 0) return -1;
            int best = -1;
            double bestRatio = 0;
            for (int digit = 0; digit < ChargeShapes.Length; digit++)
            {
                int score = 0;
                int compared = 0;
                string[] rows = ChargeShapes[digit];
                for (int y = 0; y < rows.Length; y++)
                {
                    string row = rows[y];
                    for (int x = 0; x < row.Length; x++)
                    {
                        bool on = ButtonInk(frame, paren - 8 + x, panel.Y + 203 + y);
                        bool want = row[x] == '#';
                        if (on == want) score++;
                        compared++;
                    }
                }
                double ratio = compared == 0 ? 0 : score / (double)compared;
                if (ratio > bestRatio)
                {
                    bestRatio = ratio;
                    best = digit;
                }
            }
            if (bestRatio < 0.82) return -1;
            return best;
        }

        static bool ButtonInk(Frame frame, int x, int y)
        {
            int r, g, b;
            frame.Get(x, y, out r, out g, out b);
            return r + g + b < 120 && r < 110 && g < 90;
        }

        // 5-wide glyphs taken from the reward button, plus the same stroke style for 0-2.
        static readonly string[][] ChargeShapes = new string[][] {
            new string[] { ".##..", "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".##.." },
            new string[] { ".#...", "##...", ".#...", ".#...", ".#...", ".#...", ".#...", "###.." },
            new string[] { "###..", "...#.", "...#.", "..#..", ".#...", "#....", "#....", "####." },
            new string[] { "###..", "...#.", "...#.", ".....", "..##.", "...##", "...##", "...#.", "###.." },
            new string[] { "..#..", "..#..", ".##..", "..#..", "..#..", "..#..", "####.", "..#..", ".##.." },
            new string[] { "####.", "#....", "#....", ".##..", "..##.", "...#.", "...#.", "...#.", "##..." }
        };
    }

    // opened: a reward window that is not a one-step reroll. Count it.
    // replaced: REROLL REWARD (n) fell by exactly one on this same window.
    // closed: the window has been gone long enough that the next one is new.
    internal sealed class RewardSession
    {
        public int MissesBeforeClose = 3;
        public bool Open;
        int misses;
        int charges = -1;

        public string See(bool visible, int charges, string mark)
        {
            if (!visible)
            {
                if (!Open) return "none";
                misses++;
                if (misses >= MissesBeforeClose)
                {
                    Open = false;
                    misses = 0;
                    this.charges = -1;
                    return "closed";
                }
                return "none";
            }
            misses = 0;
            if (!Open)
            {
                Open = true;
                this.charges = charges;
                return "opened";
            }
            if (this.charges >= 0 && charges == this.charges - 1)
            {
                this.charges = charges;
                return "replaced";
            }
            // The picture jitters while the window stays up. That is not a new casket.
            // A refill (n goes up) is not one either. A new casket needs the window to close first.
            if (charges >= 0) this.charges = charges;
            return "none";
        }

        public void Reset()
        {
            Open = false;
            misses = 0;
            charges = -1;
        }
    }
}
