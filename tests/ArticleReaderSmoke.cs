using System;
using System.Windows.Forms;
using TranslationByLocalAI;

namespace TranslationByLocalAISmoke
{
    internal static class ArticleReaderSmoke
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var icon = UiTheme.CreateAppIcon())
            using (var form = new ArticleReaderForm(icon))
            {
                Application.Run(form);
            }
        }
    }
}
