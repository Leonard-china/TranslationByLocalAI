using System;
using System.IO;

namespace TranslationByLocalAI
{
    internal static class AppLogger
    {
        private static readonly object Sync = new object();

        internal static string LogPath
        {
            get { return Path.Combine(AppConfig.ConfigDirectory, "app.log"); }
        }

        internal static void Write(string message)
        {
            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(AppConfig.ConfigDirectory);
                    File.AppendAllText(
                        LogPath,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                        + " "
                        + message
                        + Environment.NewLine);
                }
            }
            catch
            {
            }
        }
    }
}
