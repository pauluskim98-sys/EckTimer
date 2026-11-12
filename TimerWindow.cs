using System;
using System.Drawing;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using System.IO;
using System.Windows.Forms;

namespace EckTimer
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TimerWindow());
        }
    }

    internal sealed class TimerWindow : Form
    {
        private Color Ink = Color.FromArgb(21, 22, 27);
        private Color PanelColor = Color.FromArgb(43, 45, 55);
        private Color Paper = Color.FromArgb(246, 247, 252);
        private Color Muted = Color.FromArgb(176, 180, 196);
        private Color Accent = Color.FromArgb(194, 197, 255);
        private static readonly string[] Modes = { "Timer", "Stoppuhr", "Wecker" };
        private readonly Timekeeping time = new Timekeeping();
        private readonly Notifications notifications = new Notifications();
        private System.Threading.Timer refresh;
        private int refreshQueued;
        private readonly Button[] tabs = new Button[3];
        private readonly TextBox durationEditor;
        private readonly List<int> presets = new List<int>();
        private readonly ToolStripMenuItem presetMenu = new ToolStripMenuItem("Timer-Vorlagen");
        private Point? moveOrigin;
        private Rectangle digitBounds;
        private float digitFontSize;
        private Font editorFont;
        private string originalEditText;
        private int resizeEdge;
        private Point resizeOrigin;
        private Rectangle resizeBounds;
        private Size miniSize = new Size(100, 34);
        private readonly Button action, reset, minimize, close, templates, settings;
        private readonly FlowLayoutPanel presetStrip = new FlowLayoutPanel { AutoScroll = true, WrapContents = false };
        private readonly ToolStripMenuItem themeMenu = new ToolStripMenuItem("Dunkle Ansicht") { CheckOnClick = true };
        private readonly CheckBox sound, blink, pinned;
        private readonly DateTimePicker alarm;
        private readonly ToolStripMenuItem menuPin;
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private readonly string settingsPath;
        private readonly Font digital = new Font("Bahnschrift", 48, FontStyle.Regular, GraphicsUnit.Pixel);
        private Rectangle timeArea;
        private Rectangle normalSize;
        private bool compact, settingUp = true;
        private int mode;
        private string display = "";
        private string status = "";
        private bool bright;

        public TimerWindow()
        {
            Text = "EckTimer";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
            BackColor = Ink;
            ForeColor = Paper;
            Font = new Font("Segoe UI", 10);
            var overridePath = Environment.GetEnvironmentVariable("ECKTIMER_SETTINGS");
            settingsPath = overridePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EckTimer", "settings.txt");
            time.CountdownSignal = time.AlarmSignal = Signal.Sound | Signal.Blink;

            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                tabs[i] = MakeButton(Modes[i], () => SelectMode(index));
            }
            durationEditor = new TextBox
            {
                Visible = false, BorderStyle = BorderStyle.None, TextAlign = HorizontalAlignment.Center,
                BackColor = PanelColor, ForeColor = Paper, AccessibleName = "Countdown-Dauer in Stunden, Minuten und Sekunden"
            };
            durationEditor.KeyDown += EditDurationKey;
            durationEditor.LostFocus += (sender, args) => FinishDurationEdit(true);
            Controls.Add(durationEditor);
            presetMenu.DropDownOpening += (sender, args) => PopulatePresets();
            alarm = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm",
                ShowUpDown = true, AccessibleName = "Weckzeit"
            };
            Controls.Add(alarm);
            templates = MakeButton("+ Speichern", SaveCurrentPreset);
            Controls.Add(presetStrip);
            presetStrip.BackColor = Ink;
            action = MakeButton("Start", RunAction);
            reset = MakeButton("Zurücksetzen", ResetMode);
            minimize = MakeButton("–", () => WindowState = FormWindowState.Minimized);
            minimize.AccessibleName = "Minimieren";
            close = MakeButton("×", Close);
            close.AccessibleName = "Schließen";
            settings = MakeButton("Einstellungen", ShowSettings);
            sound = MakeCheck("Ton abspielen", SaveSignal);
            blink = MakeCheck("Fenster blinken lassen", SaveSignal);
            pinned = MakeCheck("Immer im Vordergrund", () => { TopMost = pinned.Checked; menuPin.Checked = TopMost; });

            for (int i = 0; i < Modes.Length; i++)
            {
                int index = i;
                menu.Items.Add(Modes[i], null, (sender, args) => SelectMode(index));
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Start / Pause / Wecker umschalten", null, (sender, args) => RunAction());
            menu.Items.Add("Zurücksetzen", null, (sender, args) => ResetMode());
            menu.Items.Add("Signal stoppen", null, (sender, args) => Dismiss());
            menu.Items.Add(new ToolStripSeparator());
            menuPin = new ToolStripMenuItem("Immer im Vordergrund") { CheckOnClick = true };
            menuPin.CheckedChanged += (sender, args) => pinned.Checked = menuPin.Checked;
            menu.Items.Add(menuPin);
            menu.Items.Add(presetMenu);
            menu.Items.Add("Einstellungen", null, (sender, args) => ShowSettings());
            menu.Items.Add("Bedienelemente anzeigen", null, (sender, args) => Expand());
            menu.Items.Add("Minimieren", null, (sender, args) => WindowState = FormWindowState.Minimized);
            menu.Items.Add("Schließen", null, (sender, args) => Close());
            ContextMenuStrip = menu;
            presetMenu.DropDownItems.Add("Vorlagen");
            durationEditor.ContextMenuStrip = new ContextMenuStrip();
            durationEditor.ContextMenuStrip.Items.Add("Dauer übernehmen", null, (sender, args) => FinishDurationEdit(true));

            time.SetDuration(TimeSpan.FromMinutes(5));
            alarm.Value = DateTime.Today.AddHours(7);
            LoadSettings();
            themeMenu.CheckedChanged += (sender, args) => { ApplyTheme(); RefreshPresetStrip(); SaveSettings(); };
            ApplyTheme();
            RefreshPresetStrip();
            settingUp = false;
            SelectMode(mode);

        }



        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            bool openedInNormalSize = Width >= 310 && Height >= 280;
            Arrange();
            if (openedInNormalSize && compact) Expand();
            refresh = new System.Threading.Timer(QueueRefresh, null, 0, 50);
        }

        private void QueueRefresh(object ignored)
        {
            if (System.Threading.Interlocked.Exchange(ref refreshQueued, 1) != 0) return;
            try
            {
                BeginInvoke((MethodInvoker)(() =>
                {
                    System.Threading.Interlocked.Exchange(ref refreshQueued, 0);
                    if (!IsDisposed) UpdateDisplay();
                }));
            }
            catch (InvalidOperationException)
            {
                System.Threading.Interlocked.Exchange(ref refreshQueued, 0);
            }
        }

        private Button MakeButton(string caption, Action handler)
        {
            var button = new Button { Text = caption, FlatStyle = FlatStyle.Flat, BackColor = PanelColor, ForeColor = Paper };
            button.FlatAppearance.BorderSize = 0;
            button.Cursor = Cursors.Hand;
            button.Click += (sender, args) => handler();
            Controls.Add(button);
            return button;
        }

        private CheckBox MakeCheck(string caption, Action handler)
        {
            var box = new CheckBox { Text = caption, ForeColor = Paper };
            box.CheckedChanged += (sender, args) => handler();
            Controls.Add(box);
            return box;
        }

        private void SelectMode(int index)
        {
            FinishDurationEdit(true);
            mode = index;
            settingUp = true;
            var signal = mode == 2 ? time.AlarmSignal : time.CountdownSignal;
            sound.Checked = (signal & Signal.Sound) != 0;
            blink.Checked = (signal & Signal.Blink) != 0;
            settingUp = false;
            UpdateDisplay();
            Arrange();
        }

        private void SaveSignal()
        {
            if (settingUp) return;
            Signal signal = (sound.Checked ? Signal.Sound : Signal.None) | (blink.Checked ? Signal.Blink : Signal.None);
            if (mode == 2) time.AlarmSignal = signal;
            else if (mode == 0) time.CountdownSignal = signal;
        }

        private void ShowSettings()
        {
            using (var dialog = new Form
            {
                Text = "Einstellungen", StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog, ClientSize = new Size(288, 164),
                ShowInTaskbar = false, MinimizeBox = false, MaximizeBox = false, Font = Font,
                BackColor = Ink, ForeColor = Paper
            })
            {
                var day = new RadioButton { Text = "Tagmodus", Checked = !themeMenu.Checked };
                var night = new RadioButton { Text = "Nachtmodus", Checked = themeMenu.Checked };
                var pin = new CheckBox { Text = "Immer im Vordergrund", Checked = pinned.Checked };
                var done = new Button { Text = "Übernehmen", DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat,
                    BackColor = PanelColor };
                day.SetBounds(16, 14, 250, 24);
                night.SetBounds(16, 43, 250, 24);
                pin.SetBounds(16, 76, 250, 24);
                done.SetBounds(155, 119, 116, 29);
                dialog.Controls.AddRange(new Control[] { day, night, pin, done });
                dialog.AcceptButton = done;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                themeMenu.Checked = night.Checked;
                pinned.Checked = pin.Checked;
                SaveSettings();
            }
        }

        private void ApplyTheme()
        {
            bool dark = themeMenu.Checked;
            Ink = dark ? Color.FromArgb(21, 22, 27) : Color.FromArgb(251, 251, 253);
            PanelColor = dark ? Color.FromArgb(43, 45, 55) : Color.FromArgb(232, 233, 241);
            Paper = dark ? Color.FromArgb(246, 247, 252) : Color.FromArgb(23, 24, 32);
            Muted = dark ? Color.FromArgb(176, 180, 196) : Color.FromArgb(82, 85, 103);
            Accent = dark ? Color.FromArgb(194, 197, 255) : Color.FromArgb(169, 174, 251);
            BackColor = Ink;
            ForeColor = Paper;
            foreach (Control control in Controls)
            {
                control.ForeColor = Paper;
                if (control is Button || control is TextBox) control.BackColor = PanelColor;
            }
            Arrange();
        }

        private void SaveCurrentPreset()
        {
            FinishDurationEdit(true);
            int seconds = (int)time.Duration.TotalSeconds;
            if (seconds <= 0 || presets.Contains(seconds)) return;
            if (!SavePreset(seconds))
                MessageBox.Show(this, "Die Vorlage konnte nicht gespeichert werden.", "EckTimer",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private bool SavePreset(int seconds)
        {
            presets.Add(seconds);
            if (!SaveSettings())
            {
                presets.Remove(seconds);
                return false;
            }
            RefreshPresetStrip();
            presetStrip.ScrollControlIntoView(presetStrip.Controls[presetStrip.Controls.Count - 1]);
            return true;
        }

        private void RefreshPresetStrip()
        {
            while (presetStrip.Controls.Count > 0)
            {
                var old = presetStrip.Controls[0];
                old.ContextMenuStrip.Dispose();
                old.Dispose();
            }
            foreach (int seconds in presets)
            {
                var button = new Button
                {
                    Text = FormatTime(TimeSpan.FromSeconds(seconds), false),
                    FlatStyle = FlatStyle.Flat, BackColor = PanelColor,
                    ForeColor = Paper, AutoEllipsis = true
                };
                button.FlatAppearance.BorderSize = 0;
                button.Click += (sender, args) =>
                {
                    if (!time.CountdownRunning)
                    {
                        time.SetDuration(TimeSpan.FromSeconds(seconds));
                        UpdateDisplay();
                    }
                };
                var commands = new ContextMenuStrip();
                commands.Items.Add("Vorlage löschen", null, (sender, args) =>
                {
                    presets.Remove(seconds);
                    RefreshPresetStrip();
                    SaveSettings();
                });
                button.ContextMenuStrip = commands;
                presetStrip.Controls.Add(button);
            }
        }

        private void PopulatePresets()
        {
            presetMenu.DropDownItems.Clear();
            var save = new ToolStripMenuItem("Aktuelle Dauer speichern");
            save.Click += (sender, args) => SaveCurrentPreset();
            presetMenu.DropDownItems.Add(save);
            var remove = new ToolStripMenuItem("Vorlage löschen") { Enabled = presets.Count > 0 };
            foreach (int seconds in presets)
            {
                var value = TimeSpan.FromSeconds(seconds);
                var name = FormatTime(value, false);
                var load = new ToolStripMenuItem(name) { Enabled = !time.CountdownRunning };
                load.Click += (sender, args) =>
                {
                    time.SetDuration(value);
                    SelectMode(0);
                };
                presetMenu.DropDownItems.Add(load);
                remove.DropDownItems.Add(name, null, (sender, args) => { presets.Remove(seconds); RefreshPresetStrip(); SaveSettings(); });
            }
            presetMenu.DropDownItems.Add(remove);
        }

        private void ToggleCompact()
        {
            moveOrigin = null;
            Capture = false;
            FinishDurationEdit(false);
            UpdateDisplay();
            bool shrinking = !compact;
            if (mode == 0 && time.CountdownRunning != shrinking) time.ToggleCountdown();
            if (mode == 1 && time.StopwatchRunning != shrinking) time.ToggleStopwatch();
            if (compact)
            {
                miniSize = Size;
                Expand();
            }
            else
            {
                Bounds = KeepVisible(new Rectangle(Location, miniSize));
            }
            UpdateDisplay();
        }

        private void BeginDurationEdit(Point location)
        {
            var area = timeArea;
            originalEditText = display;
            durationEditor.Text = originalEditText;
            durationEditor.MaxLength = 9;
            var previousFont = editorFont;
            editorFont = new Font(digital.FontFamily, digitFontSize, FontStyle.Regular, GraphicsUnit.Pixel);
            durationEditor.Font = editorFont;
            if (previousFont != null) previousFont.Dispose();
            durationEditor.BackColor = Ink;
            durationEditor.ForeColor = Paper;
            int height = durationEditor.PreferredHeight;
            int width = Math.Min(area.Width, digitBounds.Width + 8);
            durationEditor.SetBounds(area.X + (area.Width - width) / 2,
                digitBounds.Y + (digitBounds.Height - height) / 2, width, height);
            durationEditor.Show();
            durationEditor.BringToFront();
            durationEditor.Focus();
            var caret = durationEditor.GetCharIndexFromPosition(new Point(location.X - durationEditor.Left, height / 2));
            durationEditor.Select(caret, 0);
            Invalidate();
        }

        private void EditDurationKey(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter && e.KeyCode != Keys.Escape) return;
            e.SuppressKeyPress = true;
            FinishDurationEdit(e.KeyCode == Keys.Enter);
        }

        private void FinishDurationEdit(bool commit)
        {
            if (!durationEditor.Visible) return;
            int seconds;
            if (commit && durationEditor.Text != originalEditText && TryParseDuration(durationEditor.Text, out seconds))
                time.SetDuration(TimeSpan.FromSeconds(seconds));
            durationEditor.Hide();
            UpdateDisplay();
            Invalidate();
        }
        private static bool TryParseDuration(string text, out int total)
        {
            total = 0;
            var parts = text.Trim().Split(':');
            if (parts.Length > 3) return false;
            foreach (string part in parts)
            {
                int value;
                if (!int.TryParse(part, out value) || value < 0 || value > 3599999
                    || (total > 0 && value >= 60) || (long)total * 60 + value > 3599999)
                    return false;
                total = total * 60 + value;
            }
            return parts.Length == 1 || (int.Parse(parts[parts.Length - 1]) < 60
                && (parts.Length == 2 || int.Parse(parts[1]) < 60));
        }

        private void RunAction()
        {
            UpdateDisplay();
            if (notifications.Active != Signal.None) { Dismiss(); return; }
            switch (mode)
            {
                case 0: time.ToggleCountdown(); break;
                case 1: time.ToggleStopwatch(); break;
                case 2:
                    if (time.AlarmDue.HasValue) time.DisarmAlarm();
                    else time.ArmAlarm(alarm.Value.TimeOfDay);
                    break;
            }
            UpdateDisplay();
        }

        private void ResetMode()
        {
            if (mode == 0) time.ResetCountdown();
            if (mode == 1) time.ResetStopwatch();
            UpdateDisplay();
        }

        private void Dismiss()
        {
            notifications.Dismiss();
            UpdateDisplay();
        }

        private static string FormatTime(TimeSpan value, bool roundUp)
        {
            long seconds = (long)(roundUp ? Math.Ceiling(value.TotalSeconds) : Math.Floor(value.TotalSeconds));
            return string.Format("{0:00}:{1:00}:{2:00}", seconds / 3600, seconds / 60 % 60, seconds % 60);
        }

        private void UpdateDisplay()
        {
            notifications.Raise(time.Check());
            if (IsHandleCreated) notifications.UpdateTaskbar(Handle, WindowState == FormWindowState.Minimized);
            bool signaling = notifications.Active != Signal.None;
            string current, detail, verb;
            if (mode == 0)
            {
                current = FormatTime(time.Remaining, true);
                detail = time.CountdownRunning ? "Läuft" : time.Remaining == time.Duration ? "Bereit" :
                    time.Remaining == TimeSpan.Zero ? "Abgelaufen" : "Pausiert";
                verb = time.CountdownRunning ? "Pause" : detail == "Pausiert" ? "Fortsetzen" : "Start";

            }
            else if (mode == 1)
            {
                current = FormatTime(time.Elapsed, false);
                detail = time.StopwatchRunning ? "Läuft" : time.Elapsed > TimeSpan.Zero ? "Pausiert" : "Bereit";
                verb = time.StopwatchRunning ? "Pause" : detail == "Pausiert" ? "Fortsetzen" : "Start";
            }
            else
            {
                current = DateTime.Now.ToString("HH:mm:ss");
                bool armed = time.AlarmDue.HasValue;
                detail = armed ? "Wecker " + (time.AlarmDue.Value.Date == DateTime.Today ? "heute" : "morgen")
                    + " um " + time.AlarmDue.Value.ToString("HH:mm") : "Wecker aus";
                verb = armed ? "Deaktivieren" : "Aktivieren";
                alarm.Enabled = !armed;
            }

            int currentDuration = (int)time.Duration.TotalSeconds;
            templates.Enabled = durationEditor.Visible || (currentDuration > 0 && !presets.Contains(currentDuration));
            foreach (Control preset in presetStrip.Controls) preset.Enabled = !time.CountdownRunning;
            action.Text = signaling ? "Signal stoppen" : verb;
            action.Enabled = signaling || mode != 0 || time.Duration > TimeSpan.Zero;
            if (signaling && !compact) detail = "Signal aktiv";
            bool nextBright = notifications.Bright;
            if (display != current || status != detail || bright != nextBright)
            {
                display = current;
                status = detail;
                bright = nextBright;
                Invalidate();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (action == null || WindowState == FormWindowState.Minimized) return;
            Arrange();
        }

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr window);

        private float LayoutScale
        {
            get { return IsHandleCreated ? Math.Max(1f, GetDpiForWindow(Handle) / 96f) : 1f; }
        }

        private void Arrange()
        {
            ArrangeAtScale(LayoutScale);
        }

        private void ArrangeAtScale(float scale)
        {
            int w = (int)(ClientSize.Width / scale), h = (int)(ClientSize.Height / scale);
            Func<int, int> px = value => (int)Math.Round(value * scale);
            Action<Control, int, int, int, int> place = (control, x, y, width, height) =>
                control.SetBounds(px(x), px(y), px(width), px(height));
            compact = w < 310 || h < 280;

            if (compact)
            {
                foreach (Control child in Controls) child.Hide();
                timeArea = new Rectangle(4, 3, Math.Max(1, ClientSize.Width - 8), Math.Max(1, ClientSize.Height - 6));
                Invalidate();
                return;
            }

            templates.Visible = mode == 0;
            presetStrip.Visible = mode == 0;
            presetStrip.BackColor = Ink;
            int inner = Math.Min(520, w - 44);
            int padding = (w - inner) / 2;
            int contentRight = padding + inner;
            place(templates, contentRight - 124, 86, 124, 30);
            place(presetStrip, padding, 86, inner - 131, 53);
            foreach (Control preset in presetStrip.Controls)
            {
                preset.Size = new Size(px(96), px(30));
                preset.Margin = new Padding(0, 0, px(5), 0);
            }
            settings.Show();
            place(settings, w - 188, 13, 104, 25);
            action.Show();
            minimize.Show();
            close.Show();
            pinned.Show();
            if (resizeEdge == 0) normalSize = Bounds;
            place(minimize, w - 77, 13, 28, 25);
            place(close, w - 43, 13, 28, 25);

            int tabWidth = (inner - 12) / 3;
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i].Show();
                place(tabs[i], padding + i * (tabWidth + 6), 48, tabWidth, 32);
                tabs[i].BackColor = mode == i ? PanelColor : Ink;
                tabs[i].ForeColor = mode == i ? Paper : Muted;
            }

            int bottom = h - (mode == 2 ? 166 : 118);
            int timeTop = mode == 0 ? (presets.Count == 0 ? 120 : 141) : 83;
            timeArea = new Rectangle(px(padding), px(timeTop), px(inner), px(bottom - timeTop - 5));
            int controlsTop = bottom + (mode == 2 ? 54 : 6);
            alarm.Visible = mode == 2;
            place(alarm, padding, bottom + 22, inner, 24);
            place(action, padding, controlsTop, mode == 2 ? inner : (inner - 8) / 2, 34);
            action.BackColor = Accent;
            action.ForeColor = Color.FromArgb(23, 24, 32);
            reset.Visible = mode != 2;
            place(reset, padding + (inner - 8) / 2 + 8, controlsTop, inner - (inner - 8) / 2 - 8, 34);
            sound.Visible = blink.Visible = mode != 1;
            place(sound, padding, controlsTop + 42, 123, 26);
            place(blink, padding + 126, controlsTop + 42, inner - 126, 26);
            place(pinned, padding, controlsTop + 71, inner, 26);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var graphics = e.Graphics;
            Color background = bright ? Accent : Ink;
            Color foreground = bright ? Color.FromArgb(23, 24, 32) : Paper;
            graphics.Clear(background);
            if (!compact)
            {
                TextRenderer.DrawText(graphics, "EckTimer", Font, new Rectangle((int)(18 * LayoutScale), (int)(13 * LayoutScale), (int)(105 * LayoutScale), (int)(25 * LayoutScale)),
                    bright ? Color.FromArgb(23, 24, 32) : Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            var area = timeArea;
            int captionHeight = (int)(20 * LayoutScale);
            if (!compact && area.Height >= 2 * captionHeight + 7)
            {
                TextRenderer.DrawText(graphics, status, Font, new Rectangle(area.X, area.Bottom - captionHeight, area.Width, captionHeight),
                    bright ? Color.FromArgb(23, 24, 32) : Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                area.Height -= captionHeight;
            }
            if (!durationEditor.Visible) DrawTime(graphics, area, foreground);
        }

        private void DrawTime(Graphics graphics, Rectangle area, Color color)
        {
            if (area.Width < 1 || area.Height < 1 || display.Length == 0) return;
            const TextFormatFlags measure = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            var natural = TextRenderer.MeasureText(graphics, display, digital, Size.Empty, measure);
            float scale = Math.Min((float)area.Width / natural.Width, (float)area.Height / natural.Height);
            digitFontSize = Math.Max(1, (float)Math.Floor(digital.Size * scale * (compact ? 1f : .84f)));
            using (var font = new Font(digital.FontFamily, digitFontSize,
                FontStyle.Regular, GraphicsUnit.Pixel))
            {
                var size = TextRenderer.MeasureText(graphics, display, font, Size.Empty, measure);
                digitBounds = Rectangle.Intersect(area, new Rectangle(
                    area.X + (area.Width - size.Width) / 2, area.Y + (area.Height - size.Height) / 2,
                    size.Width, size.Height));
                TextRenderer.DrawText(graphics, display, font, area, color, measure
                    | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keys)
        {
            if (keys == Keys.Escape && notifications.Active != Signal.None) { Dismiss(); return true; }
            if (compact && keys == Keys.Space) { RunAction(); return true; }
            if (keys == (Keys.Control | Keys.M)) { WindowState = FormWindowState.Minimized; return true; }
            return base.ProcessCmdKey(ref msg, keys);
        }

        private void Expand()
        {
            float scale = LayoutScale;
            var size = normalSize.Size;
            if (size.Width < 310 * scale || size.Height < 280 * scale)
                size = new Size((int)(412 * scale), (int)(375 * scale));
            var target = new Rectangle(Location, size);
            Bounds = KeepVisible(target);
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int disabled = 1;
            DwmSetWindowAttribute(Handle, 2, ref disabled, sizeof(int));
            int noBorder = -2;
            DwmSetWindowAttribute(Handle, 34, ref noBorder, sizeof(int));
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.Style |= 0x00020000 | 0x00080000;
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int ncCalcSize = 0x83, ncHitTest = 0x84, getMinMax = 0x24;
            if (m.Msg == ncCalcSize)
            {
                m.Result = IntPtr.Zero;
                return;
            }
            if (m.Msg == 0x02E0)
            {
                base.WndProc(ref m);
                if (settings != null) Arrange();
                return;
            }
            if (m.Msg == getMinMax)
            {
                base.WndProc(ref m);
                System.Runtime.InteropServices.Marshal.WriteInt32(m.LParam, 24, 72);
                System.Runtime.InteropServices.Marshal.WriteInt32(m.LParam, 28, 28);
                return;
            }
            if (m.Msg == ncHitTest)
            {
                m.Result = (IntPtr)1;
                return;
            }
            base.WndProc(ref m);
        }

        private int GetResizeEdge(Point p)
        {
            bool left = p.X < 5, right = p.X >= ClientSize.Width - 5;
            bool top = p.Y < 5, bottom = p.Y >= ClientSize.Height - 5;
            return top ? (left ? 13 : right ? 14 : 12) :
                bottom ? (left ? 16 : right ? 17 : 15) : left ? 10 : right ? 11 : 1;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (notifications.Active != Signal.None) { Dismiss(); return; }
            if (e.Clicks == 2)
            {
                if (digitBounds.Contains(e.Location))
                {
                    if (mode == 0) BeginDurationEdit(e.Location);
                }
                else
                    ToggleCompact();
                return;
            }
            int edge = GetResizeEdge(e.Location);
            if (edge != 1)
            {
                resizeEdge = edge;
                resizeOrigin = PointToScreen(e.Location);
                resizeBounds = Bounds;
                Capture = true;
                return;
            }
            moveOrigin = e.Location;
            Capture = true;
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int edge = resizeEdge == 0 ? GetResizeEdge(e.Location) : resizeEdge;
            Cursor = edge == 10 || edge == 11 ? Cursors.SizeWE
                : edge == 12 || edge == 15 ? Cursors.SizeNS
                : edge == 13 || edge == 17 ? Cursors.SizeNWSE
                : edge == 14 || edge == 16 ? Cursors.SizeNESW : Cursors.Default;
            if (resizeEdge != 0 && e.Button == MouseButtons.Left)
            {
                var mouse = PointToScreen(e.Location);
                int dx = mouse.X - resizeOrigin.X, dy = mouse.Y - resizeOrigin.Y;
                var b = resizeBounds;
                int left = edge == 10 || edge == 13 || edge == 16 ? Math.Min(b.Right - 72, b.Left + dx) : b.Left;
                int right = edge == 11 || edge == 14 || edge == 17 ? Math.Max(b.Left + 72, b.Right + dx) : b.Right;
                int top = edge == 12 || edge == 13 || edge == 14 ? Math.Min(b.Bottom - 28, b.Top + dy) : b.Top;
                int bottom = edge == 15 || edge == 16 || edge == 17 ? Math.Max(b.Top + 28, b.Bottom + dy) : b.Bottom;
                Bounds = Rectangle.FromLTRB(left, top, right, bottom);
                return;
            }
            if (!moveOrigin.HasValue || e.Button != MouseButtons.Left) return;
            var start = moveOrigin.Value;
            var threshold = SystemInformation.DragSize;
            if (Math.Abs(start.X - e.X) < threshold.Width / 2 && Math.Abs(start.Y - e.Y) < threshold.Height / 2) return;
            moveOrigin = null;
            var point = PointToScreen(e.Location);
            ReleaseCapture();
            SendMessage(Handle, 0xA1, (IntPtr)2, (IntPtr)((point.X & 65535) | (point.Y << 16)));
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (Capture) return;
            if (resizeEdge != 0)
            {
                resizeEdge = 0;
                KeepWindowOnScreen();
                if (!compact) normalSize = Bounds;
            }
            moveOrigin = null;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            moveOrigin = null;
            bool resized = resizeEdge != 0;
            resizeEdge = 0;
            base.OnMouseUp(e);
            if (resized)
            {
                KeepWindowOnScreen();
                if (!compact) normalSize = Bounds;
            }
        }

        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);
            KeepWindowOnScreen();
        }

        private void KeepWindowOnScreen()
        {
            if (WindowState != FormWindowState.Normal) return;
            var visible = KeepVisible(Bounds);
            if (visible != Bounds) Bounds = visible;
        }

        private static Rectangle KeepVisible(Rectangle bounds)
        {
            var work = Screen.FromRectangle(bounds).WorkingArea;
            bounds.Width = Math.Min(work.Width, Math.Max(72, bounds.Width));
            bounds.Height = Math.Min(work.Height, Math.Max(28, bounds.Height));
            bounds.X = Math.Max(work.Left, Math.Min(bounds.X, work.Right - bounds.Width));
            bounds.Y = Math.Max(work.Top, Math.Min(bounds.Y, work.Bottom - bounds.Height));
            return bounds;
        }

        private void LoadSettings()
        {
            var work = Screen.PrimaryScreen.WorkingArea;
            var bounds = new Rectangle(work.Right - 436, work.Top + 24, 412, 375);
            try
            {
                var fields = File.ReadAllText(settingsPath).Split('|');
                bounds = new Rectangle(int.Parse(fields[0]), int.Parse(fields[1]), int.Parse(fields[2]), int.Parse(fields[3]));
                pinned.Checked = bool.Parse(fields[4]);
                mode = Math.Max(0, Math.Min(2, int.Parse(fields[5])));
                int seconds = Math.Max(0, Math.Min(3599999, int.Parse(fields[6])));
                time.SetDuration(TimeSpan.FromSeconds(seconds));
                time.CountdownSignal = (Signal)(int.Parse(fields[7]) & 3);
                time.AlarmSignal = (Signal)(int.Parse(fields[8]) & 3);
                alarm.Value = DateTime.Today.AddMinutes(Math.Max(0, Math.Min(1439, int.Parse(fields[9]))));
                if (fields.Length > 11) themeMenu.Checked = bool.Parse(fields[11]);
                if (fields.Length > 10)
                    foreach (string entry in fields[10].Split(','))
                    {
                        int preset;
                        if (int.TryParse(entry, out preset) && preset > 0 && preset <= 3599999 && !presets.Contains(preset))
                            presets.Add(preset);
                    }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (FormatException) { }
            catch (OverflowException) { }
            catch (IndexOutOfRangeException) { }
            Bounds = KeepVisible(bounds);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (refresh != null) refresh.Dispose();
            notifications.Dispose();
            digital.Dispose();
            if (editorFont != null) editorFont.Dispose();
            SaveSettings();
            base.OnFormClosed(e);
        }

        private bool SaveSettings()
        {
            var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                File.WriteAllText(settingsPath, string.Join("|", new object[] {
                    bounds.X, bounds.Y, bounds.Width, bounds.Height, TopMost, mode, (int)time.Duration.TotalSeconds,
                    (int)time.CountdownSignal, (int)time.AlarmSignal, (int)alarm.Value.TimeOfDay.TotalMinutes, string.Join(",", presets), themeMenu.Checked
                }));
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }

        }
    }
}
