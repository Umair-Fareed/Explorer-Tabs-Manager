using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ExplorerTabManager
{
    public class SettingsForm : Form
    {
        private readonly SettingsManager settingsManager;

        private CheckBox chkAutoSave;
        private NumericUpDown numAutoSaveInterval;
        private CheckBox chkAutoRestore;
        private CheckBox chkStartWithWindows;
        private Button btnSave;
        private Button btnCancel;
        private LinkLabel lnkWebsite;

        private const string AppName    = "ExplorerTabManager";
        private const string StartupKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string WebsiteUrl = "https://suzagear.com/tabs";

        // Design tokens
        private static readonly Color Accent     = Color.FromArgb(0, 120, 212);
        private static readonly Color BgPage     = Color.White;
        private static readonly Color BgFooter   = Color.FromArgb(245, 245, 245);
        private static readonly Color BorderLine = Color.FromArgb(225, 225, 225);
        private static readonly Color TextPrimary   = Color.FromArgb(25,  25,  25);
        private static readonly Color TextSecondary = Color.FromArgb(100, 100, 100);
        private static readonly Font  FontBase   = new Font("Segoe UI", 9.5f);
        private static readonly Font  FontBold   = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
        private static readonly Font  FontSmall  = new Font("Segoe UI", 8.5f);

        public SettingsForm(SettingsManager settingsManager)
        {
            this.settingsManager = settingsManager;
            InitializeComponents();
            LoadSettings();
        }

        private void InitializeComponents()
        {
            this.Text            = "Explorer Tabs Manager — Settings";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox     = false;
            this.MinimizeBox     = false;
            this.StartPosition   = FormStartPosition.CenterScreen;
            this.Font            = FontBase;
            this.BackColor       = BgPage;

            const int W    = 460;   // client width
            const int padX = 24;
            int y          = 22;

            // ── Auto-Save ────────────────────────────────────────────────────
            AddSectionHeader("Auto-Save", padX, ref y);

            chkAutoSave = MakeCheckBox("Automatically save open tabs", padX, y);

            var lblEvery = MakeLabel("every", padX + 238, y + 2, TextSecondary, FontSmall);

            numAutoSaveInterval = new NumericUpDown
            {
                Location = new Point(padX + 272, y - 1),
                Width    = 50,
                Height   = 24,
                Minimum  = 2,
                Maximum  = 300,
                Value    = 5,
                Font     = FontBase,
                TextAlign = HorizontalAlignment.Center
            };

            var lblSec = MakeLabel("sec", padX + 326, y + 2, TextSecondary, FontSmall);

            y += 44;

            // ── Auto-Restore ─────────────────────────────────────────────────
            AddSectionHeader("Auto-Restore", padX, ref y);

            chkAutoRestore = MakeCheckBox("Restore tabs when Explorer is reopened", padX, y);
            y += 26;

            var lblNote = new Label
            {
                Text      = "Tabs are saved automatically. When you close all Explorer windows\n" +
                            "and reopen Explorer, all your tabs are restored.",
                Location  = new Point(padX + 22, y),
                Size      = new Size(W - padX * 2 - 22, 34),
                ForeColor = TextSecondary,
                Font      = FontSmall,
                BackColor = Color.Transparent
            };

            y += 48;

            // ── General ──────────────────────────────────────────────────────
            AddSectionHeader("General", padX, ref y);

            chkStartWithWindows = MakeCheckBox("Start with Windows", padX, y);
            y += 40;

            // ── Footer panel ─────────────────────────────────────────────────
            const int footerH = 56;
            int totalClientH  = y + footerH;

            this.ClientSize = new Size(W, totalClientH);

            var footer = new Panel
            {
                Location  = new Point(0, y),
                Size      = new Size(W, footerH),
                BackColor = BgFooter
            };
            footer.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(BorderLine), 0, 0, ((Panel)s).Width, 0);
            };

            // ℹ link
            lnkWebsite = new LinkLabel
            {
                Text      = "ℹ  suzagear.com/tabs",
                Location  = new Point(padX, 18),
                AutoSize  = true,
                Font      = FontSmall,
                LinkColor = Accent,
                ActiveLinkColor = Color.FromArgb(0, 84, 166)
            };
            lnkWebsite.LinkBehavior = LinkBehavior.HoverUnderline;
            lnkWebsite.Click += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo(WebsiteUrl) { UseShellExecute = true }); }
                catch { }
            };

            // Cancel button — outlined
            btnCancel = new Button
            {
                Text      = "Cancel",
                Location  = new Point(W - 178, 14),
                Size      = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                Font      = FontBase,
                BackColor = Color.White,
                ForeColor = TextPrimary,
                Cursor    = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(190, 190, 190);
            btnCancel.FlatAppearance.BorderSize  = 1;
            btnCancel.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 245, 245);
            btnCancel.Click += (s, e) => this.Close();

            // Save button — filled accent
            btnSave = new Button
            {
                Text      = "Save",
                Location  = new Point(W - 92, 14),
                Size      = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                Font      = FontBold,
                BackColor = Accent,
                ForeColor = Color.White,
                Cursor    = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize  = 0;
            btnSave.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 102, 180);
            btnSave.Click += BtnSave_Click;

            footer.Controls.AddRange(new Control[] { lnkWebsite, btnCancel, btnSave });

            // Add all top-level controls
            this.Controls.AddRange(new Control[]
            {
                chkAutoSave, lblEvery, numAutoSaveInterval, lblSec,
                chkAutoRestore, lblNote,
                chkStartWithWindows,
                footer
            });
        }

        // ── Section header: accent bar + bold label + rule line ──────────────
        private void AddSectionHeader(string title, int x, ref int y)
        {
            // 3px left accent bar
            var bar = new Panel
            {
                Location  = new Point(x, y + 1),
                Size      = new Size(3, 16),
                BackColor = Accent
            };

            var lbl = new Label
            {
                Text      = title,
                Location  = new Point(x + 8, y),
                AutoSize  = true,
                Font      = FontBold,
                ForeColor = TextPrimary
            };

            // Horizontal rule to the right of the title
            var rule = new Panel
            {
                Location  = new Point(x + 8 + lbl.PreferredWidth + 10, y + 9),
                Size      = new Size(this.ClientSize.Width - x - 8 - lbl.PreferredWidth - x - 10, 1),
                BackColor = BorderLine
            };

            this.Controls.Add(bar);
            this.Controls.Add(lbl);
            this.Controls.Add(rule);

            y += 28;
        }

        private static CheckBox MakeCheckBox(string text, int x, int y)
        {
            return new CheckBox
            {
                Text      = text,
                Location  = new Point(x, y),
                AutoSize  = true,
                Font      = FontBase,
                ForeColor = TextPrimary,
                Cursor    = Cursors.Hand
            };
        }

        private static Label MakeLabel(string text, int x, int y, Color color, Font font)
        {
            return new Label
            {
                Text      = text,
                Location  = new Point(x, y),
                AutoSize  = true,
                Font      = font,
                ForeColor = color
            };
        }

        // ── Load / Save ──────────────────────────────────────────────────────
        private void LoadSettings()
        {
            var s = settingsManager.Settings;
            chkAutoSave.Checked       = s.AutoSaveEnabled;
            numAutoSaveInterval.Value = Clamp((decimal)s.AutoSaveIntervalSeconds,
                                              numAutoSaveInterval.Minimum,
                                              numAutoSaveInterval.Maximum);
            chkAutoRestore.Checked      = s.AutoRestoreOnStartup;
            chkStartWithWindows.Checked = IsRegisteredAtStartup();
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            var s = settingsManager.Settings;
            s.AutoSaveEnabled         = chkAutoSave.Checked;
            s.AutoSaveIntervalSeconds = (int)numAutoSaveInterval.Value;
            s.AutoRestoreOnStartup    = chkAutoRestore.Checked;
            s.StartWithWindows        = chkStartWithWindows.Checked;

            settingsManager.SaveSettings();
            ApplyStartWithWindows(chkStartWithWindows.Checked);

            MessageBox.Show("Settings saved.", "Explorer Tabs Manager",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.Close();
        }

        // ── Start-with-Windows helpers ────────────────────────────────────────
        private bool IsRegisteredAtStartup()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StartupKey, false);
                return key?.GetValue(AppName) != null;
            }
            catch { return false; }
        }

        private void ApplyStartWithWindows(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StartupKey, true);
                if (key == null) return;
                if (enable)
                    key.SetValue(AppName, $"\"{Application.ExecutablePath}\"");
                else
                    key.DeleteValue(AppName, throwOnMissingValue: false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not update startup entry:\n{ex.Message}",
                                "Explorer Tabs Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static decimal Clamp(decimal v, decimal min, decimal max)
            => v < min ? min : v > max ? max : v;
    }
}
