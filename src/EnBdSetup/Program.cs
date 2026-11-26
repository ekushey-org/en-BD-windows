using System;
using System.Linq;
using System.Windows.Forms;

namespace EnBdSetup
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            string mode = args.FirstOrDefault() ?? string.Empty;

            if (string.Equals(mode, "/install", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "--install", StringComparison.OrdinalIgnoreCase))
            {
                Action<string> fileLog = s => LanguageManager.AppendSetupLogLine("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + s);
                try
                {
                    LanguageManager.AppendSetupLogLine("=== Silent Install " + Constants.ProductVersion + " started " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
                    LanguageManager.EnsureEnGbLanguagePack(fileLog);
                    LocaleInstaller.InstallCustomLocale(fileLog);
                    string klid = KeyboardInstaller.InstallKeyboardLayout(fileLog);
                    LanguageManager.ConfigureLanguagesAndDisplay(klid, fileLog);
                    LocaleInstaller.ApplyUserInternationalRegistryTweaks(fileLog);
                    LanguageManager.RegisterUninstaller(fileLog);
                    fileLog("Done.");
                    return 0;
                }
                catch (Exception ex)
                {
                    fileLog("INSTALLATION UNSUCCESSFUL: " + ex.Message);
                    return 1;
                }
            }

            if (string.Equals(mode, "/uninstall", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "--uninstall", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Action<string> noop = s => { };
                    LanguageManager.RestorePreviousLanguages(noop);
                    KeyboardInstaller.UninstallKeyboardLayout(noop);
                    LocaleInstaller.UninstallCustomLocale(noop);
                    LanguageManager.UnregisterUninstaller(noop);
                    return 0;
                }
                catch
                {
                    return 1;
                }
            }

            bool uninstallUi = string.Equals(mode, "/uninstall-ui", StringComparison.OrdinalIgnoreCase);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(uninstallUi));
            return 0;
        }
    }
}
