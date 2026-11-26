using System;
using System.Drawing;
using System.Globalization;
using System.IO;

namespace Chest
{
    // The shipped templates were measured on a 1440p client at 100% (396 by 239).
    // Other tiers scale from that once and stay cached. 1080p is smaller, 4K is larger.
    internal struct RewardProfile
    {
        public int Bucket;
        public int Percent;
        public int Num;
        public int Den;
        public bool Identity { get { return Num == Den; } }

        public static RewardProfile Native { get { return new RewardProfile { Bucket = 1440, Percent = 100, Num = 1, Den = 1 }; } }

        public int Px(int native)
        {
            if (Num == Den || native == 0) return native;
            int value = native * Num;
            if (value >= 0) value += Den / 2;
            else value -= Den / 2;
            return value / Den;
        }

        public static RewardProfile For(int clientHeight, int dpi)
        {
            int bucket = ClientScale.ResolutionBucket(clientHeight);
            if (bucket == 0 || (clientHeight > 0 && clientHeight < 900)) bucket = 1440;
            int percent = dpi <= 0 ? 100 : ClientScale.ScalingPercent(dpi);
            int num = bucket * percent;
            int den = 1440 * 100;
            int g = Gcd(num, den);
            num /= g;
            den /= g;
            return new RewardProfile { Bucket = bucket, Percent = percent, Num = num, Den = den };
        }

        static int Gcd(int a, int b)
        {
            if (a < 0) a = -a;
            if (b < 0) b = -b;
            while (b != 0) { int t = a % b; a = b; b = t; }
            return a == 0 ? 1 : a;
        }
    }

    internal static class ClientScale
    {
        public static int ResolutionBucket(int pixelHeight)
        {
            if (pixelHeight <= 0) return 0;
            int best = 1080, bestDist = Math.Abs(pixelHeight - 1080);
            int mid = Math.Abs(pixelHeight - 1440);
            if (mid < bestDist) { best = 1440; bestDist = mid; }
            int tall = Math.Abs(pixelHeight - 2160);
            if (tall < bestDist) best = 2160;
            return best;
        }

        public static string ResolutionName(int bucket)
        {
            if (bucket >= 2000) return "4K";
            if (bucket >= 1300) return "1440p";
            return "1080p";
        }

        public static int ScalingPercent(int dpi)
        {
            if (dpi <= 0) return 100;
            float exact = dpi / 96F * 100F;
            int best = 100;
            float bestDist = Math.Abs(exact - 100F);
            int[] choices = { 125, 150, 200 };
            for (int i = 0; i < choices.Length; i++)
            {
                float dist = Math.Abs(exact - choices[i]);
                if (dist < bestDist) { best = choices[i]; bestDist = dist; }
            }
            return best;
        }
    }

    // After TRAIL COMPLETE is found, the next grabs are that window plus padding.
    internal sealed class CaptureFollow
    {
        public const int Pad = 80;
        public const int MissesBeforeFull = 4;
        bool wide = true;
        int misses;
        Size size;

        public void Reset()
        {
            wide = true;
            misses = 0;
            size = Size.Empty;
        }

        public Rectangle Choose(Size client, Rectangle panel)
        {
            Rectangle full = new Rectangle(0, 0, client.Width, client.Height);
            if (size.Width > 0 && size != client) { wide = true; misses = 0; }
            size = client;
            if (wide || panel.Width < 40 || panel.Height < 40)
            {
                wide = false;
                return full;
            }
            Rectangle roi = Rectangle.Inflate(panel, Pad, Pad);
            roi = Rectangle.Intersect(roi, full);
            if (roi.Width < 100 || roi.Height < 100) return full;
            return roi;
        }

        public void Note(bool found)
        {
            if (found) { misses = 0; return; }
            misses++;
            if (misses >= MissesBeforeFull) { misses = 0; wide = true; }
        }

        // A padded grab can miss the title while the reward is still on screen.
        // That miss does not count until the full client is checked and also misses.
        public static bool MissCounts(bool narrowed, bool foundInGrab, bool foundFull)
        {
            if (foundInGrab) return false;
            if (narrowed && foundFull) return false;
            return true;
        }
    }

    // Another Kavros client in front hides this panel. Our own client does not.
    internal static class OverlayVisibility
    {
        public static bool CoveredByOtherClient(IntPtr selected, IntPtr foreground, string foregroundTitle)
        {
            if (foreground == IntPtr.Zero || foreground == selected) return false;
            if (string.IsNullOrEmpty(foregroundTitle)) return false;
            return foregroundTitle.IndexOf("kavros", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    // Remembers the last physical rectangle and skips a move that did not change.
    internal struct PhysicalPlace
    {
        public bool Known;
        public Rectangle Bounds;
        public bool Changed(Rectangle next)
        {
            if (!Known || Bounds != next) { Known = true; Bounds = next; return true; }
            return false;
        }
    }

    internal static class ClientLaunch
    {
        public static long Hwnd;
        public static int Pid;
        public static string BindDirectory;

        public static void Reset()
        {
            Hwnd = 0;
            Pid = 0;
        }

        public static void ReadArgs(string[] args)
        {
            if (args == null) return;
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i] ?? "";
                string name, value;
                if (!Split(arg, out name, out value))
                {
                    if (i + 1 >= args.Length) continue;
                    name = arg;
                    value = args[i + 1];
                    if (!IsHandle(name) && !IsPid(name)) continue;
                    i++;
                }
                long number;
                if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) || number <= 0) continue;
                if (IsHandle(name)) Hwnd = number;
                else if (IsPid(name)) Pid = (int)number;
            }
        }

        public static void ReadEnvironment()
        {
            ApplyEnv("COMPANION_CLIENT_HWND", true);
            ApplyEnv("KAVROS_CLIENT_HWND", true);
            ApplyEnv("COMPANION_CLIENT_PID", false);
            ApplyEnv("KAVROS_CLIENT_PID", false);
        }

        public static void WriteRequest()
        {
            if (Hwnd <= 0 && Pid <= 0) return;
            try
            {
                string dir = BindRoot();
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "bind.txt"), Hwnd.ToString(CultureInfo.InvariantCulture) + "\n" + Pid.ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception) { }
        }

        public static void ReadRequest()
        {
            try
            {
                string path = Path.Combine(BindRoot(), "bind.txt");
                if (!File.Exists(path)) return;
                string text = File.ReadAllText(path);
                File.Delete(path);
                string[] lines = text.Split('\n');
                long hwnd;
                int pid;
                if (lines.Length > 0 && long.TryParse(lines[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out hwnd) && hwnd > 0) Hwnd = hwnd;
                if (lines.Length > 1 && int.TryParse(lines[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out pid) && pid > 0) Pid = pid;
            }
            catch (Exception) { }
        }

        static string BindRoot()
        {
            if (!string.IsNullOrEmpty(BindDirectory)) return BindDirectory;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChestData");
        }

        static void ApplyEnv(string name, bool hwnd)
        {
            string text = Environment.GetEnvironmentVariable(name);
            long number;
            if (text == null || !long.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number) || number <= 0) return;
            if (hwnd) Hwnd = number;
            else Pid = (int)number;
        }

        static bool Split(string arg, out string name, out string value)
        {
            name = "";
            value = "";
            int cut = arg.IndexOf('=');
            if (cut < 0) cut = arg.IndexOf(':');
            if (cut <= 0 || cut >= arg.Length - 1) return false;
            name = arg.Substring(0, cut);
            value = arg.Substring(cut + 1);
            return IsHandle(name) || IsPid(name);
        }

        static bool IsHandle(string name)
        {
            return Same(name, "--hwnd") || Same(name, "--client-hwnd") || Same(name, "/hwnd") || Same(name, "hwnd");
        }

        static bool IsPid(string name)
        {
            return Same(name, "--pid") || Same(name, "--client-pid") || Same(name, "/pid") || Same(name, "pid");
        }

        static bool Same(string text, string value)
        {
            return string.Equals(text ?? "", value, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static class CompanionStatus
    {
        public const string ModuleId = "chest";
        static string lastState;
        static DateTime lastWrite;

        public static void Write(string state)
        {
            try
            {
                if (state == lastState && (DateTime.UtcNow - lastWrite).TotalSeconds < 5) return;
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kavros", "Companion", "modules");
                Directory.CreateDirectory(dir);
                string json = "{"
                    + "\"id\":\"chest\","
                    + "\"name\":\"Kavros Chest\","
                    + "\"author\":\"Zenity\","
                    + "\"version\":\"" + AppBranding.FullVersion + "\","
                    + "\"state\":\"" + (state ?? "") + "\","
                    + "\"paused\":false,"
                    + "\"label\":\"" + (state == "running" ? "Running" : "Stopped") + "\","
                    + "\"updatedUtc\":\"" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + "\""
                    + "}";
                string path = Path.Combine(dir, "chest.json");
                string next = path + ".tmp";
                File.WriteAllText(next, json);
                if (File.Exists(path)) File.Replace(next, path, null);
                else File.Move(next, path);
                lastWrite = DateTime.UtcNow;
                lastState = state;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        public static string ModulePath(string root)
        {
            return Path.Combine(root, "companion.module.json");
        }
    }
}
