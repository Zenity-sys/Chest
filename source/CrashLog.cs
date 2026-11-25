using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Chest
{
    // Failures go to disk. The player never gets a crash dialog.
    internal static class CrashLog
    {
        internal static string FilePath
        {
            get
            {
                return Path.Combine(AppPaths.DataRoot, "crash.log");
            }
        }

        internal static void Install()
        {
            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs args) { Write(args.Exception); };
            }
            catch (Exception ex) { Write(ex); }
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs args)
            {
                Exception exception = args.ExceptionObject as Exception;
                Write(exception ?? new Exception(args.ExceptionObject == null ? "Unknown failure" : args.ExceptionObject.ToString()));
            };
            try
            {
                TaskScheduler.UnobservedTaskException += delegate(object sender, UnobservedTaskExceptionEventArgs args)
                {
                    Write(args.Exception);
                    args.SetObserved();
                };
            }
            catch (Exception ex) { Write(ex); }
        }

        internal static void Write(Exception exception)
        {
            try
            {
                if (exception == null) return;
                string directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.AppendAllText(FilePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + exception + Environment.NewLine + Environment.NewLine);
            }
            catch { }
        }
    }
}
