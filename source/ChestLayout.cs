using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Chest
{
    // Every number the window and the watcher use. assets/chest-layout.txt
    // overrides them, so a later mockup can move the grid without a rebuild.
    internal sealed class ChestLayout
    {
        public int Columns = 6;
        public int Icon = 72;
        public int Gap = 12;
        public int NameHeight = 16;
        public int Pad = 16;
        public int WindowWidth = 880;
        public int WindowHeight = 720;
        public int PollMilliseconds = 70;
        public int MissesBeforeClose = 3;
        public double IconMatch = 0.94;

        public static ChestLayout Load(string path)
        {
            ChestLayout layout = new ChestLayout();
            if (!File.Exists(path)) return layout;
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            layout.Columns = Read(values, "columns", layout.Columns);
            layout.Icon = Read(values, "icon", layout.Icon);
            layout.Gap = Read(values, "gap", layout.Gap);
            layout.NameHeight = Read(values, "nameHeight", layout.NameHeight);
            layout.Pad = Read(values, "pad", layout.Pad);
            layout.WindowWidth = Read(values, "windowWidth", layout.WindowWidth);
            layout.WindowHeight = Read(values, "windowHeight", layout.WindowHeight);
            layout.PollMilliseconds = Read(values, "pollMilliseconds", layout.PollMilliseconds);
            layout.MissesBeforeClose = Read(values, "missesBeforeClose", layout.MissesBeforeClose);
            double match;
            string text;
            if (values.TryGetValue("iconMatch", out text) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out match))
                layout.IconMatch = match;
            return layout;
        }

        static int Read(Dictionary<string, string> values, string key, int fallback)
        {
            string text;
            int number;
            if (values.TryGetValue(key, out text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) && number >= 0)
                return number;
            return fallback;
        }
    }
}
