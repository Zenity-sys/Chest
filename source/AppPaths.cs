using System;
using System.IO;

namespace Chest
{
    // Saved rewards live outside the install folder. The updater deletes the
    // install folder, so totals cannot sit in the same place as Chest.exe.
    internal static class AppPaths
    {
        public static string DataRoot
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChestData"); }
        }

        public static void Prepare()
        {
            try
            {
                Directory.CreateDirectory(DataRoot);
                Environment.CurrentDirectory = DataRoot;
                string legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Chest", "totals.json");
                string current = Path.Combine(DataRoot, "totals.json");
                if (!File.Exists(current) && File.Exists(legacy))
                    File.Copy(legacy, current, false);
            }
            catch (Exception) { }
        }
    }
}
