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
            int n = width * height;
            bool[] drop = new bool[n];
            int[] queue = new int[n];
            int qh = 0, qt = 0;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (x != 0 && y != 0 && x != width - 1 && y != height - 1) continue;
                    int i = y * width + x;
                    if (SlotPixel(copy, i * 4, x, y, width, height))
                    {
                        drop[i] = true;
                        queue[qt++] = i;
                    }
                }
            while (qh < qt)
            {
                int i = queue[qh++];
                int x = i % width, y = i / width;
                Try(copy, drop, queue, ref qt, width, height, x + 1, y);
                Try(copy, drop, queue, ref qt, width, height, x - 1, y);
                Try(copy, drop, queue, ref qt, width, height, x, y + 1);
                Try(copy, drop, queue, ref qt, width, height, x, y - 1);
            }
            for (int i = 0; i < n; i++)
            {
                int o = i * 4;
                if (copy[o + 3] < 128) continue;
                int x = i % width, y = i / width;
                int b = copy[o], g = copy[o + 1], r = copy[o + 2];
                bool digit = y < height * 0.42 && x < width * 0.55 && r > 200 && g > 165 && b < 90;
                if (drop[i] || IsPaper(r, g, b) || digit || (r > 230 && g > 230 && b > 230)) copy[o + 3] = 0;
            }
            return copy;
        }

        static void Try(byte[] copy, bool[] drop, int[] queue, ref int qt, int width, int height, int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return;
            int i = y * width + x;
            if (drop[i]) return;
            if (!SlotPixel(copy, i * 4, x, y, width, height)) return;
            drop[i] = true;
            queue[qt++] = i;
        }

        static bool SlotPixel(byte[] px, int o, int x, int y, int width, int height)
        {
            if (px[o + 3] < 128) return true;
            int b = px[o], g = px[o + 1], r = px[o + 2];
            int max = r > g ? r : g; if (b > max) max = b;
            int min = r < g ? r : g; if (b < min) min = b;
            int sum = r + g + b;
            if (IsPaper(r, g, b)) return true;
            if (r > 230 && g > 230 && b > 230) return true;
            // The slot tile is a flat dark blue-grey, a little lighter than the panel.
            return max - min < 36 && sum < 150 && sum > 12;
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
