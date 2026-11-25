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
            return paper > 80 && paper > ink;
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

        static bool IsPaper(int r, int g, int b)
        {
            if (r < 165 || g < 140 || b < 95) return false;
            if (b > 185) return false;
            if (r - b < 28) return false;
            if (g - b < 18) return false;
            if (r - g > 55) return false;
            return true;
        }
    }
}
