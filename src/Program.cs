using System;
using System.Threading;
using System.Windows.Forms;

namespace TranslationByLocalAI
{
    internal static class Program
    {
        private const string MutexName = "Local\\TranslationByLocalAI.SingleInstance";

        [STAThread]
        private static void Main()
        {
            AppLogger.Write("Process entry.");
            bool createdNew;
            using (var mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    AppLogger.Write("Another application instance owns the mutex.");
                    MessageBox.Show(
                        "划词翻译已经在运行，请查看系统托盘。",
                        "本地 AI 划词翻译",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                NativeMethods.SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                AppLogger.Write("Starting application context.");
                var openReader = HasArgument(
                    Environment.GetCommandLineArgs(),
                    "--reader");
                using (var context = new TranslationApplicationContext(openReader))
                {
                    Application.Run(context);
                }
            }
        }

        private static bool HasArgument(string[] args, string expected)
        {
            foreach (var value in args)
            {
                if (string.Equals(value, expected, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
