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
        public string Key = "";
        public string Name = "";
        public bool Named;
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
        static RewardProfile profile = RewardProfile.Native;

        public static RewardProfile Profile { get { return profile; } }

        public static void Use(int clientHeight, int dpi)
        {
            RewardProfile next = RewardProfile.For(clientHeight, dpi);
            if (next.Bucket == profile.Bucket && next.Percent == profile.Percent) return;
            profile = next;
        }

        static int Px(int native) { return profile.Px(native); }

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
                        if (length >= Px(110) && length <= Px(190) && GoldCount(frame, y, x0) >= Px(80))
                        {
                            int left = x0 - Px(2), top = y - Px(4);
                            if (left >= 0 && top >= 0 && left + Px(PanelWidth) <= w && top + Px(PanelHeight) <= h)
                            {
                                Rectangle panel = new Rectangle(left, top, Px(PanelWidth), Px(PanelHeight));
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
            int bottom = Math.Min(frame.Height, y + Px(18));
            int right = Math.Min(frame.Width, x0 + Px(160));
            for (int yy = y; yy < bottom; yy++)
                for (int xx = x0; xx < right; xx++)
                    if (Gold(frame, xx, yy)) gold++;
            return gold;
        }

        // Item names, quantities, and the casket value. Pixel position is not part of it,
        // so a shifted grab of the same reward stays the same casket.
        public static string Semantic(int value, IList<SeenItem> items)
        {
            StringBuilder text = new StringBuilder();
            text.Append(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (items == null) return text.ToString();
            foreach (SeenItem item in items)
            {
                string key = item == null ? "" : item.Key;
                if (string.IsNullOrEmpty(key)) key = "q";
                text.Append('|');
                text.Append(key);
                text.Append('x');
                text.Append((item == null ? 0 : item.Quantity).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            return text.ToString();
        }

        public static RewardShot Read(Frame frame, Rectangle panel, List<Glyph> stack, List<Glyph> valueDigits)
        {
            RewardShot shot = new RewardShot();
            shot.Items.AddRange(ReadItems(frame, panel, stack));
            shot.Value = ReadValue(frame, panel, valueDigits);
            shot.Fingerprint = Semantic(shot.Value, shot.Items);
            return shot;
        }

        static List<SeenItem> ReadItems(Frame frame, Rectangle panel, List<Glyph> stack)
        {
            int top = panel.Y + Px(46), bottom = panel.Y + Px(90), left = panel.X + Px(16), right = panel.X + Px(378);
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
                if (bw < Px(8) || bh < Px(8) || bw > Px(50) || bh > Px(40)) continue;
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

        internal static int ReadStack(Frame frame, SeenItem item, List<Glyph> glyphs)
        {
            // Three digits plus the black drop shadow. Stay in the top of the icon.
            int top = Math.Max(0, item.Y - Px(2));
            int bottom = Math.Min(frame.Height - 1, item.Y + Px(10));
            int left = Math.Max(0, item.X - Px(2));
            int right = Math.Min(frame.Width - 1, item.X + Px(26));
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
                    char ch;
                    double score;
                    Best(glyphs, bits, gw, gh, out ch, out score);
                    // A thin stroke is the digit 1 even when its template is a poor fit.
                    if (score < 0.80 && gw * 2 < gh) { ch = '1'; score = 0.80; }
                    text += score >= 0.80 ? ch.ToString() : "?";
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
            int y = panel.Y + Px(116);
            int x = panel.X + Px(40);
            int limit = panel.X + Px(360);
            while (x < limit)
            {
                Glyph best = null;
                double bestScore = 0;
                foreach (Glyph glyph in glyphs)
                {
                    double score = Score(frame, x, y, glyph, delegate(int px, int py) { return ValueInk(frame, px, py); });
                    int wide = Px(glyph.Width);
                    if (score >= 0.86 && (best == null || score > bestScore + 0.015 || (score >= bestScore - 0.015 && wide > (best == null ? 0 : Px(best.Width)))))
                    {
                        best = glyph;
                        bestScore = score;
                    }
                }
                if (best != null)
                {
                    hits.Add(new int[] { x, best.Value, Px(best.Width) });
                    x += Px(best.Width);
                }
                else x++;
            }
            List<List<int[]>> runs = new List<List<int[]>>();
            List<int[]> current = new List<int[]>();
            foreach (int[] hit in hits)
            {
                if (current.Count > 0 && hit[0] > current[current.Count - 1][0] + current[current.Count - 1][2] + Px(6))
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
            if (x < 0 || y < 0 || x + Px(glyph.Width) > frame.Width || y + Px(glyph.Height) > frame.Height) return -1;
            int hit = 0, total = glyph.Width * glyph.Height;
            for (int yy = 0; yy < glyph.Height; yy++)
                for (int xx = 0; xx < glyph.Width; xx++)
                    if (ink(x + Px(xx), y + Px(yy)) == glyph.On[yy * glyph.Width + xx]) hit++;
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
            // Only the pure yellow stack digit. Warm parchment must not become part of the number.
            return r >= 235 && g >= 235 && b <= 40;
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
            if (titleTemplate == null || frame == null || panel.Width < Px(170)) return false;
            long sad = 0;
            int n = 0;
            for (int y = 0; y < titleTemplate.Height; y++)
                for (int x = 0; x < titleTemplate.Width; x++)
                {
                    int r1, g1, b1, r2, g2, b2;
                    frame.Get(panel.X + Px(10 + x), panel.Y + Px(4 + y), out r1, out g1, out b1);
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
            if (frame == null || panel.Width < Px(200)) return -1;
            int strict = ReadCharge(frame, panel, false);
            if (strict >= 0) return strict;
            if (SpentReroll(frame, panel)) return 0;
            return ReadCharge(frame, panel, true);
        }

        static int ReadCharge(Frame frame, Rectangle panel, bool hover)
        {
            int paren = -1;
            for (int x = panel.X + Px(255); x < panel.X + Px(278); x++)
            {
                int ink = 0;
                for (int y = panel.Y + Px(200); y < panel.Y + Px(216); y++)
                    if (ButtonInk(frame, x, y, hover)) ink++;
                if (ink < Px(6)) continue;
                int left = 0;
                for (int y = panel.Y + Px(200); y < panel.Y + Px(216); y++)
                    if (ButtonInk(frame, x - Px(1), y, hover)) left++;
                if (left < Px(3)) paren = x;
            }
            if (paren < 0) return SpentReroll(frame, panel) ? 0 : -1;
            int best = -1;
            double bestRatio = 0;
            double others = 0;
            double two = 0;
            int[] shifts = new int[] { -2, -1, 0, 1, 2 };
            int[] vshifts = new int[] { -1, 0, 1 };
            for (int digit = 0; digit < ChargeShapes.Length; digit++)
            {
                string[] rows = ChargeShapes[digit];
                for (int si = 0; si < shifts.Length; si++)
                    for (int vi = 0; vi < vshifts.Length; vi++)
                    {
                        int inkWidth;
                        double ratio = ScoreCharge(frame, paren - Px(8) + Px(shifts[si]), panel.Y + Px(203) + Px(vshifts[vi]), rows, hover, out inkWidth);
                        if (digit == 1)
                        {
                            if (inkWidth < 1 || inkWidth > 3) continue;
                        }
                        else if (inkWidth < 4) continue;
                        if (ratio > bestRatio)
                        {
                            bestRatio = ratio;
                            best = digit;
                        }
                        if (digit == 2)
                        {
                            if (ratio > two) two = ratio;
                        }
                        else if ((digit == 1 || digit == 3 || digit == 4 || digit == 5) && ratio > others) others = ratio;
                    }
            }
            if (best == 1 || best == 3 || best == 4 || best == 5 || best == 0)
            {
                if (bestRatio >= 0.82) return best;
            }
            else if (best == 2 && bestRatio >= 0.82) return 2;
            // (2) is the only count whose serif varies. A wide digit with a flat base is a 2
            // when 1, 3, 4 and 5 did not match.
            if (paren >= 0 && others < 0.78 && (two >= 0.65 || LooksLikeTwo(frame, panel, paren, hover))) return 2;
            return -1;
        }

        static bool LooksLikeTwo(Frame frame, Rectangle panel, int paren, bool hover)
        {
            int leftParen = -1;
            int from = paren - Px(16);
            int to = paren - Px(8);
            for (int x = from; x <= to; x++)
            {
                int ink = 0;
                for (int y = panel.Y + Px(200); y < panel.Y + Px(216); y++)
                    if (ButtonInk(frame, x, y, hover)) ink++;
                if (ink >= Px(6)) leftParen = x;
            }
            if (leftParen < 0) return false;
            int x0 = leftParen + Px(1);
            int x1 = paren - Px(1);
            int y0 = panel.Y + Px(202);
            int y1 = panel.Y + Px(212);
            if (x1 < x0) return false;
            int inkPixels = 0;
            int minX = 99999, maxX = -1, minY = 99999, maxY = -1;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (!ButtonInk(frame, x, y, hover)) continue;
                    inkPixels++;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            if (inkPixels == 0 || maxY < 0) return false;
            int inkWidth = maxX - minX + 1;
            int bottomInk = 0;
            int bottom = maxY - Px(1);
            if (bottom < minY) bottom = minY;
            for (int y = bottom; y <= maxY; y++)
                for (int x = x0; x <= x1; x++)
                    if (ButtonInk(frame, x, y, hover)) bottomInk++;
            return inkWidth >= Px(4) && inkWidth <= Px(7) && inkPixels >= Px(9) && inkPixels <= Px(28) && bottomInk >= Px(3);
        }

        // Ink overlap, not background dots. A skinny 1 must not win just because most cells are empty.
        static double ScoreCharge(Frame frame, int originX, int originY, string[] rows, bool hover, out int inkWidth)
        {
            int tp = 0, fp = 0, fn = 0, hit = 0, total = 0;
            int minC = 99, maxC = -1;
            for (int y = 0; y < rows.Length; y++)
            {
                string row = rows[y];
                for (int x = 0; x < row.Length; x++)
                {
                    bool on = ButtonInk(frame, originX + Px(x), originY + Px(y), hover);
                    bool want = row[x] == '#';
                    total++;
                    if (on == want) hit++;
                    if (on && want) tp++;
                    else if (on) fp++;
                    else if (want) fn++;
                    if (!on) continue;
                    if (x < minC) minC = x;
                    if (x > maxC) maxC = x;
                }
            }
            inkWidth = maxC >= 0 ? maxC - minC + 1 : 0;
            double accuracy = total == 0 ? 0 : hit / (double)total;
            int denom = 2 * tp + fp + fn;
            double overlap = denom == 0 ? 0 : (2.0 * tp) / denom;
            return (overlap + accuracy) / 2.0;
        }

        // The last reroll greys the button out. It then reads REROLL REWARD with no (n).
        static bool SpentReroll(Frame frame, Rectangle panel)
        {
            int grey = 0, samples = 0;
            int y = panel.Y + Px(210);
            int step = Math.Max(1, Px(2));
            for (int x = panel.X + Px(110); x <= panel.X + Px(130); x += step)
            {
                samples++;
                int r, g, b;
                frame.Get(x, y, out r, out g, out b);
                if (r >= 110 && r <= 155 && g >= 100 && g <= 145 && b >= 75 && b <= 125 && r > g && g > b) grey++;
            }
            for (int x = panel.X + Px(260); x <= panel.X + Px(290); x += step)
            {
                samples++;
                int r, g, b;
                frame.Get(x, y, out r, out g, out b);
                if (r >= 110 && r <= 155 && g >= 100 && g <= 145 && b >= 75 && b <= 125 && r > g && g > b) grey++;
            }
            if (samples == 0 || grey * 2 < samples) return false;
            int white = 0;
            int top = panel.Y + Px(203), bottom = panel.Y + Px(211);
            int left = panel.X + Px(137), right = panel.X + Px(254);
            for (int yy = top; yy <= bottom; yy++)
                for (int xx = left; xx <= right; xx++)
                {
                    int r, g, b;
                    frame.Get(xx, yy, out r, out g, out b);
                    if (r > 220 && g > 220 && b > 220) white++;
                }
            return white >= Px(100);
        }

        static bool ButtonInk(Frame frame, int x, int y, bool hover)
        {
            int r, g, b;
            frame.Get(x, y, out r, out g, out b);
            int sum = r + g + b;
            if (sum < 120 && r < 110 && g < 90) return true;
            if (!hover) return false;
            int br, bg, bb;
            frame.Get(x, Math.Max(0, y - 6), out br, out bg, out bb);
            int back = br + bg + bb;
            return sum < 210 && back > sum + 180 && r + 40 < br;
        }

        // 5-wide glyphs. 1 and 2 are the live 9-row button strokes. 1 is only two pixels wide.
        static readonly string[][] ChargeShapes = new string[][] {
            new string[] { ".##..", "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".##.." },
            new string[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".##..", ".##..", ".##.." },
            new string[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#....", "#...#", "#####" },
            new string[] { "###..", "...#.", "...#.", ".....", "..##.", "...##", "...##", "...#.", "###.." },
            new string[] { "..#..", "..#..", ".##..", "..#..", "..#..", "..#..", "####.", "..#..", ".##.." },
            new string[] { "####.", "#....", "#....", ".##..", "..##.", "...#.", "...#.", "...#.", "##..." }
        };
    }

    // opened: a new casket. The window just appeared, or it stayed open and the
    // items or value settled on something new without a one-step reroll.
    // replaced: REROLL REWARD (n) fell by exactly one on this same window.
    // closed: the window has been gone long enough that the next one is new.
    internal sealed class RewardSession
    {
        public int MissesBeforeClose = 8;
        public int CloseAfterMilliseconds = 450;
        public int RerollMemoryMilliseconds = 3000;
        public int CompletionMilliseconds = 300;
        public int RerollFixMilliseconds = 500;
        public bool Open;
        public System.Func<int> Clock;
        int misses;
        int missAt;
        int heldCharges = -1;
        int closedAt;
        bool haveClosedAt;
        int loggedAt;
        bool openedInPlace;
        bool rerollPending;
        string logged = "";
        string returnMark = "";
        int returnSeen;

        int Now()
        {
            if (Clock != null) return Clock();
            return Environment.TickCount;
        }

        int Elapsed(int start)
        {
            int elapsed = unchecked(Now() - start);
            if (elapsed < 0) return RerollMemoryMilliseconds;
            return elapsed;
        }

        void Remember(int charges, string mark, bool inPlace, bool opened)
        {
            // A new casket clears a stale count, even when this frame could not read n.
            if (opened) heldCharges = charges;
            else if (charges >= 0) heldCharges = charges;
            logged = mark ?? "";
            loggedAt = Now();
            openedInPlace = inPlace;
            rerollPending = false;
        }

        internal static bool SameReward(string logged, string mark)
        {
            int left = MarkValue(logged);
            int right = MarkValue(mark);
            if (left <= 0 || left != right) return false;
            return ItemKeys(logged) == ItemKeys(mark);
        }

        static string ItemKeys(string mark)
        {
            if (string.IsNullOrEmpty(mark)) return "";
            string[] parts = mark.Split('|');
            List<string> keys = new List<string>();
            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i];
                int cut = part.LastIndexOf('x');
                string key = part;
                if (cut > 0)
                {
                    bool digits = cut + 1 < part.Length;
                    for (int c = cut + 1; c < part.Length && digits; c++)
                        if (part[c] < '0' || part[c] > '9') digits = false;
                    if (digits) key = part.Substring(0, cut);
                }
                if (key.Length > 0) keys.Add(key);
            }
            keys.Sort(StringComparer.Ordinal);
            StringBuilder built = new StringBuilder();
            for (int i = 0; i < keys.Count; i++)
            {
                if (i > 0) built.Append('|');
                built.Append(keys[i]);
            }
            return built.ToString();
        }

        static int MarkValue(string mark)
        {
            if (string.IsNullOrEmpty(mark)) return 0;
            int bar = mark.IndexOf('|');
            string head = bar < 0 ? mark : mark.Substring(0, bar);
            int value;
            return int.TryParse(head, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out value) ? value : 0;
        }

        bool Dropped(int charges)
        {
            return heldCharges >= 0 && charges == heldCharges - 1;
        }

        public string See(bool visible, int charges, string mark)
        {
            if (!visible)
            {
                if (!Open) return "none";
                if (misses == 0) missAt = Now();
                misses++;
                int elapsed = Elapsed(missAt);
                // A hover or a bad grab blinks for a frame or two. That is not a close.
                if (misses >= MissesBeforeClose || elapsed >= CloseAfterMilliseconds)
                {
                    Open = false;
                    misses = 0;
                    closedAt = Now();
                    haveClosedAt = true;
                    return "closed";
                }
                return "none";
            }
            misses = 0;
            mark = mark ?? "";
            if (rerollPending && mark.Length > 0 && mark != logged)
            {
                Open = true;
                Remember(charges, mark, false, false);
                return "replaced";
            }
            if (Dropped(charges))
            {
                int since = haveClosedAt ? Elapsed(closedAt) : 0;
                bool live = Open || since <= RerollMemoryMilliseconds;
                if (live && (mark.Length == 0 || mark == logged))
                {
                    // The button changed before the new items were drawn.
                    if (openedInPlace && Elapsed(loggedAt) <= RerollFixMilliseconds)
                    {
                        Open = true;
                        Remember(charges, mark, false, false);
                        return "coalesce";
                    }
                    if (charges >= 0) heldCharges = charges;
                    rerollPending = true;
                    return "none";
                }
                if (live)
                {
                    Open = true;
                    Remember(charges, mark, false, false);
                    return "replaced";
                }
            }
            if (!Open)
            {
                // Same items and value, even if one stack digit flickered.
                if (SameReward(logged, mark) || (!string.IsNullOrEmpty(logged) && mark == logged))
                {
                    Open = true;
                    returnSeen = 0;
                    returnMark = "";
                    if (charges >= 0) heldCharges = charges;
                    return "none";
                }
                // A window uncovering the client can flash one bad frame. Wait for a second read.
                if (haveClosedAt && !string.IsNullOrEmpty(logged))
                {
                    if (mark == returnMark) returnSeen++;
                    else { returnMark = mark; returnSeen = 1; }
                    if (returnSeen < 2) return "none";
                    Open = true;
                    returnSeen = 0;
                    returnMark = "";
                    Remember(charges, mark, false, true);
                    return "opened";
                }
                Open = true;
                Remember(charges, mark, false, true);
                return "opened";
            }
            if (charges >= 0 && !rerollPending) heldCharges = charges;
            if (SameReward(logged, mark)) return "none";
            if (mark.Length == 0 || mark == logged) return "none";
            int loggedValue = MarkValue(logged);
            int nextValue = MarkValue(mark);
            bool sameValue = loggedValue > 0 && loggedValue == nextValue && Elapsed(loggedAt) <= CompletionMilliseconds;
            bool sameCount = charges < 0 || charges == heldCharges;
            if (sameValue && sameCount)
            {
                Remember(charges, mark, false, false);
                return "replaced";
            }
            Remember(charges, mark, true, true);
            return "opened";
        }

        public void Reset()
        {
            Open = false;
            misses = 0;
            missAt = 0;
            heldCharges = -1;
            closedAt = 0;
            haveClosedAt = false;
            loggedAt = 0;
            openedInPlace = false;
            rerollPending = false;
            logged = "";
            returnMark = "";
            returnSeen = 0;
        }
    }
}
