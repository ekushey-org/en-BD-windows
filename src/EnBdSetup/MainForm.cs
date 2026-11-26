using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EnBdSetup
{
    internal sealed class MainForm : Form
    {
        private readonly Button _btnInstall;
        private readonly Button _btnUninstall;
        private readonly Button _btnClose;
        private readonly ProgressBar _progress;
        private readonly TextBox _txtLog;
        private readonly bool _autoUninstallMode;

        public MainForm(bool autoUninstallMode)
        {
            _autoUninstallMode = autoUninstallMode;

            Text = Constants.ProductDisplayName;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(520, 380);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            Font = new Font("Segoe UI", 9.5f);
            BackColor = Color.White;

            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(0),
                BackColor = Color.White
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // Header
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // Summary
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // Progress
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // Log
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // Buttons

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.FromArgb(245, 248, 252),
                Padding = new Padding(16, 14, 16, 14),
                Margin = new Padding(0),
                AutoSize = true
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            var picIcon = new PictureBox
            {
                Size = new Size(44, 44),
                SizeMode = PictureBoxSizeMode.Zoom,
                Margin = new Padding(0, 0, 14, 0)
            };
            if (Icon != null)
            {
                picIcon.Image = Icon.ToBitmap();
            }

            var lblTitle = new Label
            {
                Text = "English (Bangladesh) Setup",
                UseMnemonic = false,
                Font = new Font("Segoe UI Semibold", 13f),
                ForeColor = Color.FromArgb(20, 35, 55),
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 0, 2)
            };

            var lblSub = new Label
            {
                Text = "ekushey.org  •  en-BD Locale  •  " + Constants.ProductVersion,
                UseMnemonic = false,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(85, 100, 120),
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };

            header.Controls.Add(picIcon, 0, 0);
            header.SetRowSpan(picIcon, 2);
            header.Controls.Add(lblTitle, 1, 0);
            header.Controls.Add(lblSub, 1, 1);

            var lblSummary = new Label
            {
                Text =
                    "• ৳ (BDT), Lakh/Crore, DD/MM/YYYY, 12-hr AM/PM, Metric, A4\r\n" +
                    "• Default keyboard: US with Taka sign",
                UseMnemonic = false,
                AutoSize = true,
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(45, 55, 70),
                Padding = new Padding(16, 14, 16, 12),
                Margin = new Padding(0)
            };

            _progress = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Height = 14,
                Style = ProgressBarStyle.Continuous,
                Margin = new Padding(16, 0, 16, 10)
            };

            _txtLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 8.75f),
                BackColor = Color.FromArgb(24, 28, 36),
                ForeColor = Color.FromArgb(225, 232, 240),
                Margin = new Padding(16, 0, 16, 12)
            };

            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                AutoSize = true,
                Padding = new Padding(16, 0, 16, 16),
                Margin = new Padding(0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _btnInstall = new Button
            {
                Text = "Install",
                UseMnemonic = false,
                Size = new Size(116, 34),
                BackColor = Color.FromArgb(16, 110, 190),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnInstall.FlatAppearance.BorderSize = 0;
            _btnInstall.Click += async (s, e) => await RunInstallAsync();

            _btnUninstall = new Button
            {
                Text = "Uninstall",
                UseMnemonic = false,
                Size = new Size(116, 34),
                FlatStyle = FlatStyle.System,
                Margin = new Padding(0)
            };
            _btnUninstall.Click += async (s, e) => await RunUninstallAsync();

            _btnClose = new Button
            {
                Text = "Close",
                UseMnemonic = false,
                Size = new Size(92, 34),
                FlatStyle = FlatStyle.System,
                Margin = new Padding(0)
            };
            _btnClose.Click += (s, e) => Close();

            footer.Controls.Add(_btnInstall, 0, 0);
            footer.Controls.Add(_btnUninstall, 1, 0);
            footer.Controls.Add(_btnClose, 3, 0);

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(lblSummary, 0, 1);
            root.Controls.Add(_progress, 0, 2);
            root.Controls.Add(_txtLog, 0, 3);
            root.Controls.Add(footer, 0, 4);

            Controls.Add(root);

            Shown += async (s, e) =>
            {
                if (_autoUninstallMode)
                {
                    var res = MessageBox.Show(
                        this,
                        "Remove the English (Bangladesh) [en-BD] locale and US with Taka sign keyboard layout?",
                        Constants.ProductDisplayName,
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    if (res == DialogResult.Yes)
                    {
                        await RunUninstallAsync();
                    }
                    else
                    {
                        Close();
                    }
                }
            };
        }

        private void AppendLog(string message)
        {
            string formatted = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message;
            LanguageManager.AppendSetupLogLine(formatted);

            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendLogLineUi), formatted);
                return;
            }
            AppendLogLineUi(formatted);
        }

        private void AppendLogLineUi(string formatted)
        {
            _txtLog.AppendText(formatted + Environment.NewLine);
        }

        private void SetBusy(bool busy, int progressValue = 100)
        {
            _btnInstall.Enabled = !busy;
            _btnUninstall.Enabled = !busy;
            _btnClose.Enabled = !busy;
            _progress.Style = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            _progress.Value = busy ? 30 : progressValue;
        }

        private async Task RunInstallAsync()
        {
            _txtLog.Clear();
            LanguageManager.AppendSetupLogLine("=== Install " + Constants.ProductVersion + " started " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
            SetBusy(true);
            bool succeeded = false;

            try
            {
                await Task.Run(() =>
                {
                    // Step 1: Ensure en-GB display language pack is downloaded BEFORE modifying any system settings.
                    LanguageManager.EnsureEnGbLanguagePack(AppendLog);

                    // Step 2: Install en-BD custom locale, US with Taka sign keyboard, and activate display language.
                    LocaleInstaller.InstallCustomLocale(AppendLog);
                    string klid = KeyboardInstaller.InstallKeyboardLayout(AppendLog);
                    LanguageManager.ConfigureLanguagesAndDisplay(klid, AppendLog);
                    LocaleInstaller.ApplyUserInternationalRegistryTweaks(AppendLog);
                    LanguageManager.RegisterUninstaller(AppendLog);
                });

                succeeded = true;
                AppendLog("Done. Log saved to " + LanguageManager.GetSetupLogPath());
                SetBusy(false, 100);

                var answer = MessageBox.Show(
                    this,
                    "Installation complete. Sign out or restart Windows so all apps use the new en-BD display language, regional format, and keyboard layout.\r\n\r\nRestart Windows now?",
                    Constants.ProductDisplayName,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (answer == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start("shutdown.exe", "/r /t 3");
                    Close();
                }
            }
            catch (Exception ex)
            {
                AppendLog("INSTALLATION UNSUCCESSFUL: " + ex.Message);
                AppendLog("Log saved to " + LanguageManager.GetSetupLogPath());
                SetBusy(false, 0);
                MessageBox.Show(
                    this,
                    "Installation unsuccessful.\r\n\r\n" + ex.Message + "\r\n\r\nLog: " + LanguageManager.GetSetupLogPath(),
                    Constants.ProductDisplayName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false, succeeded ? 100 : 0);
            }
        }

        private async Task RunUninstallAsync()
        {
            _txtLog.Clear();
            LanguageManager.AppendSetupLogLine("=== Uninstall " + Constants.ProductVersion + " started " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
            SetBusy(true);
            bool succeeded = false;

            try
            {
                await Task.Run(() =>
                {
                    LanguageManager.RestorePreviousLanguages(AppendLog);
                    KeyboardInstaller.UninstallKeyboardLayout(AppendLog);
                    LocaleInstaller.UninstallCustomLocale(AppendLog);
                    LanguageManager.UnregisterUninstaller(AppendLog);
                });

                succeeded = true;
                AppendLog("Uninstall complete. Please sign out or restart Windows.");
                SetBusy(false, 100);
                MessageBox.Show(
                    this,
                    "Uninstall complete. Please sign out or restart Windows.",
                    Constants.ProductDisplayName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppendLog("UNINSTALL ERROR: " + ex.Message);
                SetBusy(false, 0);
                MessageBox.Show(this, "Uninstall failed:\r\n\r\n" + ex.Message, Constants.ProductDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false, succeeded ? 100 : 0);
            }
        }
    }
}
