using System;

namespace Chest
{
    // A noted reward is the same item drawn on a tan certificate.
    // The paper is removed so the item matches and displays unnoted.
    internal static class Noted
    {
        public static bool LooksNoted(byte[] bgra, int width, int height)
        {
            if (bgra == null || width < 8 || height < 8) return false;
            int paper = 0, ink = 0;
            int n = width * height;
            for (int i = 0; i < n; i++)
            {
                int o = i * 4;
                if (bgra[o + 3] < 128) continue;
                int b = bgra[o], g = bgra[o + 1], r = bgra[o + 2];
                if (IsPaper(r, g, b)) paper++;
                else if (r + g + b > 90) ink++;
            }
            return paper > 30 && paper > ink;
        }

        public static byte[] WithoutPaper(byte[] bgra, int width, int height)
        {
            byte[] copy = new byte[bgra.Length];
            Buffer.BlockCopy(bgra, 0, copy, 0, bgra.Length);
            int n = width * height;
            for (int i = 0; i < n; i++)
            {
                int o = i * 4;
                if (copy[o + 3] < 128) continue;
                int b = copy[o], g = copy[o + 1], r = copy[o + 2];
                if (IsPaper(r, g, b)) copy[o + 3] = 0;
            }
            return copy;
        }

        // Drops the flat slot colour and the certificate. Dark item pixels stay.
        // A sprite that is already transparent is left as it is.
        // A noted slot whose parchment was already removed still has the dark
        // certificate outline around a smaller item. Returns that item, scaled up.
        public static byte[] InnerIcon(byte[] bgra, int width, int height, out int outWidth, out int outHeight)
        {
            outWidth = 0;
            outHeight = 0;
            if (bgra == null || width < 24 || height < 24) return null;
            int n = width * height;
            bool[] frame = new bool[n];
            bool[] color = new bool[n];
            int frameCount = 0;
            for (int i = 0; i < n; i++)
            {
                int o = i * 4;
                if (bgra[o + 3] < 128) continue;
                int b = bgra[o], g = bgra[o + 1], r = bgra[o + 2];
                if (r + g + b < 55 || IsPaper(r, g, b) || IsCertificate(r, g, b))
                {
                    frame[i] = true;
                    frameCount++;
                }
                else color[i] = true;
            }
            if (frameCount < 80) return null;
            byte[] seen = new byte[n];
            int[] stack = new int[n];
            int best = 0, bx0 = 0, by0 = 0, bx1 = -1, by1 = -1;
            for (int start = 0; start < n; start++)
            {
                if (!color[start] || seen[start] != 0) continue;
                int sp = 0;
                stack[sp++] = start;
                seen[start] = 1;
                int count = 0, x0 = width, y0 = height, x1 = -1, y1 = -1;
                while (sp > 0)
                {
                    int i = stack[--sp];
                    int x = i % width, y = i / width;
                    count++;
                    if (x < x0) x0 = x;
                    if (y < y0) y0 = y;
                    if (x > x1) x1 = x;
                    if (y > y1) y1 = y;
                    PushColor(color, seen, stack, ref sp, width, height, x + 1, y);
                    PushColor(color, seen, stack, ref sp, width, height, x - 1, y);
                    PushColor(color, seen, stack, ref sp, width, height, x, y + 1);
                    PushColor(color, seen, stack, ref sp, width, height, x, y - 1);
                }
                if (count > best)
                {
                    best = count;
                    bx0 = x0;
                    by0 = y0;
                    bx1 = x1;
                    by1 = y1;
                }
            }
            if (best < 12 || bx1 < 0) return null;
            if (bx0 < 2 || by0 < 2 || bx1 > width - 3 || by1 > height - 3) return null;
            int left = 0, right = 0, top = 0, bottom = 0;
            for (int i = 0; i < n; i++)
            {
                if (!frame[i]) continue;
                int x = i % width, y = i / width;
                if (x < bx0) left++;
                else if (x > bx1) right++;
                if (y < by0) top++;
                else if (y > by1) bottom++;
            }
            if (left < 6 || right < 6 || top < 6 || bottom < 6) return null;
            int cw = bx1 - bx0 + 1, ch = by1 - by0 + 1;
            if (cw * ch * 10 > width * height * 7) return null;
            int side = 32;
            byte[] scaled = new byte[side * side * 4];
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                {
                    int sx = bx0 + x * cw / side;
                    int sy = by0 + y * ch / side;
                    if (sx > bx1) sx = bx1;
                    if (sy > by1) sy = by1;
                    int fromIndex = sy * width + sx;
                    if (!color[fromIndex]) continue;
                    int from = fromIndex * 4;
                    int to = (y * side + x) * 4;
                    scaled[to] = bgra[from];
                    scaled[to + 1] = bgra[from + 1];
                    scaled[to + 2] = bgra[from + 2];
                    scaled[to + 3] = 255;
                }
            outWidth = side;
            outHeight = side;
            return scaled;
        }

        static void PushColor(bool[] color, byte[] seen, int[] stack, ref int sp, int width, int height, int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return;
            int i = y * width + x;
            if (seen[i] != 0 || !color[i]) return;
            seen[i] = 1;
            stack[sp++] = i;
        }

        public static byte[] CleanForDisplay(byte[] bgra, int width, int height)
        {
            if (bgra == null || width < 2 || height < 2) return bgra;
            byte[] copy = new byte[bgra.Length];
            Buffer.BlockCopy(bgra, 0, copy, 0, bgra.Length);
            int n = width * height;
            int clear = 0;
            for (int i = 0; i < n; i++)
                if (copy[i * 4 + 3] < 128) clear++;
            if (clear * 5 > n) return copy;
            int slotR = 0, slotG = 0, slotB = 0, slotN = 0;
            // The slot is the most common edge colour that is not pure black.
            int[] count = new int[32 * 32 * 32];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (x >= 5 && y >= 5 && x < width - 5 && y < height - 5) continue;
                    int o = (y * width + x) * 4;
                    if (copy[o + 3] < 128) continue;
                    int b = copy[o], g = copy[o + 1], r = copy[o + 2];
                    if (r + g + b < 24) continue;
                    int key = (r / 8) + (g / 8) * 32 + (b / 8) * 1024;
                    count[key]++;
                }
            for (int i = 0; i < count.Length; i++)
            {
                if (count[i] <= slotN) continue;
                slotN = count[i];
                slotR = (i % 32) * 8 + 4;
                slotG = ((i / 32) % 32) * 8 + 4;
                slotB = (i / 1024) * 8 + 4;
            }
            bool[] drop = new bool[n];
            int[] queue = new int[n];
            int qh = 0, qt = 0;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (x != 0 && y != 0 && x != width - 1 && y != height - 1) continue;
                    int i = y * width + x;
                    if (IsBackdrop(copy, i * 4, slotR, slotG, slotB))
                    {
                        drop[i] = true;
                        queue[qt++] = i;
                    }
                }
            while (qh < qt)
            {
                int i = queue[qh++];
                int x = i % width, y = i / width;
                Spread(copy, drop, queue, ref qt, width, height, x + 1, y, slotR, slotG, slotB);
                Spread(copy, drop, queue, ref qt, width, height, x - 1, y, slotR, slotG, slotB);
                Spread(copy, drop, queue, ref qt, width, height, x, y + 1, slotR, slotG, slotB);
                Spread(copy, drop, queue, ref qt, width, height, x, y - 1, slotR, slotG, slotB);
            }
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (x >= 2 && y >= 2 && x < width - 2 && y < height - 2) continue;
                    int o = (y * width + x) * 4;
                    if (copy[o] + copy[o + 1] + copy[o + 2] < 16) drop[y * width + x] = true;
                }
            for (int i = 0; i < n; i++)
                if (drop[i]) copy[i * 4 + 3] = 0;
            DropCornerDigit(copy, width, height);
            return copy;
        }

        // Only the small in-game stack digit. A gold item is larger than this box.
        static void DropCornerDigit(byte[] px, int width, int height)
        {
            int xLimit = Math.Max(6, width * 45 / 100);
            int yLimit = Math.Max(6, height * 45 / 100);
            int minX = width, minY = height, maxX = -1, maxY = -1, count = 0;
            for (int y = 0; y < yLimit; y++)
                for (int x = 0; x < xLimit; x++)
                {
                    int o = (y * width + x) * 4;
                    if (px[o + 3] < 128 || !StackYellow(px[o + 2], px[o + 1], px[o])) continue;
                    count++;
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            if (count < 4 || count > 80) return;
            if (maxX - minX > 14 || maxY - minY > 14) return;
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    int o = (y * width + x) * 4;
                    if (StackYellow(px[o + 2], px[o + 1], px[o])) px[o + 3] = 0;
                }
        }

        static void Spread(byte[] copy, bool[] drop, int[] queue, ref int qt, int width, int height, int x, int y, int sr, int sg, int sb)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return;
            int i = y * width + x;
            if (drop[i]) return;
            if (!IsBackdrop(copy, i * 4, sr, sg, sb)) return;
            drop[i] = true;
            queue[qt++] = i;
        }

        static bool IsBackdrop(byte[] px, int o, int sr, int sg, int sb)
        {
            if (px[o + 3] < 40) return true;
            int b = px[o], g = px[o + 1], r = px[o + 2];
            if (IsPaper(r, g, b) || IsCertificate(r, g, b)) return true;
            if (sr + sg + sb < 24) return false;
            return Math.Abs(r - sr) + Math.Abs(g - sg) + Math.Abs(b - sb) <= 22;
        }

        // A noted certificate is a warm tan, including the darker shade around the icon.
        static bool IsCertificate(int r, int g, int b)
        {
            if (r < 125 || g < 100 || b < 55) return false;
            if (b > 185) return false;
            if (r + g + b < 300) return false;
            if (r - b < 16) return false;
            if (g - b < 6) return false;
            if (r - g > 70) return false;
            return true;
        }

        static bool StackYellow(int r, int g, int b)
        {
            return r > 210 && g > 175 && b < 70 && r - b > 140;
        }

        static bool IsPaper(int r, int g, int b)
        {
            if (r < 145 || g < 120 || b < 70) return false;
            if (b > 190) return false;
            if (r - b < 24) return false;
            if (g - b < 12) return false;
            if (r - g > 70) return false;
            return true;
        }
    }
}
