using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace Chest
{
    internal sealed class WikiHit
    {
        public string FileName = "";
        public string Name = "";
        public string WikiFile = "";
        public double Score, Second;
        public bool Confident;
    }

    // Names an item the bundled catalog missed. The index is a picture
    // descriptor built ahead of time, so a live read never waits on the wiki.
    internal static class WikiLookup
    {
        public const int Cells = 16;
        public const double MinimumScore = 0.955;
        public const double MinimumShape = 0.90;
        public const double MinimumMargin = 0.05;
        public const double AmbiguousScore = 0.90;

        public static string IndexPath = "";
        public static string IconDirectory = "";
        public static string SpriteDirectory = "";
        public static string CacheDirectory = "";
        public static bool AllowNetwork = true;
        public static ManualResetEvent TestPause;

        static readonly object loadGate = new object();
        static readonly object cacheGate = new object();
        static List<WikiEntry> entries;

        sealed class WikiEntry
        {
            public string FileName, Name, WikiFile;
            public byte[] Desc;
        }

        public static void UseAssets(string assetDirectory)
        {
            if (string.IsNullOrEmpty(assetDirectory)) return;
            IndexPath = Path.Combine(assetDirectory, "wiki-index.txt");
            IconDirectory = Path.Combine(assetDirectory, "icons");
            entries = null;
        }

        public static void Reset()
        {
            lock (loadGate) { entries = null; }
        }

        public static WikiHit Identify(byte[] bgra, int width, int height)
        {
            WikiHit hit = new WikiHit();
            List<WikiEntry> list = Load();
            if (list == null || list.Count == 0 || bgra == null || width < 2) return hit;
            byte[] desc = Describe(bgra, width, height);
            if (Ink(desc) < 6) return hit;
            double bestShape = 0;
            foreach (WikiEntry entry in list)
            {
                double shape, color;
                Measure(desc, entry.Desc, out shape, out color);
                if (color > hit.Score || (color == hit.Score && shape > bestShape))
                {
                    hit.Second = hit.Score;
                    hit.Score = color;
                    bestShape = shape;
                    hit.FileName = entry.FileName;
                    hit.Name = entry.Name;
                    hit.WikiFile = entry.WikiFile;
                }
                else if (color > hit.Second) hit.Second = color;
            }
            double margin = hit.Score - hit.Second;
            // Shape and colour both have to agree. A silhouette match in another colour stays Unknown.
            hit.Confident = bestShape >= MinimumShape && hit.Score >= MinimumScore && margin >= MinimumMargin && !string.IsNullOrEmpty(hit.Name);
            return hit;
        }

        // A stack (more than one in the slot) is never a piece of worn gear.
        public static bool AllowRename(int quantity, string name)
        {
            if (quantity > 1 && Gear(name)) return false;
            return true;
        }

        public static bool Gear(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string text = name.ToLowerInvariant();
            string[] parts = new string[] {
                "staff", "stave", "crossbow", "helm", "platebody", "platelegs", "plateskirt",
                "kiteshield", "amulet", "beret", "crozier", "tights", "stole"
            };
            for (int i = 0; i < parts.Length; i++)
                if (text.IndexOf(parts[i], StringComparison.Ordinal) >= 0) return true;
            if (text.IndexOf("bow", StringComparison.Ordinal) >= 0 && text.IndexOf("bowl", StringComparison.Ordinal) < 0) return true;
            return false;
        }

        // Returns at once. The callback runs later, off the caller thread.
        public static void ResolveAsync(byte[] bgra, int width, int height, Action<WikiHit, string> done)
        {
            if (bgra == null || width < 2 || done == null) return;
            byte[] copy = new byte[bgra.Length];
            Buffer.BlockCopy(bgra, 0, copy, 0, bgra.Length);
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    if (TestPause != null) TestPause.WaitOne(3000);
                    WikiHit hit = Identify(copy, width, height);
                    string path = null;
                    if (hit != null && hit.Confident) path = EnsureCached(hit);
                    done(hit, path);
                }
                catch (Exception ex)
                {
                    CrashLog.Write(ex);
                    try { done(null, null); } catch (Exception) { }
                }
            });
        }

        public static string EnsureCached(WikiHit hit)
        {
            if (hit == null || string.IsNullOrEmpty(hit.FileName) || string.IsNullOrEmpty(CacheDirectory)) return null;
            lock (cacheGate)
            {
                try
                {
                    Directory.CreateDirectory(CacheDirectory);
                    string cached = Path.Combine(CacheDirectory, SafeFile(hit.FileName));
                    if (IsPng(cached))
                    {
                        WriteMeta(cached, hit);
                        return cached;
                    }
                    string local = LocalSprite(hit);
                    if (!string.IsNullOrEmpty(local) && IsPng(local))
                    {
                        File.Copy(local, cached, true);
                        WriteMeta(cached, hit);
                        return cached;
                    }
                    if (!AllowNetwork || string.IsNullOrEmpty(hit.WikiFile)) return null;
                    string url = "https://runescape.wiki/wiki/Special:FilePath/" + Uri.EscapeDataString(hit.WikiFile.Replace(' ', '_'));
                    string temp = cached + ".part";
                    using (WebClient client = new WikiClient())
                    {
                        client.Headers["User-Agent"] = "Chest/0.6.7 (reward logger)";
                        client.DownloadFile(url, temp);
                    }
                    if (!IsPng(temp))
                    {
                        try { File.Delete(temp); } catch (Exception) { }
                        return null;
                    }
                    if (File.Exists(cached)) File.Delete(cached);
                    File.Move(temp, cached);
                    WriteMeta(cached, hit);
                    return cached;
                }
                catch (Exception ex)
                {
                    CrashLog.Write(ex);
                    return null;
                }
            }
        }

        public static string LocalSprite(WikiHit hit)
        {
            if (hit == null || string.IsNullOrEmpty(hit.FileName)) return null;
            string file = SafeFile(hit.FileName);
            string[] roots = new string[] { IconDirectory, SpriteDirectory };
            foreach (string root in roots)
            {
                if (string.IsNullOrEmpty(root)) continue;
                string path = Path.Combine(root, file);
                if (IsPng(path)) return path;
            }
            return null;
        }

        public static int WriteIndex(string iconDirectory, string catalogPath, string manifestPath, string outputPath)
        {
            List<WikiEntry> all = new List<WikiEntry>();
            Dictionary<string, int> seenFile = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(iconDirectory))
            {
                Dictionary<string, string[]> catalog = ReadCatalog(catalogPath);
                foreach (string path in Directory.GetFiles(iconDirectory, "*.png"))
                {
                    string file = Path.GetFileName(path);
                    string name = Path.GetFileNameWithoutExtension(file);
                    string wiki = name + ".png";
                    string[] row;
                    if (catalog.TryGetValue(file, out row))
                    {
                        if (row.Length > 1 && row[1].Length > 0) name = row[1];
                        wiki = name + ".png";
                    }
                    AddEntry(all, seenFile, path, file, name, wiki);
                }
            }
            if (!string.IsNullOrEmpty(manifestPath) && File.Exists(manifestPath))
            {
                string folder = Path.GetDirectoryName(manifestPath);
                foreach (string line in File.ReadAllLines(manifestPath))
                {
                    if (line.Length == 0 || line[0] == '#') continue;
                    string[] parts = line.Split('|');
                    if (parts.Length < 3) continue;
                    string file = parts[0].Trim();
                    string name = parts[1].Trim();
                    string wiki = parts[2].Trim();
                    string path = Path.Combine(folder, file);
                    AddEntry(all, seenFile, path, file, name, wiki);
                }
            }
            List<WikiEntry> unique = DropTwins(all);
            List<WikiEntry> kept = DropAmbiguous(unique);
            StringBuilder text = new StringBuilder();
            text.Append("# file|name|wiki|descriptor\r\n");
            foreach (WikiEntry entry in kept)
            {
                text.Append(entry.FileName).Append('|').Append(entry.Name.Replace("|", "/")).Append('|');
                text.Append(entry.WikiFile.Replace("|", "/")).Append('|').Append(Hex(entry.Desc)).Append("\r\n");
            }
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(outputPath, text.ToString(), new UTF8Encoding(false));
            return kept.Count;
        }

        static void AddEntry(List<WikiEntry> all, Dictionary<string, int> seenFile, string path, string file, string name, string wiki)
        {
            if (string.IsNullOrEmpty(file) || seenFile.ContainsKey(file)) return;
            if (!File.Exists(path)) return;
            Frame frame = FramePng.Decode(path);
            if (frame == null || frame.Width < 2) return;
            byte[] desc = Describe(frame.Bgra, frame.Width, frame.Height);
            if (Ink(desc) < 6) return;
            WikiEntry entry = new WikiEntry();
            entry.FileName = file;
            entry.Name = name;
            entry.WikiFile = wiki;
            entry.Desc = desc;
            seenFile[file] = all.Count;
            all.Add(entry);
        }

        static List<WikiEntry> DropTwins(List<WikiEntry> all)
        {
            Dictionary<string, List<WikiEntry>> groups = new Dictionary<string, List<WikiEntry>>();
            foreach (WikiEntry entry in all)
            {
                string key = Hex(entry.Desc);
                List<WikiEntry> group;
                if (!groups.TryGetValue(key, out group))
                {
                    group = new List<WikiEntry>();
                    groups[key] = group;
                }
                group.Add(entry);
            }
            List<WikiEntry> unique = new List<WikiEntry>();
            foreach (List<WikiEntry> group in groups.Values)
            {
                if (group.Count == 1)
                {
                    unique.Add(group[0]);
                    continue;
                }
                // Same picture under one item's names can stay. Two different items cannot.
                WikiEntry keep = group[0];
                bool same = true;
                for (int i = 1; i < group.Count; i++)
                {
                    if (!SameItem(keep.Name, group[i].Name)) { same = false; break; }
                    if (group[i].Name.Length < keep.Name.Length) keep = group[i];
                }
                if (same) unique.Add(keep);
            }
            return unique;
        }

        static bool SameItem(string left, string right)
        {
            if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase)) return true;
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right)) return false;
            if (right.StartsWith(left + " (", StringComparison.OrdinalIgnoreCase)) return true;
            if (left.StartsWith(right + " (", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static List<WikiEntry> DropAmbiguous(List<WikiEntry> unique)
        {
            bool[] drop = new bool[unique.Count];
            for (int i = 0; i < unique.Count; i++)
            {
                for (int j = i + 1; j < unique.Count; j++)
                {
                    if (!Close(unique[i].Desc, unique[j].Desc)) continue;
                    drop[i] = true;
                    drop[j] = true;
                }
            }
            List<WikiEntry> kept = new List<WikiEntry>();
            for (int i = 0; i < unique.Count; i++)
                if (!drop[i]) kept.Add(unique[i]);
            return kept;
        }

        static bool Close(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            int cells = left.Length / 3;
            int sad = 0, n = 0, seen = 0;
            for (int i = 0; i < left.Length; i += 3)
            {
                seen++;
                int l = left[i] + left[i + 1] + left[i + 2];
                int r = right[i] + right[i + 1] + right[i + 2];
                if (l >= 18 || r >= 18)
                {
                    sad += Math.Abs(left[i] - right[i]) + Math.Abs(left[i + 1] - right[i + 1]) + Math.Abs(left[i + 2] - right[i + 2]);
                    n++;
                }
                int remain = cells - seen;
                int maxN = n + remain;
                if (maxN < 6) return false;
                double bestPossible = 1.0 - sad / (maxN * 765.0);
                if (bestPossible < AmbiguousScore) return false;
            }
            if (n < 6) return false;
            return 1.0 - sad / (n * 765.0) >= AmbiguousScore;
        }

        public static byte[] Describe(byte[] bgra, int width, int height)
        {
            byte[] desc = new byte[Cells * Cells * 3];
            byte[] patch = IconLibrary.Normalize(bgra, width, height);
            int n = 32;
            int[] sumR = new int[Cells * Cells];
            int[] sumG = new int[Cells * Cells];
            int[] sumB = new int[Cells * Cells];
            int[] weight = new int[Cells * Cells];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int o = (y * n + x) * 4;
                    if (patch[o + 3] < 128) continue;
                    int cx = x * Cells / n;
                    int cy = y * Cells / n;
                    int cell = cy * Cells + cx;
                    sumR[cell] += patch[o + 2];
                    sumG[cell] += patch[o + 1];
                    sumB[cell] += patch[o];
                    weight[cell]++;
                }
            for (int cell = 0; cell < weight.Length; cell++)
            {
                if (weight[cell] == 0) continue;
                desc[cell * 3] = (byte)(sumR[cell] / weight[cell]);
                desc[cell * 3 + 1] = (byte)(sumG[cell] / weight[cell]);
                desc[cell * 3 + 2] = (byte)(sumB[cell] / weight[cell]);
            }
            return desc;
        }

        public static double Score(byte[] left, byte[] right)
        {
            double shape, color;
            Measure(left, right, out shape, out color);
            return color;
        }

        static void Measure(byte[] left, byte[] right, out double shape, out double color)
        {
            shape = 0;
            color = 0;
            if (left == null || right == null || left.Length != right.Length || left.Length < 3) return;
            int lumSad = 0, chanSad = 0, n = 0;
            for (int i = 0; i < left.Length; i += 3)
            {
                int lR = left[i], lG = left[i + 1], lB = left[i + 2];
                int rR = right[i], rG = right[i + 1], rB = right[i + 2];
                int l = lR + lG + lB;
                int r = rR + rG + rB;
                if (l < 18 && r < 18) continue;
                lumSad += Math.Abs(l / 3 - r / 3);
                chanSad += Math.Abs(lR - rR) + Math.Abs(lG - rG) + Math.Abs(lB - rB);
                n++;
            }
            if (n < 6) return;
            shape = 1.0 - lumSad / (n * 255.0);
            color = 1.0 - chanSad / (n * 765.0);
            if (shape < 0) shape = 0;
            if (color < 0) color = 0;
        }

        static int Ink(byte[] desc)
        {
            int n = 0;
            if (desc == null) return 0;
            for (int i = 0; i < desc.Length; i += 3)
                if (desc[i] + desc[i + 1] + desc[i + 2] >= 18) n++;
            return n;
        }

        static List<WikiEntry> Load()
        {
            if (entries != null) return entries;
            lock (loadGate)
            {
                if (entries != null) return entries;
                List<WikiEntry> loaded = new List<WikiEntry>();
                string path = IndexPath;
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) path = FindIndex();
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    foreach (string raw in File.ReadAllLines(path))
                    {
                        string line = raw;
                        if (line.Length > 0 && line[0] == '\uFEFF') line = line.Substring(1);
                        if (line.Length == 0 || line[0] == '#') continue;
                        string[] parts = line.Split('|');
                        if (parts.Length < 4) continue;
                        byte[] desc = FromHex(parts[3].Trim());
                        if (desc == null || desc.Length != Cells * Cells * 3) continue;
                        WikiEntry entry = new WikiEntry();
                        entry.FileName = parts[0].Trim();
                        entry.Name = parts[1].Trim();
                        entry.WikiFile = parts[2].Trim();
                        entry.Desc = desc;
                        loaded.Add(entry);
                    }
                }
                entries = loaded;
                return entries;
            }
        }

        static string FindIndex()
        {
            string[] starts = new string[] { AppDomain.CurrentDomain.BaseDirectory, Directory.GetCurrentDirectory() };
            foreach (string start in starts)
            {
                if (string.IsNullOrEmpty(start)) continue;
                DirectoryInfo dir = new DirectoryInfo(start);
                for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
                {
                    string candidate = Path.Combine(dir.FullName, "assets", "wiki-index.txt");
                    if (File.Exists(candidate)) return candidate;
                }
            }
            return "";
        }

        static Dictionary<string, string[]> ReadCatalog(string catalogPath)
        {
            Dictionary<string, string[]> catalog = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(catalogPath) || !File.Exists(catalogPath)) return catalog;
            foreach (string line in File.ReadAllLines(catalogPath))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                string[] parts = line.Split('|');
                if (parts.Length >= 2) catalog[parts[0].Trim()] = parts;
            }
            return catalog;
        }

        static void WriteMeta(string pngPath, WikiHit hit)
        {
            string meta = Path.ChangeExtension(pngPath, ".txt");
            File.WriteAllText(meta, hit.Name + "\r\n" + hit.WikiFile + "\r\n", Encoding.UTF8);
        }

        static bool IsPng(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    if (stream.Length < 8) return false;
                    byte[] magic = new byte[8];
                    if (stream.Read(magic, 0, 8) < 8) return false;
                    return magic[0] == 137 && magic[1] == 80 && magic[2] == 78 && magic[3] == 71;
                }
            }
            catch (Exception) { return false; }
        }

        static string SafeFile(string name)
        {
            string file = Path.GetFileName(name ?? "");
            if (string.IsNullOrEmpty(file)) return "item.png";
            foreach (char ch in Path.GetInvalidFileNameChars())
                file = file.Replace(ch, '-');
            return file;
        }

        static string Hex(byte[] data)
        {
            char[] chars = new char[data.Length * 2];
            const string hex = "0123456789abcdef";
            for (int i = 0; i < data.Length; i++)
            {
                chars[i * 2] = hex[data[i] >> 4];
                chars[i * 2 + 1] = hex[data[i] & 15];
            }
            return new string(chars);
        }

        static byte[] FromHex(string text)
        {
            if (string.IsNullOrEmpty(text) || (text.Length % 2) != 0) return null;
            byte[] data = new byte[text.Length / 2];
            for (int i = 0; i < data.Length; i++)
            {
                int hi = HexValue(text[i * 2]);
                int lo = HexValue(text[i * 2 + 1]);
                if (hi < 0 || lo < 0) return null;
                data[i] = (byte)((hi << 4) | lo);
            }
            return data;
        }

        static int HexValue(char ch)
        {
            if (ch >= '0' && ch <= '9') return ch - '0';
            if (ch >= 'a' && ch <= 'f') return ch - 'a' + 10;
            if (ch >= 'A' && ch <= 'F') return ch - 'A' + 10;
            return -1;
        }

        sealed class WikiClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest request = base.GetWebRequest(address);
                if (request != null) request.Timeout = 8000;
                return request;
            }
        }
    }
}
