using System;
using System.Threading;
using System.Windows.Forms;

namespace ImageMap.WinForms
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            try
            {
                CrashLog.Install();
                CrashLog.Start();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (_, e) => CrashLog.Error("Application.ThreadException", e.Exception);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                {
                    if (e.ExceptionObject is Exception ex)
                        CrashLog.Error("AppDomain.UnhandledException", ex);
                    else
                        CrashLog.Error("AppDomain.UnhandledException: " + e.ExceptionObject);
                };
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                CrashLog.Error("Startup failure", ex);
                try
                {
                    MessageBox.Show(
                        "Image Map failed to start.\n\n" + ex.Message + "\n\nLog: " + CrashLog.Path,
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
}
