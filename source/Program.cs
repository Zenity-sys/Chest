using System;
using System.IO;
using System.Windows.Forms;

namespace Chest
{
    public static class Program
    {
        private static System.Threading.Mutex singleInstance;

        [STAThread]
        public static int Main(string[] args)
        {
            CrashLog.Install();
            NetSecurity.EnableModernTls();
            if (args.Length > 0 && args[0] == "--test")
                return Tests.Run(args.Length > 1 ? args[1] : null);
            AppPaths.Prepare();
            try { AppUpdates.Start(); }
            catch (Exception startup) { CrashLog.Write(startup); }
            try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); }
            catch (EntryPointNotFoundException)
            {
                try { Native.SetProcessDPIAware(); }
                catch (EntryPointNotFoundException) { }
                catch (DllNotFoundException) { }
            }
            catch (DllNotFoundException) { }
            bool created;
            singleInstance = new System.Threading.Mutex(true, "Chest.RewardLogger", out created);
            if (!created || singleInstance == null) return 0;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ChestForm());
            return 0;
        }
    }
}
