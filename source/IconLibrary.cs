using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Chest
{
    internal sealed class IconRef
    {
        public string FileName, Name, Group;
        public int Width, Height;
        public byte[] Bgra;
        public bool Show = true;
        public readonly List<int> Samples = new List<int>();
    }

    internal sealed class IconHit
    {
        public string Name = "Unknown";
        public string Group = "";
        public double Score, Second;
        public bool Known;
    }

    internal static class IconLibrary
    {
        public static List<IconRef> Load(string iconDirectory, string catalogPath)
        {
            Dictionary<string, string[]> catalog = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(catalogPath))
            {
                foreach (string line in File.ReadAllLines(catalogPath))
                {
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    string[] parts = line.Split('|');
                    if (parts.Length >= 3) catalog[parts[0]] = parts;
                }
            }
            List<IconRef> icons = new List<IconRef>();
            if (!Directory.Exists(iconDirectory)) return icons;
            foreach (string path in Directory.GetFiles(iconDirectory, "*.png"))
            {
                string file = Path.GetFileName(path);
                Frame frame = FramePng.Decode(path);
                if (frame == null || frame.Width < 2) continue;
                IconRef icon = new IconRef();
                icon.FileName = file;
                icon.Bgra = frame.Bgra;
                icon.Width = frame.Width;
                icon.Height = frame.Height;
                string[] row;
                if (catalog.TryGetValue(file, out row))
                {
                    icon.Name = row[1];
                    icon.Group = row[2];
                    if (row.Length >= 4 && row[3] == "match") icon.Show = false;
                }
                else { icon.Name = Path.GetFileNameWithoutExtension(file); icon.Group = icon.Name; }
                BuildSamples(icon);
                icons.Add(icon);
            }
            return icons;
        }

        static void BuildSamples(IconRef icon)
        {
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    int sx = x * icon.Width / 32;
                    int sy = y * icon.Height / 32;
                    int i = (sy * icon.Width + sx) * 4;
                    if (icon.Bgra[i + 3] < 128) continue;
                    int r = icon.Bgra[i + 2], g = icon.Bgra[i + 1], b = icon.Bgra[i];
                    if (r > 210 && g > 175 && b < 70) continue;
                    icon.Samples.Add(x);
                    icon.Samples.Add(y);
                    icon.Samples.Add(icon.Bgra[i + 2]);
                    icon.Samples.Add(icon.Bgra[i + 1]);
                    icon.Samples.Add(icon.Bgra[i]);
                }
        }

        public static IconHit Match(List<IconRef> icons, byte[] crop, int width, int height, double minimum)
        {
            IconHit hit = new IconHit();
            if (icons == null || crop == null || width < 2) return hit;
            byte[] small = new byte[32 * 32 * 4];
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    int sx = Math.Min(width - 1, x * width / 32);
                    int sy = Math.Min(height - 1, y * height / 32);
                    int from = (sy * width + sx) * 4;
                    int to = (y * 32 + x) * 4;
                    Buffer.BlockCopy(crop, from, small, to, 4);
                    int r = small[to + 2], g = small[to + 1], b = small[to];
                    if (r > 210 && g > 175 && b < 70) small[to + 3] = 0;
                }
            foreach (IconRef icon in icons)
            {
                if (icon.Samples.Count < 40) continue;
                int sad = 0, n = 0;
                for (int s = 0; s < icon.Samples.Count; s += 5)
                {
                    int x = icon.Samples[s], y = icon.Samples[s + 1];
                    int i = (y * 32 + x) * 4;
                    if (small[i + 3] < 128) continue;
                    sad += Math.Abs(small[i + 2] - icon.Samples[s + 2]) + Math.Abs(small[i + 1] - icon.Samples[s + 3]) + Math.Abs(small[i] - icon.Samples[s + 4]);
                    n++;
                }
                if (n < 12) continue;
                double score = 1.0 - sad / (n * 765.0);
                string group = string.IsNullOrEmpty(icon.Group) ? icon.Name : icon.Group;
                string best = string.IsNullOrEmpty(hit.Group) ? hit.Name : hit.Group;
                if (score > hit.Score)
                {
                    if (!string.IsNullOrEmpty(best) && best != group) hit.Second = hit.Score;
                    hit.Score = score;
                    hit.Name = icon.Name;
                    hit.Group = icon.Group;
                }
                else if (group != best && score > hit.Second) hit.Second = score;
            }
            hit.Known = hit.Score >= minimum && hit.Score - hit.Second >= 0.03;
            if (!hit.Known) { hit.Name = "Unknown"; hit.Group = ""; }
            return hit;
        }

        public static string HashCrop(byte[] bgra, int width, int height)
        {
            ulong bits = 0;
            int n = 0;
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    int sx0 = x * width / 8, sx1 = (x + 1) * width / 8;
                    int sy0 = y * height / 8, sy1 = (y + 1) * height / 8;
                    if (sx1 <= sx0) sx1 = sx0 + 1;
                    if (sy1 <= sy0) sy1 = sy0 + 1;
                    int sum = 0, count = 0;
                    for (int yy = sy0; yy < sy1 && yy < height; yy++)
                        for (int xx = sx0; xx < sx1 && xx < width; xx++)
                        {
                            int i = (yy * width + xx) * 4;
                            int r = bgra[i + 2], g = bgra[i + 1], b = bgra[i];
                            if (r > 210 && g > 175 && b < 70) continue;
                            sum += r + g + b;
                            count++;
                        }
                    int avg = count == 0 ? 0 : sum / count;
                    if (x < 7)
                    {
                        int sx = Math.Min(width - 1, (x + 1) * width / 8);
                        int sy = Math.Min(height - 1, y * height / 8);
                        int j = (sy * width + sx) * 4;
                        int right = bgra[j + 2] + bgra[j + 1] + bgra[j];
                        if (avg > right) bits |= 1UL << n;
                        n++;
                    }
                }
            return bits.ToString("x16");
        }
    }

    internal static class FramePng
    {
        public static Frame Decode(string path)
        {
            try
            {
                using (Bitmap source = new Bitmap(path))
                    return FromBitmap(source);
            }
            catch (Exception ex) { CrashLog.Write(ex); return null; }
        }

        public static Frame FromBitmap(Bitmap source)
        {
            int w = source.Width, h = source.Height;
            using (Bitmap copy = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(copy))
                    g.DrawImage(source, new Rectangle(0, 0, w, h), new Rectangle(0, 0, w, h), GraphicsUnit.Pixel);
                BitmapData data = copy.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    byte[] bgra = new byte[w * h * 4];
                    int stride = data.Stride;
                    if (stride == w * 4) System.Runtime.InteropServices.Marshal.Copy(data.Scan0, bgra, 0, bgra.Length);
                    else
                    {
                        byte[] row = new byte[stride];
                        for (int y = 0; y < h; y++)
                        {
                            System.Runtime.InteropServices.Marshal.Copy(IntPtr.Add(data.Scan0, y * stride), row, 0, stride);
                            Buffer.BlockCopy(row, 0, bgra, y * w * 4, w * 4);
                        }
                    }
                    return new Frame(bgra, w, h);
                }
                finally { copy.UnlockBits(data); }
            }
        }

        public static void Save(Frame frame, string path)
        {
            if (frame == null) return;
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            using (Bitmap bmp = new Bitmap(frame.Width, frame.Height, PixelFormat.Format32bppArgb))
            {
                BitmapData data = bmp.LockBits(new Rectangle(0, 0, frame.Width, frame.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int stride = data.Stride;
                    if (stride == frame.Width * 4) System.Runtime.InteropServices.Marshal.Copy(frame.Bgra, 0, data.Scan0, frame.Bgra.Length);
                    else
                    {
                        for (int y = 0; y < frame.Height; y++)
                            System.Runtime.InteropServices.Marshal.Copy(frame.Bgra, y * frame.Width * 4, IntPtr.Add(data.Scan0, y * stride), frame.Width * 4);
                    }
                }
                finally { bmp.UnlockBits(data); }
                bmp.Save(path, ImageFormat.Png);
            }
        }
    }
}
