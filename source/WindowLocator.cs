using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;

namespace Chest
{
    public sealed class GameWindow
    {
        public IntPtr Handle;
        public string Title = "";
        public string ProcessName = "";
        public string ClassName = "";
        public uint ProcessId;
        public Rectangle Client;
        public string Display;
        public override string ToString() { return string.IsNullOrEmpty(Display) ? Title : Display; }
    }

    internal static class ClientIdentity
    {
        public static string Label(uint pid, int index, string title)
        {
            string fromShare = NameFromShare(pid);
            if (!string.IsNullOrEmpty(fromShare)) return fromShare;
            return "Client " + (index + 1).ToString();
        }

        static string sharePath;
        static DateTime shareTime;
        static Dictionary<int, string> shareNames = new Dictionary<int, string>();

        static string NameFromShare(uint pid)
        {
            if (pid == 0) return null;
            try
            {
                if (sharePath == null)
                    sharePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KavrosOverlay", "client-names.json");
                if (!System.IO.File.Exists(sharePath))
                {
                    shareTime = DateTime.MinValue;
                    shareNames = new Dictionary<int, string>();
                    return null;
                }
                DateTime written = System.IO.File.GetLastWriteTimeUtc(sharePath);
                if (written != shareTime)
                {
                    shareTime = written;
                    shareNames = ParseShare(System.IO.File.ReadAllText(sharePath, Encoding.UTF8));
                }
                string name;
                if (shareNames.TryGetValue((int)pid, out name) && !string.IsNullOrEmpty(name)) return name;
            }
            catch (Exception) { }
            return null;
        }

        internal static Dictionary<int, string> ParseShare(string json)
        {
            Dictionary<int, string> map = new Dictionary<int, string>();
            if (string.IsNullOrEmpty(json)) return map;
            foreach (System.Text.RegularExpressions.Match obj in System.Text.RegularExpressions.Regex.Matches(json, "\\{[^}]+\\}"))
            {
                System.Text.RegularExpressions.Match pid = System.Text.RegularExpressions.Regex.Match(obj.Value, "\"pid\"\\s*:\\s*(-?\\d+)");
                System.Text.RegularExpressions.Match name = System.Text.RegularExpressions.Regex.Match(obj.Value, "\"name\"\\s*:\\s*\"([^\"]*)\"");
                int id;
                if (pid.Success && name.Success && int.TryParse(pid.Groups[1].Value, out id) && name.Groups[1].Value.Length > 0)
                    map[id] = name.Groups[1].Value;
            }
            return map;
        }
    }

    public static class WindowLocator
    {
        static bool Contains(string text, string value) { return (text ?? "").IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0; }

        public static bool IsLauncher(GameWindow window)
        {
            bool gameTitle = Contains(window.Title, "nxt") || Contains(window.Title, "944") || Contains(window.ClassName, "jag") || Contains(window.ClassName, "glfw");
            return Contains(window.Title, "launcher") || Contains(window.ClassName, "launcher") || (Contains(window.ProcessName, "launcher") && !gameTitle);
        }

        public static int Score(GameWindow window)
        {
            if (window == null || window.Handle == IntPtr.Zero) return -1000;
            if (IsOwn(window) || IsHelperProcess(window.ProcessName)) return -1000;
            if (Contains(window.Title, "AFK Warden") || Contains(window.Title, "Slayer Helper") || Contains(window.Title, "Kavros Clue Helper")) return -1000;
            if (Contains(window.Title, "Companion") && (Contains(window.Title, "Kavros") || Contains(window.ProcessName, "Kavros"))) return -1000;
            if (Contains(window.ClassName, "CabinetWClass") || Contains(window.ClassName, "Chrome_WidgetWin") || Contains(window.ClassName, "ApplicationFrameWindow")) return -1000;
            if (IsLauncher(window)) return -1000;
            if (!Contains(window.Title, "kavros")) return -1000;
            int score = 400;
            if (Contains(window.ProcessName, "kavros")) score += 80;
            if (Contains(window.ClassName, "jag") || Contains(window.ClassName, "glfw") || Contains(window.ClassName, "lwjgl")) score += 70;
            if (window.Client.Width >= 600 && window.Client.Height >= 350) score += 40;
            if (window.Client.Width < 250 || window.Client.Height < 150) score -= 250;
            return score;
        }

        static bool IsOwn(GameWindow window)
        {
            return Contains(window.ProcessName, "Chest") || Contains(window.Title, "Chest");
        }

        public static bool IsClient(GameWindow window) { return Score(window) >= 100; }

        public static bool IsForeground(IntPtr game)
        {
            if (game == IntPtr.Zero) return false;
            IntPtr foreground = Native.GetForegroundWindow();
            if (foreground == IntPtr.Zero) return false;
            return foreground == game || Native.GetAncestor(foreground, Native.GaRoot) == game;
        }

        public static List<GameWindow> Clients(IEnumerable<GameWindow> windows)
        {
            List<GameWindow> clients = new List<GameWindow>();
            if (windows == null) return clients;
            foreach (GameWindow window in windows)
                if (IsClient(window)) clients.Add(window);
            return clients;
        }

        public static void NameClients(IList<GameWindow> clients)
        {
            if (clients == null) return;
            for (int i = 0; i < clients.Count; i++)
            {
                GameWindow window = clients[i];
                if (window == null) continue;
                window.Display = ClientIdentity.Label(window.ProcessId, i, window.Title);
            }
        }

        public static GameWindow Choose(IEnumerable<GameWindow> windows, IntPtr current, uint processId, IntPtr foreground)
        {
            List<GameWindow> games = new List<GameWindow>();
            if (windows != null)
                foreach (GameWindow window in windows)
                    if (IsClient(window)) games.Add(window);
            games.Sort(delegate(GameWindow a, GameWindow b)
            {
                int score = Score(b).CompareTo(Score(a));
                return score != 0 ? score : string.Compare(a.Title, b.Title, StringComparison.Ordinal);
            });
            GameWindow existing = games.Find(delegate(GameWindow w) { return w.Handle == current; });
            if (existing != null) return existing;
            if (processId != 0)
            {
                GameWindow same = games.Find(delegate(GameWindow w) { return w.ProcessId == processId; });
                if (same != null) return same;
            }
            if (current != IntPtr.Zero && games.Count != 1) return null;
            GameWindow focused = games.Find(delegate(GameWindow w) { return w.Handle == foreground; });
            if (focused != null) return focused;
            return games.Count > 0 ? games[0] : null;
        }

        public static bool IsHelperProcess(string processName)
        {
            string name = processName ?? "";
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4);
            return string.Equals(name, "KavrosCompanion", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "KavrosAfkWarden", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "KavrosSlayerHelper", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "KavrosClueHelper", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Chest", StringComparison.OrdinalIgnoreCase);
        }

        public static List<GameWindow> Enumerate(int excludedProcessId)
        {
            List<GameWindow> list = new List<GameWindow>();
            Dictionary<uint, string> processNames = new Dictionary<uint, string>();
            Native.EnumWindows(delegate(IntPtr window, IntPtr parameter)
            {
                try { return Inspect(window, excludedProcessId, list, processNames); }
                catch (Exception ex) { CrashLog.Write(ex); return true; }
            }, IntPtr.Zero);
            list.Sort(delegate(GameWindow a, GameWindow b)
            {
                int score = Score(b).CompareTo(Score(a));
                return score != 0 ? score : string.Compare(a.Title, b.Title, StringComparison.Ordinal);
            });
            return list;
        }

        static bool Inspect(IntPtr window, int excludedProcessId, List<GameWindow> list, Dictionary<uint, string> processNames)
        {
            if (!Native.IsWindowVisible(window)) return true;
            uint pid;
            Native.GetWindowThreadProcessId(window, out pid);
            if (pid == (uint)excludedProcessId) return true;
            StringBuilder title = new StringBuilder(1024);
            Native.GetWindowText(window, title, title.Capacity);
            if (title.Length == 0) return true;
            StringBuilder className = new StringBuilder(256);
            Native.GetClassName(window, className, className.Capacity);
            string processName;
            if (!processNames.TryGetValue(pid, out processName))
            {
                try { using (Process process = Process.GetProcessById((int)pid)) processName = process.ProcessName; }
                catch { processName = ""; }
                processNames[pid] = processName;
            }
            list.Add(new GameWindow
            {
                Handle = window,
                Title = title.ToString(),
                ProcessId = pid,
                ProcessName = processName,
                ClassName = className.ToString(),
                Client = Native.ClientBounds(window)
            });
            return true;
        }
    }
}
