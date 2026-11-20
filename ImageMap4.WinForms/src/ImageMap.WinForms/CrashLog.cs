using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ImageMap.WinForms
{
    internal static class CrashLog
    {
        private static readonly object Gate = new();
        private static string? _path;

        public static string Path
        {
            get
            {
                EnsurePath();
                return _path!;
            }
        }

        public static void Install()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) =>
            {
                Error("UI thread exception", e.Exception);
                ShowCrash(e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                var ex = e.ExceptionObject as Exception ?? new Exception(Convert.ToString(e.ExceptionObject));
                Error("Unhandled exception", ex);
                if (e.IsTerminating)
                    ShowCrash(ex);
            };
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Error("Unobserved task exception", e.Exception);
                e.SetObserved();
            };
        }

        public static void Start()
        {
            Info("======== Image Map start ========");
            Info("Time: " + DateTime.Now.ToString("o"));
            Info("OS: " + Environment.OSVersion);
            Info("64-bit OS: " + Environment.Is64BitOperatingSystem);
            Info("64-bit process: " + Environment.Is64BitProcess);
            Info("IntPtr.Size: " + IntPtr.Size);
            Info("PROCESSOR_ARCHITECTURE: " + (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? ""));
            Info("CLR: " + Environment.Version);
            Info("Base directory: " + AppDomain.CurrentDomain.BaseDirectory);
            Info("Command line: " + Environment.CommandLine);
            Info("Log file: " + Path);
        }

        public static void Info(string message) => Write("INFO", message, null);

        public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

        public static void Error(Exception exception) => Write("ERROR", exception.Message, exception);

        private static void Write(string level, string message, Exception? exception)
        {
            try
            {
                EnsurePath();
                var builder = new StringBuilder();
                builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                builder.Append(" [").Append(level).Append("] ");
                builder.Append(message);
                if (exception != null)
                {
                    builder.AppendLine();
                    builder.Append(exception);
                }
                builder.AppendLine();
                lock (Gate)
                    File.AppendAllText(_path!, builder.ToString(), Encoding.UTF8);
            }
            catch
            {
            }
        }

        private static void EnsurePath()
        {
            if (_path != null)
                return;
            lock (Gate)
            {
                if (_path != null)
                    return;
                _path = TryCreate(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"))
                    ?? TryCreate(System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "ImageMap", "crash.log"))
                    ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ImageMap-crash.log");
            }
        }

        private static string? TryCreate(string path)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                File.AppendAllText(path, "");
                return path;
            }
            catch
            {
                return null;
            }
        }

        private static void ShowCrash(Exception exception)
        {
            try
            {
                MessageBox.Show(
                    "Image Map crashed.\n\n" + exception.Message + "\n\nLog: " + Path,
                    "Image Map",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch
            {
            }
        }
    }
}
