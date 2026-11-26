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
            return copy;
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
