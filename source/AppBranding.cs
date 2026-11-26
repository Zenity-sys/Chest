using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace Chest
{
    internal static class AppBranding
    {
        static readonly Assembly App = typeof(AppBranding).Assembly;

        internal static string DisplayVersion { get { return Shown(App.GetName().Version); } }

        // The window credit keeps the patch, so 0.6.2 reads v0.6.2.
        internal static string FullVersion
        {
            get
            {
                Version version = App.GetName().Version;
                if (version == null) return "v0.6.2";
                int build = version.Build < 0 ? 0 : version.Build;
                return "v" + version.Major + "." + version.Minor + "." + build;
            }
        }

        // The assembly keeps three parts (0.1.0). A zero patch is shown as v0.1.
        internal static string Shown(Version version)
        {
            if (version == null) return "";
            if (version.Build > 0) return "v" + version.Major + "." + version.Minor + "." + version.Build;
            return "v" + version.Major + "." + version.Minor;
        }

        internal static string Shown(string version)
        {
            if (string.IsNullOrEmpty(version)) return "";
            string text = version.Trim();
            if (text.StartsWith("v") || text.StartsWith("V")) text = text.Substring(1);
            string[] parts = text.Split('.');
            int major, minor, patch;
            if (parts.Length >= 2 && int.TryParse(parts[0], out major) && int.TryParse(parts[1], out minor))
            {
                if (parts.Length >= 3 && int.TryParse(parts[2], out patch) && patch > 0)
                    return "v" + major + "." + minor + "." + patch;
                return "v" + major + "." + minor;
            }
            return "v" + text;
        }

        internal static Icon LoadIcon()
        {
            using (Stream stream = App.GetManifestResourceStream("Chest.Branding.chest.ico"))
            {
                if (stream == null) return SystemIcons.Application;
                using (Icon icon = new Icon(stream, 32, 32))
                    return (Icon)icon.Clone();
            }
        }
    }
}
