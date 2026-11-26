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
        public byte[] Patch;
    }

    internal sealed class IconHit
    {
        public string Name = "Unknown";
        public string Group = "";
        public double Score, Second;
        public bool Known;
        public string Candidate = "";
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
                icon.Patch = Normalize(icon.Bgra, icon.Width, icon.Height);
                icons.Add(icon);
            }
            return icons;
        }

        // Fit the main shape into a square without stretching it sideways.
        // A wide pair of glasses stays wide, so it lines up with the wiki sprite.
        public static byte[] Normalize(byte[] src, int width, int height)
        {
            int n = 32;
            byte[] patch = new byte[n * n * 4];
            if (src == null || width < 2 || height < 2) return patch;
            int minX, minY, maxX, maxY;
            if (!MainShape(src, width, height, out minX, out minY, out maxX, out maxY)) return patch;
            int bw = maxX - minX + 1, bh = maxY - minY + 1;
            int side = bw > bh ? bw : bh;
            if (side < 1) side = 1;
            int nw = bw * n / side;
            int nh = bh * n / side;
            if (nw < 1) nw = 1;
            if (nh < 1) nh = 1;
            int ox = (n - nw) / 2, oy = (n - nh) / 2;
            for (int y = 0; y < nh; y++)
                for (int x = 0; x < nw; x++)
                {
                    int sx = minX + x * bw / nw;
                    int sy = minY + y * bh / nh;
                    if (sx > maxX) sx = maxX;
                    if (sy > maxY) sy = maxY;
                    int from = (sy * width + sx) * 4;
                    int to = ((y + oy) * n + (x + ox)) * 4;
                    patch[to] = src[from];
                    patch[to + 1] = src[from + 1];
                    patch[to + 2] = src[from + 2];
                    patch[to + 3] = src[from + 3];
                }
            return patch;
        }

        static bool MainShape(byte[] src, int width, int height, out int minX, out int minY, out int maxX, out int maxY)
        {
            minX = 0; minY = 0; maxX = -1; maxY = -1;
            int n = width * height;
            byte[] seen = new byte[n];
            int[] stack = new int[n];
            int best = 0, bestX0 = 0, bestY0 = 0, bestX1 = -1, bestY1 = -1;
            for (int start = 0; start < n; start++)
            {
                if (seen[start] != 0 || !Ink(src, start * 4)) continue;
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
                    PushInk(src, seen, stack, ref sp, width, height, x + 1, y);
                    PushInk(src, seen, stack, ref sp, width, height, x - 1, y);
                    PushInk(src, seen, stack, ref sp, width, height, x, y + 1);
                    PushInk(src, seen, stack, ref sp, width, height, x, y - 1);
                }
                if (count > best)
                {
                    best = count;
                    bestX0 = x0; bestY0 = y0; bestX1 = x1; bestY1 = y1;
                }
            }
            if (best < 8) return false;
            minX = bestX0; minY = bestY0; maxX = bestX1; maxY = bestY1;
            return true;
        }

        static void PushInk(byte[] src, byte[] seen, int[] stack, ref int sp, int width, int height, int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return;
            int i = y * width + x;
            if (seen[i] != 0 || !Ink(src, i * 4)) return;
            seen[i] = 1;
            stack[sp++] = i;
        }

        static bool Ink(byte[] src, int o)
        {
            if (src[o + 3] < 128) return false;
            int b = src[o], g = src[o + 1], r = src[o + 2];
            if (r + g + b < 12) return false;
            if (r > 210 && g > 175 && b < 70) return false;
            return true;
        }

        public static IconHit Match(List<IconRef> icons, byte[] crop, int width, int height, double minimum)
        {
            IconHit hit = new IconHit();
            if (icons == null || crop == null || width < 2) return hit;
            byte[] prepared = Noted.CleanForDisplay(crop, width, height);
            byte[] query = Normalize(prepared, width, height);
            foreach (IconRef icon in icons)
            {
                if (icon.Patch == null) continue;
                int sad = 0, n = 0;
                int pixels = query.Length / 4;
                for (int p = 0; p < pixels; p++)
                {
                    int i = p * 4;
                    if (icon.Patch[i + 3] < 128 || query[i + 3] < 128) continue;
                    sad += Math.Abs(query[i + 2] - icon.Patch[i + 2]) + Math.Abs(query[i + 1] - icon.Patch[i + 1]) + Math.Abs(query[i] - icon.Patch[i]);
                    n++;
                }
                if (n < 16) continue;
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
            double margin = hit.Score - hit.Second;
            hit.Known = hit.Score >= minimum && margin >= 0.03;
            if (!hit.Known && hit.Score >= minimum && hit.Score >= 0.945 && margin >= 0.012) hit.Known = true;
            hit.Candidate = hit.Name;
            if (!hit.Known) { hit.Name = "Unknown"; hit.Group = ""; }
            return hit;
        }

        public static byte[] Signature(byte[] bgra, int width, int height)
        {
            byte[] sig = new byte[8 * 8 * 3];
            int[] weight = new int[64];
            if (bgra == null || width < 2 || height < 2) return sig;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int o = (y * width + x) * 4;
                    if (bgra[o + 3] < 128) continue;
                    int sx = x * 8 / width;
                    int sy = y * 8 / height;
                    if (sx > 7) sx = 7;
                    if (sy > 7) sy = 7;
                    int cell = sy * 8 + sx;
                    int i = cell * 3;
                    sig[i] = (byte)((sig[i] * weight[cell] + bgra[o + 2]) / (weight[cell] + 1));
                    sig[i + 1] = (byte)((sig[i + 1] * weight[cell] + bgra[o + 1]) / (weight[cell] + 1));
                    sig[i + 2] = (byte)((sig[i + 2] * weight[cell] + bgra[o]) / (weight[cell] + 1));
                    weight[cell]++;
                }
            return sig;
        }

        public static bool SameLook(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length < 192 || right.Length < 192) return false;
            int sad = 0, n = 0;
            for (int i = 0; i < 192; i += 3)
            {
                int l = left[i] + left[i + 1] + left[i + 2];
                int r = right[i] + right[i + 1] + right[i + 2];
                if (l < 18 && r < 18) continue;
                sad += Math.Abs(left[i] - right[i]) + Math.Abs(left[i + 1] - right[i + 1]) + Math.Abs(left[i + 2] - right[i + 2]);
                n++;
            }
            return n >= 4 && sad < n * 48;
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

        // A saved session from an older build may name an icon that is no longer
        // at that key. Match the name or its slug before giving up.
        public static IconRef Resolve(List<IconRef> icons, string id, string name)
        {
            if (icons == null) return null;
            string slugId = Slug(id);
            string slugName = Slug(name);
            IconRef spare = null;
            foreach (IconRef icon in icons)
            {
                if (!SameIcon(icon, id, name, slugId, slugName)) continue;
                if (icon.Show) return icon;
                if (spare == null) spare = icon;
            }
            return spare;
        }

        static bool SameIcon(IconRef icon, string id, string name, string slugId, string slugName)
        {
            string file = icon.FileName ?? "";
            if (file.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) file = file.Substring(0, file.Length - 4);
            string group = string.IsNullOrEmpty(icon.Group) ? icon.Name : icon.Group;
            if (!string.IsNullOrEmpty(id) && (group == id || icon.Name == id || file == id || Slug(group) == slugId || Slug(icon.Name) == slugId || file == slugId))
                return true;
            if (!string.IsNullOrEmpty(name) && name != "Unknown" && (icon.Name == name || Slug(icon.Name) == slugName || file == slugName || Slug(group) == slugName))
                return true;
            return false;
        }

        public static string Slug(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            System.Text.StringBuilder built = new System.Text.StringBuilder();
            bool dash = false;
            foreach (char ch in text.Trim().ToLowerInvariant())
            {
                bool keep = (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9');
                if (keep) { built.Append(ch); dash = false; }
                else if (!dash && built.Length > 0) { built.Append('-'); dash = true; }
            }
            if (dash && built.Length > 0) built.Length -= 1;
            return built.ToString();
        }

        // A gold mark so a slot is never an empty number after an upgrade.
        public static Frame Placeholder()
        {
            int n = 32;
            byte[] px = new byte[n * n * 4];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int dx = x - 15, dy = y - 15;
                    if (dx < 0) dx = -dx;
                    if (dy < 0) dy = -dy;
                    if (dx + dy > 13) continue;
                    int o = (y * n + x) * 4;
                    bool edge = dx + dy >= 11;
                    px[o] = edge ? (byte)40 : (byte)58;
                    px[o + 1] = edge ? (byte)166 : (byte)195;
                    px[o + 2] = edge ? (byte)201 : (byte)232;
                    px[o + 3] = 255;
                }
            return new Frame(px, n, n);
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

        public static byte[] Encode(Frame frame)
        {
            if (frame == null) return new byte[0];
            using (MemoryStream stream = new MemoryStream())
            using (Bitmap bmp = new Bitmap(frame.Width, frame.Height, PixelFormat.Format32bppArgb))
            {
                BitmapData data = bmp.LockBits(new Rectangle(0, 0, frame.Width, frame.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int stride = data.Stride;
                    if (stride == frame.Width * 4) System.Runtime.InteropServices.Marshal.Copy(frame.Bgra, 0, data.Scan0, frame.Bgra.Length);
                    else
                        for (int y = 0; y < frame.Height; y++)
                            System.Runtime.InteropServices.Marshal.Copy(frame.Bgra, y * frame.Width * 4, IntPtr.Add(data.Scan0, y * stride), frame.Width * 4);
                }
                finally { bmp.UnlockBits(data); }
                bmp.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
        }
    }
}
