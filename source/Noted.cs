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

        // Drops the certificate and the flat slot colour so a crop can be shown
        // only when no clean sprite exists. Named sprites are never this image.
        public static byte[] CleanForDisplay(byte[] bgra, int width, int height)
        {
            if (bgra == null || width < 2 || height < 2) return bgra;
            byte[] copy = new byte[bgra.Length];
            Buffer.BlockCopy(bgra, 0, copy, 0, bgra.Length);
            int br = 0, bgg = 0, bb = 0, samples = 0;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (x > 1 && y > 1 && x < width - 2 && y < height - 2) continue;
                    int o = (y * width + x) * 4;
                    if (copy[o + 3] < 128) continue;
                    bb += copy[o]; bgg += copy[o + 1]; br += copy[o + 2];
                    samples++;
                }
            if (samples < 1) return copy;
            br /= samples; bgg /= samples; bb /= samples;
            int n = width * height;
            bool[] drop = new bool[n];
            int[] queue = new int[n];
            int qh = 0, qt = 0;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (x != 0 && y != 0 && x != width - 1 && y != height - 1) continue;
                    int i = y * width + x;
                    if (NearCorner(copy, i * 4, br, bgg, bb))
                    {
                        drop[i] = true;
                        queue[qt++] = i;
                    }
                }
            while (qh < qt)
            {
                int i = queue[qh++];
                int x = i % width, y = i / width;
                Spread(copy, drop, queue, ref qt, width, height, x + 1, y, br, bgg, bb);
                Spread(copy, drop, queue, ref qt, width, height, x - 1, y, br, bgg, bb);
                Spread(copy, drop, queue, ref qt, width, height, x, y + 1, br, bgg, bb);
                Spread(copy, drop, queue, ref qt, width, height, x, y - 1, br, bgg, bb);
            }
            for (int i = 0; i < n; i++)
                if (drop[i]) copy[i * 4 + 3] = 0;
            PeelHalo(copy, width, height, br, bgg, bb);
            StripCornerStack(copy, width, height);
            return copy;
        }

        // A one-pixel rim of the slot colour can survive the flood. Peel only
        // dull pixels that still match that colour, and only two pixels deep.
        static void PeelHalo(byte[] px, int width, int height, int br, int bg, int bb)
        {
            int n = width * height;
            for (int layer = 0; layer < 2; layer++)
            {
                bool[] mark = new bool[n];
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        int i = y * width + x;
                        int o = i * 4;
                        if (px[o + 3] < 40) continue;
                        if (!TouchesClear(px, width, height, x, y)) continue;
                        int b = px[o], g = px[o + 1], r = px[o + 2];
                        int chroma = Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
                        if (chroma > 22) continue;
                        if (Math.Abs(r - br) + Math.Abs(g - bg) + Math.Abs(b - bb) > 36) continue;
                        mark[i] = true;
                    }
                for (int i = 0; i < n; i++)
                    if (mark[i]) px[i * 4 + 3] = 0;
            }
        }

        static bool TouchesClear(byte[] px, int width, int height, int x, int y)
        {
            if (Clear(px, width, height, x - 1, y)) return true;
            if (Clear(px, width, height, x + 1, y)) return true;
            if (Clear(px, width, height, x, y - 1)) return true;
            if (Clear(px, width, height, x, y + 1)) return true;
            return false;
        }

        static bool Clear(byte[] px, int width, int height, int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return true;
            return px[(y * width + x) * 4 + 3] < 40;
        }

        // The in-game stack digit sits in the top-left of a crop. Remove that
        // small yellow cluster and its dark shadow. A large gold item is left.
        static void StripCornerStack(byte[] px, int width, int height)
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
            if (count < 4 || count > 90) return;
            if (maxX - minX > 16 || maxY - minY > 16) return;
            int x0 = Math.Max(0, minX - 2), y0 = Math.Max(0, minY - 2);
            int x1 = Math.Min(width - 1, maxX + 2), y1 = Math.Min(height - 1, maxY + 2);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int o = (y * width + x) * 4;
                    if (px[o + 3] < 40) continue;
                    int b = px[o], g = px[o + 1], r = px[o + 2];
                    if (StackYellow(r, g, b) || r + g + b < 96) px[o + 3] = 0;
                }
        }

        static bool StackYellow(int r, int g, int b)
        {
            return r > 210 && g > 175 && b < 70 && r - b > 140;
        }

        static void Spread(byte[] copy, bool[] drop, int[] queue, ref int qt, int width, int height, int x, int y, int br, int bg, int bb)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return;
            int i = y * width + x;
            if (drop[i]) return;
            if (!NearCorner(copy, i * 4, br, bg, bb)) return;
            drop[i] = true;
            queue[qt++] = i;
        }

        static bool NearCorner(byte[] px, int o, int br, int bg, int bb)
        {
            if (px[o + 3] < 40) return true;
            int b = px[o], g = px[o + 1], r = px[o + 2];
            return Math.Abs(r - br) + Math.Abs(g - bg) + Math.Abs(b - bb) <= 28;
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
