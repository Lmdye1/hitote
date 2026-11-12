using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Hitote
{
    internal static class Program
    {
        [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
        [STAThread]
        private static int Main(string[] args)
        {
            bool created;
            using (var mutex = new Mutex(true, "Local\\Hitote.OneHand.Keyboard", out created))
            {
                if (!created) { MessageBox.Show("ひとては起動済みです。通知領域のアイコンから開けます。", "ひとて"); return 0; }
                SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                try
                {
                    using (var keyboard = new KeyboardService())
                    using (var form = new MainForm(keyboard))
                    {
                        bool smoke = Array.IndexOf(args, "--smoke-test") >= 0;
                        if (smoke)
                        {
                            var timer = new System.Windows.Forms.Timer { Interval = 800 };
                            timer.Tick += delegate { timer.Stop(); timer.Dispose(); form.ExitApplication(); };
                            form.Shown += delegate { timer.Start(); };
                        }
                        Application.Run(form);
                    }
                    return 0;
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine(error);
                    if (Array.IndexOf(args, "--smoke-test") < 0) MessageBox.Show(error.Message, "ひとてを起動できませんでした", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
                finally { mutex.ReleaseMutex(); }
            }
        }
    }

    internal sealed class MainForm : Form
    {
        private static readonly Color Green = Color.FromArgb(35, 77, 66);
        private static readonly Color Cream = Color.FromArgb(245, 244, 238);
        private static readonly Color Lime = Color.FromArgb(228, 239, 195);
        private readonly KeyboardService keyboard;
        private readonly NotifyIcon tray;
        private readonly ToolStripMenuItem trayToggle;
        private readonly System.Windows.Forms.Timer refresh;
        private readonly Button toggle;
        private readonly Label status, state;
        private readonly Label[] rowLabels = new Label[10];
        private readonly Icon onIcon, offIcon;
        private bool exit, lastEnabled, resourcesDisposed;
        private string previousState;

        public MainForm(KeyboardService service)
        {
            keyboard = service;
            Text = "ひとて — 片手キーボード";
            ClientSize = new Size(700, 660);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Cream;
            ForeColor = Green;
            Font = new Font("Yu Gothic UI", 10);
            onIcon = MakeIcon(Green);
            offIcon = MakeIcon(Color.FromArgb(126, 134, 123));
            Icon = onIcon;

            AddLabel("ひとて", 28, 22, 220, 45, 27, FontStyle.Bold);
            AddLabel("DESKTOP / 物理キーボードで片手入力", 30, 73, 620, 24, 10, FontStyle.Regular);
            toggle = new Button { Location = new Point(30, 111), Size = new Size(640, 58), FlatStyle = FlatStyle.Flat,
                BackColor = Green, ForeColor = Color.White, Font = new Font(Font.FontFamily, 15, FontStyle.Bold), TabIndex = 0 };
            toggle.FlatAppearance.BorderSize = 0;
            toggle.Click += delegate { keyboard.SetEnabled(!keyboard.Enabled); RefreshState(); };
            Controls.Add(toggle);

            status = AddLabel("", 30, 181, 640, 28, 10, FontStyle.Regular);
            AddLabel("① IMEを「ひらがな・ローマ字入力」にして、入力先のアプリをクリック", 30, 216, 640, 25, 10, FontStyle.Regular);
            AddLabel("② 行のキー → A・S・D・F・G で文字を選択。例：W → S で「き」", 30, 242, 640, 25, 10, FontStyle.Regular);
            AddLabel("行を選ぶ", 30, 283, 300, 24, 10, FontStyle.Bold);
            AddLabel("母音を選ぶ", 370, 283, 300, 24, 10, FontStyle.Bold);
            for (int i = 0; i < InputEngine.Rows.Length; i++)
            {
                int x = 30 + (i % 3) * 105;
                int y = 315 + (i / 3) * 50;
                if (i == 9) x = 30;
                rowLabels[i] = AddLabel(InputEngine.Rows[i].Key + "   " + InputEngine.Rows[i].Characters[0], x, y, i == 9 ? 307 : 97, 42, 14, FontStyle.Regular);
                rowLabels[i].TextAlign = ContentAlignment.MiddleCenter;
                rowLabels[i].BackColor = Color.White;
            }
            AddLabel("A    S    D    F    G", 370, 318, 300, 32, 19, FontStyle.Bold);
            AddLabel("あ    い    う    え    お", 370, 355, 300, 28, 16, FontStyle.Regular);
            state = AddLabel("", 370, 404, 300, 100, 13, FontStyle.Regular);
            state.BackColor = Lime;
            state.Padding = new Padding(12);
            AddLabel("Space  変換・次候補     F / Enter  確定・改行     R / Backspace  削除", 30, 527, 640, 26, 10, FontStyle.Bold);
            AddLabel("T  濁点・半濁点     B  小文字     G  、     H  。     F2  ON / OFF", 30, 555, 640, 25, 10, FontStyle.Regular);
            var hide = new Button { Text = "通知領域にしまう", Location = new Point(30, 603), Size = new Size(170, 33), FlatStyle = FlatStyle.Flat };
            hide.Click += delegate { Hide(); };
            Controls.Add(hide);
            AddLabel("終了：通知領域の「ひ」を右クリック → 終了", 218, 608, 450, 25, 9, FontStyle.Regular);

            trayToggle = new ToolStripMenuItem("片手入力をON（F2）");
            trayToggle.Click += delegate { keyboard.SetEnabled(!keyboard.Enabled); RefreshState(); };
            var menu = new ContextMenuStrip();
            menu.Items.Add(trayToggle);
            menu.Items.Add("操作ガイドを開く", null, delegate { OpenWindow(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("終了", null, delegate { ExitApplication(); });
            tray = new NotifyIcon { Icon = offIcon, Text = "ひとて OFF — F2で片手入力", Visible = true, ContextMenuStrip = menu };
            tray.DoubleClick += delegate { OpenWindow(); };
            refresh = new System.Windows.Forms.Timer { Interval = 80 };
            refresh.Tick += delegate { keyboard.CheckForeground(); RefreshState(); };
            refresh.Start();
            FormClosing += delegate(object sender, FormClosingEventArgs e) {
                if (!exit && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            };
            Resize += delegate { if (WindowState == FormWindowState.Minimized) Hide(); };
            RefreshState();
        }

        private Label AddLabel(string text, int x, int y, int width, int height, float size, FontStyle style)
        {
            var label = new Label { Text = text, Location = new Point(x, y), Size = new Size(width, height), Font = new Font(Font.FontFamily, size, style) };
            Controls.Add(label);
            return label;
        }

        private void RefreshState()
        {
            bool enabled = keyboard.Enabled;
            string stateText = !enabled ? "通常のキーボード\nF2で片手入力を開始" : keyboard.Engine.SelectedRow != null
                ? keyboard.Engine.SelectedRow.Characters[0] + "行を選択中\n" + String.Join("   ", Array.ConvertAll(keyboard.Engine.SelectedRow.Characters.ToCharArray(), c => c.ToString()))
                : keyboard.Engine.LastKana != '\0' ? "入力した文字：" + keyboard.Engine.LastKana + "\nT：濁点　B：小文字" : "行のキーを押してください\nQ W E / A S D / Z X C / V";
            if (stateText != previousState)
            {
                state.Text = stateText;
                previousState = stateText;
                for (int i = 0; i < rowLabels.Length; i++) rowLabels[i].BackColor = keyboard.Engine.SelectedRow == InputEngine.Rows[i] ? Lime : Color.White;
            }
            toggle.Text = enabled ? "ON   片手入力中   /   F2でOFF" : "OFF   通常入力   /   F2でON";
            toggle.BackColor = enabled ? Green : Color.FromArgb(103, 116, 102);
            status.Text = keyboard.Error ?? (enabled ? "入力先のアプリで使えます。Ctrl・Alt・Shift・Winの組み合わせは通常どおり。" : "片手入力はOFFです。今のキー配列でそのまま入力できます。");
            if (enabled != lastEnabled)
            {
                tray.Icon = enabled ? onIcon : offIcon;
                tray.Text = enabled ? "ひとて ON — F2で通常入力" : "ひとて OFF — F2で片手入力";
                trayToggle.Text = enabled ? "片手入力をOFF（F2）" : "片手入力をON（F2）";
                lastEnabled = enabled;
            }
        }

        private void OpenWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }
        public void ExitApplication() { exit = true; keyboard.SetEnabled(false); Close(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !resourcesDisposed)
            {
                resourcesDisposed = true;
                if (refresh != null) refresh.Dispose();
                if (tray != null) { tray.Visible = false; if (tray.ContextMenuStrip != null) tray.ContextMenuStrip.Dispose(); tray.Dispose(); }
                if (onIcon != null) onIcon.Dispose();
                if (offIcon != null) offIcon.Dispose();
            }
            base.Dispose(disposing);
        }

        private static Icon MakeIcon(Color color)
        {
            using (var bitmap = new Bitmap(32, 32))
            using (var graphics = Graphics.FromImage(bitmap))
            using (var background = new SolidBrush(color))
            using (var foreground = new SolidBrush(Lime))
            using (var font = new Font("Yu Gothic UI", 20, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.FillEllipse(background, 0, 0, 31, 31);
                graphics.DrawString("ひ", font, foreground, 4, 2);
                IntPtr handle = bitmap.GetHicon();
                try { using (Icon borrowed = Icon.FromHandle(handle)) return (Icon)borrowed.Clone(); }
                finally { DestroyIcon(handle); }
            }
        }
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    }
}
