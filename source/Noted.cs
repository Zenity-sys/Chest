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
            int br = 0, bg = 0, bb = 0, samples = 0;
            for (int y = 0; y < height; y += Math.Max(1, height / 6))
                for (int x = 0; x < width; x += Math.Max(1, width / 6))
                {
                    if (x > 3 && x < width - 4 && y > 3 && y < height - 4) continue;
                    int o = (y * width + x) * 4;
                    if (copy[o + 3] < 128) continue;
                    bb += copy[o]; bg += copy[o + 1]; br += copy[o + 2];
                    samples++;
                }
            if (samples > 0) { br /= samples; bg /= samples; bb /= samples; }
            for (int i = 0; i < width * height; i++)
            {
                int o = i * 4;
                if (copy[o + 3] < 128) continue;
                int b = copy[o], g = copy[o + 1], r = copy[o + 2];
                bool edge = Math.Abs(r - br) + Math.Abs(g - bg) + Math.Abs(b - bb) < 42;
                bool navy = r + g + b < 80 && b + 10 >= r;
                bool white = r > 225 && g > 225 && b > 225;
                if (IsPaper(r, g, b) || edge || navy || white) copy[o + 3] = 0;
            }
            return copy;
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
