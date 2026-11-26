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
