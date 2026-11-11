// Voxl Display - control panel for the virtual second monitor.
// Builds with the .NET Framework compiler that ships with Windows: see build.cmd.
//
// It sets the virtual display's resolution and scaling, composes the voxl command line
// from the settings below and starts or stops voxl.exe. Nothing here touches the video
// path: voxl captures, encodes and serves on its own; this window only configures it.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace VoxlDisplay
{
    internal static class Program
    {
        [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main()
        {
            try { SetProcessDPIAware(); } catch { }

            // --device on|off switches the virtual monitor at the device level. The window
            // uses the same call for Stop, and it is handy from a script.
            var argv = Environment.GetCommandLineArgs();
            for (int i = 1; i < argv.Length - 1; i++)
            {
                if (argv[i] != "--device") continue;
                string result = Display.SetDeviceEnabled(argv[i + 1] == "on");
                // a winexe has no console: leave the answer in a file next to the exe
                try
                {
                    System.IO.File.WriteAllText(
                        System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.ExecutablePath), "device-switch.log"),
                        result);
                }
                catch { }
                Environment.Exit(result == "ok" ? 0 : 1);
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e)
            {
                MessageBox.Show(e.Exception.Message, "Voxl Display", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            try { Application.Run(new MainForm()); }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Voxl Display", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // Settings live in settings.json next to the exe; a hand written reader keeps the app
    // free of a JSON dependency.
    internal sealed class Settings
    {
        public string Resolution = "2560x1600";
        public int ScalePercent = 200;
        public string Quality = "High";
        public string Preset = "ultrafast";
        public int Crf = 20;
        public int MaxMbps = 150;
        public int Fps = 60;
        public int Threads = 8;
        public bool Clipboard = true;
        public bool Audio = true;
        public bool ReadOnly = false;
        public string Password = "voxl6901";
        public int Port = 6901;
        public string VoxlPath = "";
        public string Addr = "";

        private static string FilePath
        {
            get { return Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "settings.json"); }
        }

        public void Save()
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"resolution\": \"" + Resolution + "\",\n");
            sb.Append("  \"scalePercent\": " + ScalePercent + ",\n");
            sb.Append("  \"quality\": \"" + Quality + "\",\n");
            sb.Append("  \"preset\": \"" + Preset + "\",\n");
            sb.Append("  \"crf\": " + Crf + ",\n");
            sb.Append("  \"maxMbps\": " + MaxMbps + ",\n");
            sb.Append("  \"fps\": " + Fps + ",\n");
            sb.Append("  \"threads\": " + Threads + ",\n");
            sb.Append("  \"clipboard\": " + (Clipboard ? "true" : "false") + ",\n");
            sb.Append("  \"audio\": " + (Audio ? "true" : "false") + ",\n");
            sb.Append("  \"readOnly\": " + (ReadOnly ? "true" : "false") + ",\n");
            sb.Append("  \"password\": \"" + Password + "\",\n");
            sb.Append("  \"port\": " + Port + ",\n");
            sb.Append("  \"voxlPath\": \"" + VoxlPath.Replace("\\", "\\\\") + "\",\n");
            sb.Append("  \"addr\": \"" + Addr + "\"\n");
            sb.Append("}\n");
            File.WriteAllText(FilePath, sb.ToString());
        }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                string p = FilePath;
                if (!File.Exists(p)) return s;
                string t = File.ReadAllText(p);
                s.Resolution = ReadString(t, "resolution", s.Resolution);
                s.ScalePercent = ReadInt(t, "scalePercent", s.ScalePercent);
                s.Quality = ReadString(t, "quality", s.Quality);
                s.Preset = ReadString(t, "preset", s.Preset);
                s.Crf = ReadInt(t, "crf", s.Crf);
                s.MaxMbps = ReadInt(t, "maxMbps", s.MaxMbps);
                s.Fps = ReadInt(t, "fps", s.Fps);
                s.Threads = ReadInt(t, "threads", s.Threads);
                s.Clipboard = ReadBool(t, "clipboard", s.Clipboard);
                s.Audio = ReadBool(t, "audio", s.Audio);
                s.ReadOnly = ReadBool(t, "readOnly", s.ReadOnly);
                s.Password = ReadString(t, "password", s.Password);
                s.Port = ReadInt(t, "port", s.Port);
                s.VoxlPath = ReadString(t, "voxlPath", s.VoxlPath);
                s.Addr = ReadString(t, "addr", s.Addr);
            }
            catch { }
            return s;
        }

        private static string ReadString(string json, string key, string fallback)
        {
            int i = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (i < 0) return fallback;
            int colon = json.IndexOf(':', i);
            int q1 = json.IndexOf('"', colon + 1);
            int q2 = json.IndexOf('"', q1 + 1);
            if (q1 < 0 || q2 < 0) return fallback;
            return json.Substring(q1 + 1, q2 - q1 - 1).Replace("\\\\", "\\");
        }

        private static int ReadInt(string json, string key, int fallback)
        {
            int i = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (i < 0) return fallback;
            int colon = json.IndexOf(':', i);
            int end = colon + 1;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == ' ' || json[end] == '-')) end++;
            int v;
            if (int.TryParse(json.Substring(colon + 1, end - colon - 1).Trim(), out v)) return v;
            return fallback;
        }

        private static bool ReadBool(string json, string key, bool fallback)
        {
            int i = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (i < 0) return fallback;
            int colon = json.IndexOf(':', i);
            string rest = json.Substring(colon + 1).TrimStart();
            if (rest.StartsWith("true")) return true;
            if (rest.StartsWith("false")) return false;
            return fallback;
        }
    }

    // The stock CheckBox centres a line box, and a caption with a descender ("Clipboard")
    // then looks lower than its square. This draws both around the same centre.
    internal sealed class FlatCheck : CheckBox
    {
        public FlatCheck()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            AutoSize = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);

            float u = DeviceDpi / 96f;
            int box = (int)Math.Round(16 * u);
            int gap = (int)Math.Round(10 * u);
            int left = (int)Math.Round(2 * u);

            Size text = TextRenderer.MeasureText(Text, Font);
            // one centre for both: the checkbox square and the caption glyphs
            int centre = ClientSize.Height / 2 + (int)Math.Round(1 * u);
            var square = new Rectangle(left, centre - box / 2, box, box);

            using (var fill = new SolidBrush(Checked ? Color.FromArgb(58, 158, 96) : Color.FromArgb(30, 30, 38)))
            using (var border = new Pen(Checked ? Color.FromArgb(74, 178, 112) : Color.FromArgb(96, 96, 108)))
            {
                g.FillRectangle(fill, square);
                g.DrawRectangle(border, square);
            }

            if (Checked)
            {
                using (var tick = new Pen(Color.White, Math.Max(2f, box / 7f)))
                {
                    tick.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    tick.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    float x = square.Left, y = square.Top, s = box;
                    g.DrawLines(tick, new[]
                    {
                        new PointF(x + s * 0.24f, y + s * 0.52f),
                        new PointF(x + s * 0.42f, y + s * 0.70f),
                        new PointF(x + s * 0.76f, y + s * 0.30f),
                    });
                }
            }

            var area = new Rectangle(square.Right + gap, 0,
                Math.Max(0, ClientSize.Width - square.Right - gap), ClientSize.Height);
            TextRenderer.DrawText(g, Text, Font, area, ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }
    // The stock flat Button draws its caption a few pixels off centre (measured 8 design px
    // high at 150 %). This paints the caption itself, vertically centred in the client area.
    internal sealed class FlatButton : Button
    {
        public FlatButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
        }

        private bool _over, _down;

        protected override void OnMouseEnter(EventArgs e) { _over = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _over = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Color back = BackColor;
            if (!Enabled) back = Color.FromArgb(
                (int)(back.R * 0.75), (int)(back.G * 0.75), (int)(back.B * 0.75));
            else if (_down) back = Color.FromArgb(
                Math.Min(255, back.R + 18), Math.Min(255, back.G + 18), Math.Min(255, back.B + 18));
            else if (_over) back = Color.FromArgb(
                Math.Min(255, back.R + 10), Math.Min(255, back.G + 10), Math.Min(255, back.B + 10));

            using (var fill = new SolidBrush(back))
                g.FillRectangle(fill, ClientRectangle);

            Color ink = Enabled ? ForeColor : Color.FromArgb(130, 130, 140);
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    internal sealed class MainForm : Form
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        private const string MonitorTag = "MTT1337";

        private readonly Settings _s;
        private readonly ComboBox _resolution = new ComboBox();
        private readonly ComboBox _scale = new ComboBox();
        private readonly ComboBox _quality = new ComboBox();
        private readonly ComboBox _fps = new ComboBox();
        private readonly ComboBox _preset = new ComboBox();
        private readonly NumericUpDown _maxMbps = new NumericUpDown();
        private readonly NumericUpDown _crf = new NumericUpDown();
        private readonly NumericUpDown _threads = new NumericUpDown();
        private readonly CheckBox _clipboard = new FlatCheck();
        private readonly CheckBox _audio = new FlatCheck();
        private readonly CheckBox _readOnly = new FlatCheck();
        private readonly TextBox _password = new TextBox();
        private readonly Label _status = new Label();
        private readonly Label _hint = new Label();
        private readonly Button _start = new FlatButton();
        private readonly Button _stop = new FlatButton();
        private readonly Button _copy = new FlatButton();
        private readonly Button _qr = new FlatButton();
        private readonly Button _reset = new FlatButton();
        private readonly Button _reload = new FlatButton();
        private readonly Button _install = new FlatButton();
        private readonly NotifyIcon _tray;
        private Process _voxl;
        private bool _busy;
        private bool _quitting;
        private bool _exitAfterWork;
        private string _device;
        private bool _syncing;

        private static readonly string[] QualityNames = { "Low", "Balanced", "High", "Maximum", "Custom" };
        private static readonly string[] PresetNames = { "ultrafast", "superfast", "veryfast", "faster" };

        public MainForm()
        {
            _s = Settings.Load();
            if (string.IsNullOrEmpty(_s.VoxlPath))
                _s.VoxlPath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "voxl.exe");
            if (string.IsNullOrEmpty(_s.Addr)) _s.Addr = LanAddress();

            // WinForms keeps resetting AutoScaleDimensions to the live DPI, which shrinks a
            // form laid out in 96 dpi units. Scaling is done by hand instead, from the
            // window's real DPI once it exists.
            AutoScaleMode = AutoScaleMode.None;
            Text = "Voxl Display - second monitor";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(640, 742);
            BackColor = Color.FromArgb(23, 23, 27);
            ForeColor = Color.FromArgb(235, 235, 240);
            Font = new Font("Segoe UI", 9.75f);

            var title = new Label();
            title.Text = "Second monitor for your tablet";
            title.Font = new Font("Segoe UI Semibold", 15f);
            title.AutoSize = true;
            title.Location = new Point(26, 16);
            Controls.Add(title);

            var sub = new Label();
            sub.Text = "Virtual display plus the voxl stream, LAN only.";
            sub.ForeColor = Color.FromArgb(140, 140, 150);
            sub.AutoSize = true;
            sub.Location = new Point(28, 52);
            Controls.Add(sub);

            // Every section: heading, its line, then 30 px to the first label and 30 to
            // its field. The same rhythm everywhere is what stops it looking crammed.
            Section("Display", 88);

            AddLabel("Resolution", 26, 120);
            Combo(_resolution, 26, 152, 260);
            _resolution.DropDownStyle = ComboBoxStyle.DropDown;
            Controls.Add(_resolution);

            AddLabel("Scaling", 306, 120);
            Combo(_scale, 306, 152, 260);
            Controls.Add(_scale);

            Section("Stream", 206);

            AddLabel("Quality", 26, 238);
            Combo(_quality, 26, 270, 260);
            foreach (string q in QualityNames) _quality.Items.Add(q);
            _quality.SelectedIndexChanged += QualityChanged;
            Controls.Add(_quality);

            AddLabel("Encoder preset (latency)", 306, 238);
            Combo(_preset, 306, 270, 260);
            foreach (string p in PresetNames) _preset.Items.Add(p);
            Controls.Add(_preset);

            AddLabel("CRF", 26, 322);
            Number(_crf, 26, 354, 110, 10, 40);
            Controls.Add(_crf);

            AddLabel("Max Mbps", 156, 322);
            Number(_maxMbps, 156, 354, 130, 5, 2000);
            Controls.Add(_maxMbps);

            AddLabel("FPS", 306, 322);
            Combo(_fps, 306, 354, 110);
            foreach (int f in new[] { 30, 50, 60, 90, 120 }) _fps.Items.Add(f.ToString());
            Controls.Add(_fps);

            AddLabel("Threads", 436, 322);
            Number(_threads, 436, 354, 130, 1, 32);
            Controls.Add(_threads);

            Check(_clipboard, "Clipboard", 26, 410, 132);
            Check(_audio, "Audio", 176, 410, 96);
            Check(_readOnly, "Read-only viewer", 290, 410, 190);

            Section("Access", 462);

            AddLabel("Password (type it on the tablet)", 26, 494);
            _password.Location = new Point(26, 526);
            _password.Size = new Size(294, 26);
            _password.BackColor = Color.FromArgb(35, 35, 42);
            _password.ForeColor = Color.White;
            _password.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(_password);

            Button(_qr, "QR code", 336, 524, Color.FromArgb(48, 48, 58), 124);
            _qr.Click += QrClicked;
            Button(_copy, "Copy link", 470, 524, Color.FromArgb(48, 48, 58), 124);
            _copy.Click += CopyClicked;

            var who = new Label();
            who.Text = "Username: any (leave it empty or type anything)";
            who.AutoSize = true;
            who.Location = new Point(28, 566);
            who.ForeColor = Color.FromArgb(120, 160, 140);
            Controls.Add(who);

            Button(_start, "Start", 26, 606, Color.FromArgb(58, 158, 96), 132);
            _start.Click += StartClicked;
            Button(_stop, "Stop", 168, 606, Color.FromArgb(48, 48, 58), 132);
            _stop.Click += StopClicked;
            Button(_reset, "Defaults", 310, 606, Color.FromArgb(48, 48, 58), 132);
            _reset.Click += ResetClicked;
            Button(_reload, "Reload", 452, 606, Color.FromArgb(48, 48, 58), 132);
            _reload.Click += delegate { LoadDisplays(); ApplySettings(); };

            Button(_install, "Install driver", 400, 686, Color.FromArgb(58, 158, 96), 184);
            _install.Size = new Size(184, 32);
            _install.Click += InstallDriverClicked;

            _status.Text = "stopped";
            _status.Location = new Point(26, 664);
            _status.Size = new Size(560, 22);
            _status.ForeColor = Color.FromArgb(150, 150, 160);
            Controls.Add(_status);

            _hint.Text = "";
            _hint.Location = new Point(26, 690);
            _hint.Size = new Size(360, 40);
            _hint.ForeColor = Color.FromArgb(110, 110, 120);
            Controls.Add(_hint);
            _tray = new NotifyIcon();
            _tray.Text = "Voxl Display";
            try { _tray.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { _tray.Icon = SystemIcons.Application; }
            var menu = new ContextMenuStrip();
            menu.Items.Add("Open", null, delegate { Restore(); });
            menu.Items.Add("Stop and exit", null, delegate
            {
                _quitting = true;
                _exitAfterWork = true;
                Hide();
                if (_busy) { Close(); return; }
                Run(DoStop);
            });
            _tray.ContextMenuStrip = menu;
            _tray.MouseClick += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) Restore();
            };
            _tray.DoubleClick += delegate { Restore(); };
            _tray.Visible = true;

            LoadDisplays();
            ApplySettings();
            ScaleToDpi();
            UpdateAccess();
            FormClosing += OnClosing;
        }

        private void Button(Button b, string text, int x, int y, Color back, int width)
        {
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(width, 40);
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = back;
            b.ForeColor = Color.White;
            b.FlatAppearance.BorderSize = 0;
            b.TextAlign = ContentAlignment.MiddleCenter;
            // native flat buttons draw the caption a touch high; nudge it to the middle

            b.UseVisualStyleBackColor = false;
            Controls.Add(b);
        }

        // Fixed width: with AutoSize three of them push into each other at higher DPI.
        private void Check(CheckBox c, string text, int x, int y, int width)
        {
            c.Text = text;
            c.Location = new Point(x, y);
            c.Size = new Size(width, 26);
            c.AutoSize = false;
            c.TextAlign = ContentAlignment.MiddleLeft;
            c.CheckAlign = ContentAlignment.MiddleLeft;
            c.Padding = new Padding(0, 0, 0, 0);
            c.ForeColor = Color.FromArgb(200, 200, 210);
            Controls.Add(c);
        }

        private void Number(NumericUpDown n, int x, int y, int w, int min, int max)
        {
            n.Location = new Point(x, y);
            n.Size = new Size(w, 26);
            n.Minimum = min;
            n.Maximum = max;
            n.BackColor = Color.FromArgb(35, 35, 42);
            n.ForeColor = Color.White;
            n.BorderStyle = BorderStyle.FixedSingle;
            n.ValueChanged += delegate { if (!_syncing) _quality.SelectedItem = "Custom"; };
        }

        private bool _scaled;

        // The window is laid out in 96 dpi units. DeviceDpi is only correct once the
        // handle exists, so the one-time scale happens here; doing it in the constructor
        // left the window at 1:1 while the fonts grew with the DPI, which is what made
        // every row look crammed.

        // A compromise between the tiny 1:1 window and a full 150 % one: the layout is scaled
        // a little, the fonts keep their normal size, so nothing is clipped and nothing is
        // unreadably small.
        private void ScaleModestly()
        {
            float f = GetDpiForWindow(Handle) / 96f;
            if (f > 1.15f) f = 1.15f;
            if (f < 1.0f) f = 1.0f;
            if (f <= 1.001f) return;
            SuspendLayout();
            Scale(new SizeF(f, f));
            ClientSize = new Size((int)(640 * f), (int)(742 * f));
            ResumeLayout(true);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (_scaled) return;
            _scaled = true;
            ScaleModestly();
        }
        private void ScaleToDpi()
        {
            if (DeviceDpi > 96)
            {
                float f = GetDpiForWindow(Handle) / 96f;
                SuspendLayout();
                Scale(new SizeF(f, f));
                ClientSize = new Size((int)(640 * f), (int)(742 * f));
                ResumeLayout(true);
            }
            if (!File.Exists(_s.VoxlPath))
                _hint.Text = "voxl.exe was not found next to this app. Put both in one folder.";
        }

        // A quiet section header with a hairline under it.
        private void Section(string text, int y)
        {
            var l = new Label();
            l.Text = text.ToUpperInvariant();
            l.Font = new Font("Segoe UI Semibold", 8.25f);
            l.ForeColor = Color.FromArgb(100, 100, 112);
            l.AutoSize = true;
            l.Location = new Point(26, y);
            Controls.Add(l);

            var line = new Panel();
            line.Location = new Point(26, y + 16);
            line.Size = new Size(566, 1);
            line.BackColor = Color.FromArgb(45, 45, 54);
            Controls.Add(line);
        }

        private void AddLabel(string text, int x, int y)
        {
            var l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Location = new Point(x, y);
            l.ForeColor = Color.FromArgb(170, 170, 180);
            Controls.Add(l);
        }

        private static void Combo(ComboBox c, int x, int y, int w)
        {
            c.Location = new Point(x, y);
            c.Size = new Size(w, 26);
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.FlatStyle = FlatStyle.Flat;
            c.BackColor = Color.FromArgb(35, 35, 42);
            c.ForeColor = Color.White;
            c.DrawMode = DrawMode.OwnerDrawFixed;
            c.DrawItem += delegate(object s, DrawItemEventArgs e)
            {
                var combo = (ComboBox)s;
                using (var back = new SolidBrush(Color.FromArgb(35, 35, 42)))
                    e.Graphics.FillRectangle(back, e.Bounds);
                if (e.Index < 0) return;
                bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                if (selected)
                    using (var hi = new SolidBrush(Color.FromArgb(62, 62, 78)))
                        e.Graphics.FillRectangle(hi, e.Bounds);
                using (var text = new SolidBrush(Color.White))
                    e.Graphics.DrawString(combo.Items[e.Index].ToString(), e.Font, text, e.Bounds);
            };
        }

        private void LoadDisplays()
        {
            _device = Display.FindDeviceName(MonitorTag);
            _resolution.Items.Clear();
            _scale.Items.Clear();

            if (_device == null)
            {
                // The stream settings still work without a display, so only the two lists
                // that come from the driver stay empty, and the window says why.
                _status.Text = "The virtual display is not attached.";
                _status.ForeColor = Color.FromArgb(220, 120, 120);
                _hint.Text =
                    "The driver is missing or was refused. Press Install driver; if that fails, " +
                    "run diagnose.ps1 next to the app and send the output.";
                _install.Visible = true;
                return;
            }

            foreach (string m in Display.ListModes(_device)) _resolution.Items.Add(m);
            int min, cur, max;
            Display.GetScaleRange(MonitorTag, out min, out cur, out max);
            for (int i = min; i <= max; i++)
            {
                int pct = Display.ScalePercentFor(MonitorTag, i);
                if (pct > 0) _scale.Items.Add(pct + "%");
            }
            _hint.Text = "Display " + _device + " is ready.";
            _install.Visible = false;
        }

        // Runs the driver install that ships next to the app, so a machine where it failed
        // can be repaired without finding the zip again.
        private void InstallDriverClicked(object sender, EventArgs e)
        {
            string script = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "install-vdd.ps1");
            string vendor = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "vendor");
            string settings = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "vdd_settings.xml");
            if (!File.Exists(script))
            {
                MessageBox.Show("install-vdd.ps1 is missing next to the app. Run install.cmd from the release zip.",
                    "Voxl Display");
                return;
            }

            Status("installing the virtual display driver...", false);
            var psi = new ProcessStartInfo("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + script + "\" " +
                "-VendorDir \"" + vendor + "\"" + (File.Exists(settings) ? " -Settings \"" + settings + "\"" : ""));
            psi.UseShellExecute = true;
            psi.Verb = "runas";
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            try
            {
                Process p = Process.Start(psi);
                p.WaitForExit();
                if (p.ExitCode != 0)
                {
                    Status("the driver install failed (exit code " + p.ExitCode + ")", true);
                    return;
                }
            }
            catch (Exception ex)
            {
                Status("the driver install was not started: " + ex.Message, true);
                return;
            }

            LoadDisplays();
            ApplySettings();
            if (_device != null) Status("driver installed, display " + _device + " is ready", false);
        }

        private void ApplySettings()
        {
            _syncing = true;
            if (_resolution.Items.Count > 0)
            {
                int i = _resolution.Items.IndexOf(_s.Resolution);
                if (i >= 0) _resolution.SelectedIndex = i; else _resolution.Text = _s.Resolution;
            }
            int best = 0, bestDiff = int.MaxValue;
            for (int i = 0; i < _scale.Items.Count; i++)
            {
                int pct;
                if (int.TryParse(_scale.Items[i].ToString().TrimEnd('%'), out pct))
                {
                    int d = Math.Abs(pct - _s.ScalePercent);
                    if (d < bestDiff) { bestDiff = d; best = i; }
                }
            }
            if (_scale.Items.Count > 0) _scale.SelectedIndex = best;
            _quality.SelectedItem = _s.Quality;
            if (_quality.SelectedIndex < 0 && _quality.Items.Count > 0) _quality.SelectedIndex = 2;
            _preset.SelectedItem = _s.Preset;
            if (_preset.SelectedIndex < 0) _preset.SelectedIndex = 0;
            _maxMbps.Value = Clamp(_s.MaxMbps, _maxMbps);
            _crf.Value = Clamp(_s.Crf, _crf);
            _threads.Value = Clamp(_s.Threads, _threads);
            _fps.SelectedItem = _s.Fps.ToString();
            if (_fps.SelectedIndex < 0) _fps.SelectedIndex = _fps.Items.Count - 1;
            _clipboard.Checked = _s.Clipboard;
            _audio.Checked = _s.Audio;
            _readOnly.Checked = _s.ReadOnly;
            _password.Text = _s.Password;
            _syncing = false;
        }

        private static decimal Clamp(int v, NumericUpDown n)
        {
            if (v < n.Minimum) return n.Minimum;
            if (v > n.Maximum) return n.Maximum;
            return v;
        }

        // Quality presets only fill the numbers; editing a number means Custom.
        private void QualityChanged(object sender, EventArgs e)
        {
            if (_syncing) return;
            _syncing = true;
            string q = _quality.SelectedItem as string;
            if (q == "Low") { _crf.Value = 26; _maxMbps.Value = 15; _preset.SelectedItem = "ultrafast"; }
            else if (q == "Balanced") { _crf.Value = 22; _maxMbps.Value = 40; _preset.SelectedItem = "ultrafast"; }
            else if (q == "High") { _crf.Value = 20; _maxMbps.Value = 150; _preset.SelectedItem = "ultrafast"; }
            else if (q == "Maximum") { _crf.Value = 16; _maxMbps.Value = 150; _preset.SelectedItem = "superfast"; }
            _syncing = false;
        }

        private void QrClicked(object sender, EventArgs e)
        {
            using (var f = new QrForm(Link(), _password.Text))
                f.ShowDialog(this);
        }

        private string Link()
        {
            return "https://" + _s.Addr + ":" + _s.Port + "/?token=" + _password.Text;
        }

        // Everything that talks to Windows or waits for a UAC prompt runs on a background
        // thread: doing it on the UI thread is what made the window freeze on Stop.
        private delegate StepResult Work();

        private sealed class StepResult
        {
            public string Message;
            public bool Error;
            public string NeedsMode;

            public static StepResult Ok(string message) { return new StepResult { Message = message }; }
            public static StepResult Fail(string message) { return new StepResult { Message = message, Error = true }; }
            public static StepResult NeedMode(string mode) { return new StepResult { NeedsMode = mode, Message = "the driver does not offer " + mode }; }
        }

        private sealed class Request
        {
            public string Resolution;
            public int ScalePercent;
            public string Quality, Preset;
            public int Crf, MaxMbps, Fps, Threads;
            public bool Clipboard, Audio, ReadOnly;
            public string Password;
        }

        private Request ReadForm()
        {
            var r = new Request();
            r.Resolution = _resolution.Text.Trim();
            int pct;
            if (int.TryParse(_scale.SelectedItem == null ? "" : _scale.SelectedItem.ToString().TrimEnd('%'), out pct))
                r.ScalePercent = pct;
            r.Quality = _quality.SelectedItem as string;
            r.Preset = _preset.SelectedItem as string;
            r.Crf = (int)_crf.Value;
            r.MaxMbps = (int)_maxMbps.Value;
            r.Fps = int.Parse(_fps.SelectedItem.ToString());
            r.Threads = (int)_threads.Value;
            r.Clipboard = _clipboard.Checked;
            r.Audio = _audio.Checked;
            r.ReadOnly = _readOnly.Checked;
            r.Password = _password.Text;
            return r;
        }

        // QR and Copy link point at a running stream; hide them otherwise so the window
        // shows only what is usable now.
        private void UpdateAccess()
        {
            bool running = _voxl != null && !_voxl.HasExited;
            _qr.Visible = running;
            _copy.Visible = running;
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _start.Enabled = !busy;
            _stop.Enabled = !busy;
            _reload.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            if (busy) _status.Text = "working...";
        }

        private void Run(Work work)
        {
            if (_busy) return;
            SetBusy(true);
            var bw = new System.ComponentModel.BackgroundWorker();
            bw.DoWork += delegate(object s, System.ComponentModel.DoWorkEventArgs e) { e.Result = work(); };
            bw.RunWorkerCompleted += delegate(object s, System.ComponentModel.RunWorkerCompletedEventArgs e)
            {
                SetBusy(false);
                if (e.Error != null) { Status(e.Error.Message, true); RefreshState(); return; }
                if (_exitAfterWork) { Close(); return; }
                StepResult r = (StepResult)e.Result;
                Status(r.Message, r.Error);
                UpdateAccess();

                if (r.NeedsMode != null)
                {
                    int w, h;
                    Display.ParseMode(r.NeedsMode, out w, out h);
                    DialogResult answer = MessageBox.Show(
                        "The driver does not offer " + r.NeedsMode + ".\r\n\r\n" +
                        "Add it to the driver's mode list? The display restarts for a second.",
                        "Add custom resolution", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    Request req = ReadForm();
                    bool add = answer == DialogResult.Yes;
                    Run(delegate
                    {
                        if (add)
                        {
                            string problem = Display.AddMode(w, h, 60);
                            if (problem != null) return StepResult.Fail("could not add the mode: " + problem);
                        }
                        return DoStart(req, !add);
                    });
                    return;
                }
                RefreshState();
                UpdateAccess();
            };
            bw.RunWorkerAsync();
        }

        // What Windows knows right now: the driver may be installed, switched off, or both.
        private void RefreshState()
        {
            _device = Display.FindDeviceName(MonitorTag);
            if (_device != null)
            {
                _resolution.Items.Clear();
                foreach (string m in Display.ListModes(_device)) _resolution.Items.Add(m);
                _scale.Items.Clear();
                int min, cur, max;
                Display.GetScaleRange(MonitorTag, out min, out cur, out max);
                for (int i = min; i <= max; i++)
                {
                    int pct = Display.ScalePercentFor(MonitorTag, i);
                    if (pct > 0) _scale.Items.Add(pct + "%");
                }
                ApplySettings();
                _install.Visible = false;
                if (!_busy && _status.Text.StartsWith("stopped")) _hint.Text = "Press Start to stream.";
                return;
            }

            bool installed = Display.DriverInstalled();
            _install.Visible = !installed;
            if (installed)
            {
                _install.Visible = false;
                _hint.Text = "The driver is installed and the monitor is switched off. Press Start to connect it.";
            }
            else
            {
                _hint.Text = "The driver is missing. Press Install driver, or run install.cmd from the release zip.";
            }
        }

        private void StartClicked(object sender, EventArgs e)
        {
            if (_busy) return;
            if (!File.Exists(_s.VoxlPath)) { Status("voxl.exe was not found: " + _s.VoxlPath, true); return; }
            Request req = ReadForm();
            Run(delegate { return DoStart(req, false); });
        }

        private void StopClicked(object sender, EventArgs e)
        {
            Run(DoStop);
        }

        // Runs on a background thread. Talks to Windows, never to the window.
        private StepResult DoStart(Request req, bool keepCurrentMode)
        {
            int w, h;
            if (!Display.ParseMode(req.Resolution, out w, out h))
                return StepResult.Fail("the resolution must look like 2560x1600");

            // 0. The layout as it is now, before enabling anything. Windows re-arranges all
            //    screens when an indirect display appears, and this is what gets put back.
            List<Display.Snapshot> before = Display.TakeSnapshot();

            // 1. The monitor may be switched off from an earlier Stop.
            if (Display.FindDeviceName(MonitorTag) == null)
            {
                string enabled = Display.SetDeviceEnabled(true);
                if (enabled != "ok") return StepResult.Fail("could not switch the display on: " + enabled);
                if (!Display.WaitForAttached(true, 25)) return StepResult.Fail("the display did not come back");
            }
            string device = Display.FindDeviceName(MonitorTag);
            if (device == null) return StepResult.Fail("the display is not attached");


            // 3. The mode: the driver only offers what is in its settings file.
            bool offered = Display.ModeOffered(device, w, h, 60);
            if (!offered && !keepCurrentMode)
                return StepResult.NeedMode(w + "x" + h);
            if (!offered)
            {
                string current = Display.CurrentMode(device);
                int cw, ch;
                if (!Display.ParseMode(current, out cw, out ch))
                    return StepResult.Fail("the display reports no usable mode");
                w = cw; h = ch;
            }

            string mode = Display.SetMode(device, w, h, 60);
            if (mode != "ok") return StepResult.Fail("resolution: " + mode);

            if (req.ScalePercent > 0)
            {
                int idx = Display.IndexOfPercent(MonitorTag, req.ScalePercent);
                if (idx >= 0) Display.SetScale(MonitorTag, idx);
            }

            string placed = Display.PlaceOnRight(device);
            string restored = Display.RestoreOthers(before, device);

            // 4. Save and start the stream.
            _s.Resolution = w + "x" + h;
            _s.ScalePercent = req.ScalePercent;
            _s.Quality = req.Quality;
            _s.Preset = req.Preset;
            _s.Crf = req.Crf;
            _s.MaxMbps = req.MaxMbps;
            _s.Fps = req.Fps;
            _s.Threads = req.Threads;
            _s.Clipboard = req.Clipboard;
            _s.Audio = req.Audio;
            _s.ReadOnly = req.ReadOnly;
            _s.Password = req.Password;
            _s.Save();

            KillVoxl();
            Process voxl = Display.StartVoxl(_s, device);
            _voxl = voxl;
            System.Threading.Thread.Sleep(1500);
            if (voxl.HasExited)
                return StepResult.Fail("voxl exited with code " + voxl.ExitCode + ", see voxl.log");

            string note = "";
            if (placed != "ok") note += " (placement: " + placed + ")";
            if (restored != "ok") note += " (restored: " + restored + ")";
            return StepResult.Ok("running at https://" + _s.Addr + ":" + _s.Port + note);
        }

        private StepResult DoStop()
        {
            KillVoxl();
            string switched = Display.SetDeviceEnabled(false);
            if (!Display.WaitForAttached(false, 20))
                return StepResult.Fail("stopped, but the display is still attached (" + switched + ")");
            return StepResult.Ok("stopped, the virtual display is disconnected");
        }

        // Defaults for everything the driver and the stream care about; the local paths
        // and the detected LAN address stay, because they belong to this machine.
        private void ResetClicked(object sender, EventArgs e)
        {
            var d = new Settings();
            _s.Resolution = d.Resolution;
            _s.ScalePercent = d.ScalePercent;
            _s.Quality = d.Quality;
            _s.Preset = d.Preset;
            _s.Crf = d.Crf;
            _s.MaxMbps = d.MaxMbps;
            _s.Fps = d.Fps;
            _s.Threads = d.Threads;
            _s.Clipboard = d.Clipboard;
            _s.Audio = d.Audio;
            _s.ReadOnly = d.ReadOnly;
            _s.Password = d.Password;
            ApplySettings();
            _s.Save();
            Status("defaults loaded", false);
        }

        private void CopyClicked(object sender, EventArgs e)
        {
            try { Clipboard.SetText(Link()); Status("link copied", false); }
            catch { Status(Link(), false); }
        }

        private void KillVoxl()
        {
            try { if (_voxl != null && !_voxl.HasExited) _voxl.Kill(); } catch { }
            foreach (Process p in Process.GetProcessesByName("voxl"))
            {
                try { p.Kill(); } catch { }
            }
            _voxl = null;
        }

        private void Status(string text, bool error)
        {
            _status.Text = text;
            try
            {
                bool live = _voxl != null && !_voxl.HasExited;
                _tray.Text = live ? "Voxl Display - streaming" : "Voxl Display - stopped";
            }
            catch { }
            _status.ForeColor = error ? Color.FromArgb(230, 130, 130) : Color.FromArgb(150, 150, 160);
        }

        // The X hides the window and leaves the stream running; the tray icon brings it back.
        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (!_quitting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                try
                {
                    _tray.ShowBalloonTip(2500, "Voxl Display",
                        "Still running in the tray. Use the icon to open or stop it.", ToolTipIcon.Info);
                }
                catch { }
                return;
            }
            _tray.Visible = false;
            _tray.Dispose();
        }

        private void Restore()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
        }

        private static string LanAddress()
        {
            try
            {
                foreach (IPAddress a in Dns.GetHostAddresses(Dns.GetHostName()))
                    if (a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                        return a.ToString();
            }
            catch { }
            return "127.0.0.1";
        }
    }

    // The QR code for the tablet: scan, no typing.
    internal sealed class QrForm : Form
    {
        public QrForm(string link, string password)
        {
            Text = "Scan to open";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(380, 460);
            BackColor = Color.FromArgb(23, 23, 27);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9.75f);

            var box = new PictureBox();
            box.Location = new Point(30, 24);
            box.Size = new Size(320, 320);
            box.SizeMode = PictureBoxSizeMode.Zoom;
            box.BackColor = Color.White;
            bool[,] m = Qr.Encode(link);
            if (m == null)
            {
                var err = new Label();
                err.Text = "The link is too long for a QR code. Use Copy link instead.";
                err.AutoSize = false;
                err.Size = new Size(320, 320);
                err.TextAlign = ContentAlignment.MiddleCenter;
                err.ForeColor = Color.Black;
                box.Controls.Add(err);
            }
            else
            {
                box.Image = Render(m, 8, 4);
            }
            Controls.Add(box);

            var url = new TextBox();
            url.Text = link;
            url.ReadOnly = true;
            url.Location = new Point(30, 356);
            url.Size = new Size(320, 26);
            url.BackColor = Color.FromArgb(35, 35, 42);
            url.ForeColor = Color.White;
            url.BorderStyle = BorderStyle.FixedSingle;
            url.SelectAll();
            Controls.Add(url);

            var hint = new Label();
            hint.Text = "Password: " + password;
            hint.Location = new Point(30, 390);
            hint.AutoSize = true;
            hint.ForeColor = Color.FromArgb(160, 160, 170);
            Controls.Add(hint);

            var close = new Button();
            close.Text = "Close";
            close.Location = new Point(270, 414);
            close.Size = new Size(80, 32);
            close.FlatStyle = FlatStyle.Flat;
            close.BackColor = Color.FromArgb(48, 48, 58);
            close.ForeColor = Color.White;
            close.FlatAppearance.BorderSize = 0;
            close.Click += delegate { Close(); };
            Controls.Add(close);
        }

        // Modules as black squares on white, with the quiet zone a scanner needs.
        private static Bitmap Render(bool[,] modules, int scale, int quiet)
        {
            int size = modules.GetLength(0);
            int side = (size + quiet * 2) * scale;
            var bmp = new Bitmap(side, side);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                using (var black = new SolidBrush(Color.Black))
                {
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                            if (modules[y, x])
                                g.FillRectangle(black, (x + quiet) * scale, (y + quiet) * scale, scale, scale);
                }
            }
            return bmp;
        }
    }

    // Display modes and per-monitor scaling through the Win32 display APIs.
    internal static class Display
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields;
            public int dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }

        [StructLayout(LayoutKind.Sequential)] private struct LUID { public uint LowPart; public int HighPart; }
        [StructLayout(LayoutKind.Sequential)] private struct SRCINFO { public LUID adapterId; public uint id; public uint modeInfoIdx; public uint statusFlags; }
        [StructLayout(LayoutKind.Sequential)]
        private struct TGTINFO
        {
            public LUID adapterId; public uint id; public uint modeInfoIdx; public uint outputTechnology;
            public uint rotation; public uint scaling; public uint refreshNum; public uint refreshDen;
            public uint scanLineOrdering; public int targetAvailable; public uint statusFlags;
        }
        [StructLayout(LayoutKind.Sequential)] private struct PATH { public SRCINFO source; public TGTINFO target; public uint flags; }
        [StructLayout(LayoutKind.Sequential)]
        private struct MODEINFO
        {
            public uint infoType; public uint id; public LUID adapterId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)] public byte[] pad;
        }
        [StructLayout(LayoutKind.Sequential)] private struct HDR { public int type; public uint size; public LUID adapterId; public uint id; }
        [StructLayout(LayoutKind.Sequential)] private struct SCALE_GET { public HDR header; public int min; public int cur; public int max; }
        [StructLayout(LayoutKind.Sequential)] private struct SCALE_SET { public HDR header; public int rel; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct TGTNAME
        {
            public HDR header; public uint flags; public uint outputTechnology;
            public ushort edidMfr; public ushort edidProduct; public uint connector;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string friendly;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string path;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SRCNM
        {
            public HDR header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string gdi;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool EnumDisplaySettingsW(string dev, int mode, ref DEVMODE dm);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int ChangeDisplaySettingsExW(string dev, ref DEVMODE dm, IntPtr h, uint flags, IntPtr p);
        [DllImport("user32.dll")] private static extern int GetDisplayConfigBufferSizes(uint f, out uint paths, out uint modes);
        [DllImport("user32.dll")] private static extern int QueryDisplayConfig(uint f, ref uint paths, [Out] PATH[] p, ref uint modes, [Out] MODEINFO[] m, IntPtr topo);
        [DllImport("user32.dll")] private static extern int DisplayConfigGetDeviceInfo(ref TGTNAME n);
        [DllImport("user32.dll")] private static extern int DisplayConfigGetDeviceInfo(ref SRCNM n);
        [DllImport("user32.dll")] private static extern int DisplayConfigGetDeviceInfo(ref SCALE_GET s);
        [DllImport("user32.dll")] private static extern int DisplayConfigSetDeviceInfo(ref SCALE_SET s);
        

        private static PATH[] Paths()
        {
            uint np, nm;
            if (GetDisplayConfigBufferSizes(2, out np, out nm) != 0) return null;
            var paths = new PATH[np];
            var modes = new MODEINFO[nm];
            for (int i = 0; i < modes.Length; i++) modes[i].pad = new byte[48];
            if (QueryDisplayConfig(2, ref np, paths, ref nm, modes, IntPtr.Zero) != 0) return null;
            return paths;
        }

        private static int FindPath(string tag)
        {
            PATH[] paths = Paths();
            if (paths == null) return -1;
            for (int i = 0; i < paths.Length; i++)
            {
                var n = new TGTNAME();
                n.header.type = 2;
                n.header.size = (uint)Marshal.SizeOf(typeof(TGTNAME));
                n.header.adapterId = paths[i].target.adapterId;
                n.header.id = paths[i].target.id;
                if (DisplayConfigGetDeviceInfo(ref n) != 0) continue;
                if (n.path.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0) return i;
            }
            return -1;
        }

        public static string FindDeviceName(string tag)
        {
            PATH[] paths = Paths();
            int i = FindPath(tag);
            if (paths == null || i < 0) return null;
            var s = new SRCNM();
            s.header.type = 1;
            s.header.size = (uint)Marshal.SizeOf(typeof(SRCNM));
            s.header.adapterId = paths[i].source.adapterId;
            s.header.id = paths[i].source.id;
            if (DisplayConfigGetDeviceInfo(ref s) != 0) return null;
            return s.gdi;
        }

        public static List<string> ListModes(string dev)
        {
            var seen = new List<string>();
            var cur = new DEVMODE();
            cur.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            EnumDisplaySettingsW(dev, -1, ref cur);
            var dm = new DEVMODE();
            dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            for (int i = 0; EnumDisplaySettingsW(dev, i, ref dm); i++)
            {
                if (dm.dmBitsPerPel != cur.dmBitsPerPel) continue;
                if (dm.dmDisplayFrequency != 60) continue;
                string s = dm.dmPelsWidth + "x" + dm.dmPelsHeight;
                if (!seen.Contains(s)) seen.Add(s);
            }
            seen.Sort(delegate(string a, string b)
            {
                int wa, ha, wb, hb;
                ParseMode(a, out wa, out ha);
                ParseMode(b, out wb, out hb);
                return (wa * ha).CompareTo(wb * hb);
            });
            return seen;
        }

        public static bool ParseMode(string s, out int w, out int h)
        {
            w = 0; h = 0;
            int x = s.IndexOf('x');
            if (x <= 0) return false;
            if (!int.TryParse(s.Substring(0, x), out w)) return false;
            if (!int.TryParse(s.Substring(x + 1), out h)) return false;
            return w >= 320 && h >= 200 && w <= 8192 && h <= 8192;
        }

        // Adds a mode through the elevated helper script: the driver's settings file lives
        // under C:\ and the device restart needs administrator rights.
        public static string AddMode(int w, int h, int hz)
        {
            string script = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "add-mode.ps1");
            if (!File.Exists(script)) return "add-mode.ps1 is missing next to the app";

            var psi = new ProcessStartInfo("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + script + "\" " +
                "-Width " + w + " -Height " + h + " -Refresh " + hz);
            psi.UseShellExecute = true;
            psi.Verb = "runas";
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            try
            {
                Process p = Process.Start(psi);
                p.WaitForExit();
                return p.ExitCode == 0 ? null : "the helper exited with code " + p.ExitCode;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        // Is the driver package installed at all? Different from "is the monitor on".
        public static bool DriverInstalled()
        {
            try
            {
                foreach (System.Management.ManagementObject o in new System.Management.ManagementObjectSearcher(
                    "SELECT Name FROM Win32_PnPEntity WHERE Name LIKE '%Virtual Display Driver%'").Get())
                    return true;
            }
            catch { }
            return false;
        }

        public static bool ModeOffered(string dev, int w, int h, int hz)
        {
            var dm = new DEVMODE();
            dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            for (int i = 0; EnumDisplaySettingsW(dev, i, ref dm); i++)
                if (dm.dmPelsWidth == w && dm.dmPelsHeight == h && dm.dmDisplayFrequency == hz) return true;
            return false;
        }

        public static string CurrentMode(string dev)
        {
            var dm = new DEVMODE();
            dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            if (!EnumDisplaySettingsW(dev, -1, ref dm)) return "";
            return dm.dmPelsWidth + "x" + dm.dmPelsHeight;
        }

        public static string SetMode(string dev, int w, int h, int hz)
        {
            var dm = new DEVMODE();
            dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            bool found = false;
            for (int i = 0; EnumDisplaySettingsW(dev, i, ref dm); i++)
            {
                if (dm.dmPelsWidth == w && dm.dmPelsHeight == h && dm.dmDisplayFrequency == hz) { found = true; break; }
            }
            if (!found) return "mode " + w + "x" + h + " is not offered";
            dm.dmFields = 0x80000 | 0x100000 | 0x40000000;
            int r = ChangeDisplaySettingsExW(dev, ref dm, IntPtr.Zero, 0x00000001, IntPtr.Zero);
            return r == 0 ? "ok" : "ChangeDisplaySettingsEx " + r;
        }

        // One attached display: its GDI name, mode and scaling step. Taken before the app
        // changes anything, so the others can be put back if Windows applies a change to
        // the wrong screen.
        public sealed class Snapshot
        {
            public string Gdi;
            public int Width, Height, Hz;
            public int X, Y;
            public int ScaleIndex;
        }

        public static List<Snapshot> TakeSnapshot()
        {
            var list = new List<Snapshot>();
            PATH[] paths = Paths();
            if (paths == null) return list;
            for (int i = 0; i < paths.Length; i++)
            {
                var s = new SRCNM();
                s.header.type = 1;
                s.header.size = (uint)Marshal.SizeOf(typeof(SRCNM));
                s.header.adapterId = paths[i].source.adapterId;
                s.header.id = paths[i].source.id;
                if (DisplayConfigGetDeviceInfo(ref s) != 0) continue;

                var dm = new DEVMODE();
                dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                if (!EnumDisplaySettingsW(s.gdi, -1, ref dm)) continue;

                var g = new SCALE_GET();
                g.header.type = -3;
                g.header.size = (uint)Marshal.SizeOf(typeof(SCALE_GET));
                g.header.adapterId = paths[i].source.adapterId;
                g.header.id = paths[i].source.id;
                int idx = DisplayConfigGetDeviceInfo(ref g) == 0 ? g.cur : int.MinValue;

                list.Add(new Snapshot { Gdi = s.gdi, Width = dm.dmPelsWidth, Height = dm.dmPelsHeight, Hz = dm.dmDisplayFrequency, ScaleIndex = idx, X = dm.dmPositionX, Y = dm.dmPositionY });
            }
            return list;
        }

        // Puts every display except the virtual one back to what it was before Start.
        public static string RestoreOthers(List<Snapshot> before, string skipGdi)
        {
            var changed = new List<string>();
            foreach (Snapshot snap in before)
            {
                // The virtual display is the one being configured: never put it back.
                if (snap.Gdi == null || snap.Gdi == skipGdi) continue;

                var dm = new DEVMODE();
                dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                if (!EnumDisplaySettingsW(snap.Gdi, -1, ref dm)) continue; // gone since then

                if (dm.dmPositionX != snap.X || dm.dmPositionY != snap.Y)
                {
                    var pos = new DEVMODE();
                    pos.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                    if (EnumDisplaySettingsW(snap.Gdi, -1, ref pos))
                    {
                        pos.dmPositionX = snap.X;
                        pos.dmPositionY = snap.Y;
                        pos.dmFields = 0x20; // DM_POSITION
                        if (ChangeDisplaySettingsExW(snap.Gdi, ref pos, IntPtr.Zero, 0x00000001, IntPtr.Zero) == 0)
                            changed.Add(snap.Gdi + " back to (" + snap.X + "," + snap.Y + ")");
                    }
                }

                if (dm.dmPelsWidth != snap.Width || dm.dmPelsHeight != snap.Height || dm.dmDisplayFrequency != snap.Hz)
                {
                    string r = SetMode(snap.Gdi, snap.Width, snap.Height, snap.Hz);
                    changed.Add(snap.Gdi + " " + snap.Width + "x" + snap.Height + (r == "ok" ? "" : " (" + r + ")"));
                }

                if (snap.ScaleIndex != int.MinValue)
                {
                    PATH[] paths = Paths();
                    if (paths == null) continue;
                    for (int i = 0; i < paths.Length; i++)
                    {
                        var s = new SRCNM();
                        s.header.type = 1;
                        s.header.size = (uint)Marshal.SizeOf(typeof(SRCNM));
                        s.header.adapterId = paths[i].source.adapterId;
                        s.header.id = paths[i].source.id;
                        if (DisplayConfigGetDeviceInfo(ref s) != 0 || s.gdi != snap.Gdi) continue;

                        var g = new SCALE_GET();
                        g.header.type = -3;
                        g.header.size = (uint)Marshal.SizeOf(typeof(SCALE_GET));
                        g.header.adapterId = paths[i].source.adapterId;
                        g.header.id = paths[i].source.id;
                        if (DisplayConfigGetDeviceInfo(ref g) != 0 || g.cur == snap.ScaleIndex) break;

                        var set = new SCALE_SET();
                        set.header.type = -4;
                        set.header.size = (uint)Marshal.SizeOf(typeof(SCALE_SET));
                        set.header.adapterId = paths[i].source.adapterId;
                        set.header.id = paths[i].source.id;
                        set.rel = snap.ScaleIndex;
                        if (DisplayConfigSetDeviceInfo(ref set) == 0)
                            changed.Add(s.gdi + " scale restored");
                        break;
                    }
                }
            }
            return changed.Count == 0 ? "ok" : string.Join(", ", changed.ToArray());
        }

        // Puts the display to the right of every other attached screen. Windows happily
        // stacks a new monitor on (0,0), which overlaps the primary and makes both show the
        // same number, so this runs after every start.
        public static string PlaceOnRight(string dev)
        {
            int right = 0;
            PATH[] paths = Paths();
            if (paths != null)
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    var s = new SRCNM();
                    s.header.type = 1;
                    s.header.size = (uint)Marshal.SizeOf(typeof(SRCNM));
                    s.header.adapterId = paths[i].source.adapterId;
                    s.header.id = paths[i].source.id;
                    if (DisplayConfigGetDeviceInfo(ref s) != 0) continue;
                    if (s.gdi == dev) continue;
                    var dm = new DEVMODE();
                    dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                    if (!EnumDisplaySettingsW(s.gdi, -1, ref dm)) continue;
                    int edge = dm.dmPositionX + dm.dmPelsWidth;
                    if (edge > right) right = edge;
                }
            }

            var move = new DEVMODE();
            move.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            if (!EnumDisplaySettingsW(dev, -1, ref move)) return "no current mode";
            if (move.dmPositionX == right && move.dmPositionY == 0) return "ok";
            move.dmPositionX = right;
            move.dmPositionY = 0;
            move.dmFields = 0x20; // DM_POSITION
            int r = ChangeDisplaySettingsExW(dev, ref move, IntPtr.Zero, 0x00000001, IntPtr.Zero);
            return r == 0 ? "ok" : "position " + r;
        }

        // Turning the device node off is the only reliable way to detach a virtual monitor:
        // SetDisplayConfig refuses the supplied configuration for these drivers. Windows
        // moves the windows back to the remaining screens when the monitor disappears.
        // Waits until the desktop really has (or has lost) the display. pnputil returns
        // success before Windows finishes the topology change, so this is the only honest
        // check.
        public static bool WaitForAttached(bool want, int seconds)
        {
            for (int i = 0; i < seconds * 4; i++)
            {
                bool attached = FindDeviceName("MTT1337") != null;
                if (attached == want) return true;
                System.Threading.Thread.Sleep(250);
            }
            return false;
        }

        public static string SetDeviceEnabled(bool on)
        {
            // One code path only: the helper that install.ps1 ships. When the app already
            // runs elevated it does not prompt, otherwise it asks once. The helper writes
            // device-switch.log, so a failure can be read instead of guessed.
            return SetDeviceEnabledElevated(on);
        }

        // One UAC prompt, then the helper script does the same pnputil call.
        private static string SetDeviceEnabledElevated(bool on)
        {
            string script = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "switch-device.ps1");
            if (!File.Exists(script)) return "switch-device.ps1 is missing next to the app";

            var psi = new ProcessStartInfo("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + script + "\" -State " + (on ? "on" : "off"));
            psi.UseShellExecute = true;
            psi.Verb = "runas";
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            try
            {
                Process p = Process.Start(psi);
                p.WaitForExit();
                if (p.ExitCode != 0) return "the switch helper exited with " + p.ExitCode;
                System.Threading.Thread.Sleep(on ? 2500 : 1200);
                return "ok";
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        // Detaches the virtual monitor from the desktop, so Windows moves the windows that
        // were on it back to the remaining screens. The driver stays installed.
        

        
        // Brings a detached virtual monitor back. QueryDisplayConfig with QDC_ALL_PATHS also
        // returns the paths that are currently off, so flipping the active bit is enough.
        
        public static void GetScaleRange(string tag, out int min, out int cur, out int max)
        {
            min = 0; cur = 0; max = 0;
            PATH[] paths = Paths();
            int i = FindPath(tag);
            if (paths == null || i < 0) return;
            var g = new SCALE_GET();
            g.header.type = -3;
            g.header.size = (uint)Marshal.SizeOf(typeof(SCALE_GET));
            g.header.adapterId = paths[i].source.adapterId;
            g.header.id = paths[i].source.id;
            if (DisplayConfigGetDeviceInfo(ref g) == 0)
            {
                min = g.min; cur = g.cur; max = g.max;
            }
        }

        // The API takes a step index, not a percentage, and the steps depend on the mode:
        // at 2560x1600 they are 125, 150, 175, 200 and 225 percent.
        public static int ScalePercentFor(string tag, int index)
        {
            int min, cur, max;
            GetScaleRange(tag, out min, out cur, out max);
            if (index < min || index > max) return -1;
            int first = 100 + (min + 1) * 25;
            return first + (index - min) * 25;
        }

        public static int IndexOfPercent(string tag, int percent)
        {
            int min, cur, max;
            GetScaleRange(tag, out min, out cur, out max);
            for (int i = min; i <= max; i++)
                if (ScalePercentFor(tag, i) == percent) return i;
            return -1;
        }

        public static bool SetScale(string tag, int index)
        {
            PATH[] paths = Paths();
            int i = FindPath(tag);
            if (paths == null || i < 0) return false;
            var s = new SCALE_SET();
            s.header.type = -4;
            s.header.size = (uint)Marshal.SizeOf(typeof(SCALE_SET));
            s.header.adapterId = paths[i].source.adapterId;
            s.header.id = paths[i].source.id;
            s.rel = index;
            return DisplayConfigSetDeviceInfo(ref s) == 0;
        }

        public static Process StartVoxl(Settings s, string deviceName)
        {
            string[] order = OrderedDevices();
            int index = 0;
            for (int i = 0; i < order.Length; i++) if (order[i] == deviceName) index = i;

            var args = new StringBuilder();
            args.AppendFormat("--display {0} --port={1} --tls --title=\"Second display\" ", index, s.Port);
            args.AppendFormat("--enable-basic-auth=true --basic-auth-password=\"{0}\" --addr={1} ", s.Password, s.Addr);
            args.AppendFormat("--framerate={0} --preset={1} --crf={2} --max-kbps={3} --encoder-threads={4}",
                s.Fps, s.Preset, s.Crf, s.MaxMbps * 1000, s.Threads);
            if (!s.Clipboard) args.Append(" --enable-clipboard=false");
            if (!s.Audio) args.Append(" --audio-enabled=false");
            if (s.ReadOnly) args.Append(" --read-only");

            var psi = new ProcessStartInfo(s.VoxlPath, args.ToString());
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.WorkingDirectory = Path.GetDirectoryName(s.VoxlPath);
            // Keep voxl's own log: a hidden window otherwise hides why it stopped.
            psi.RedirectStandardError = true;
            psi.RedirectStandardOutput = true;
            Process p = Process.Start(psi);
            string logPath = Path.Combine(Path.GetDirectoryName(s.VoxlPath), "voxl.log");
            try
            {
                var log = new StreamWriter(new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
                log.AutoFlush = true;
                p.ErrorDataReceived += delegate(object o, DataReceivedEventArgs e) { if (e.Data != null) log.WriteLine(e.Data); };
                p.OutputDataReceived += delegate(object o, DataReceivedEventArgs e) { if (e.Data != null) log.WriteLine(e.Data); };
                p.BeginErrorReadLine();
                p.BeginOutputReadLine();
            }
            catch { }
            return p;
        }

        // voxl lists displays the way it prints them: the primary (at 0,0) first, then by
        // top and left. --display is an index into that order.
        private static string[] OrderedDevices()
        {
            var list = new List<string>();
            var boxes = new List<int[]>();
            PATH[] paths = Paths();
            if (paths == null) return new string[0];
            for (int i = 0; i < paths.Length; i++)
            {
                var s = new SRCNM();
                s.header.type = 1;
                s.header.size = (uint)Marshal.SizeOf(typeof(SRCNM));
                s.header.adapterId = paths[i].source.adapterId;
                s.header.id = paths[i].source.id;
                if (DisplayConfigGetDeviceInfo(ref s) != 0) continue;
                var dm = new DEVMODE();
                dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                if (!EnumDisplaySettingsW(s.gdi, -1, ref dm)) continue;
                list.Add(s.gdi);
                boxes.Add(new[] { dm.dmPositionX, dm.dmPositionY });
            }
            for (int i = 0; i < list.Count; i++)
            {
                int best = i;
                for (int j = i + 1; j < list.Count; j++) if (Less(boxes[j], boxes[best])) best = j;
                if (best != i)
                {
                    string t = list[i]; list[i] = list[best]; list[best] = t;
                    int[] b = boxes[i]; boxes[i] = boxes[best]; boxes[best] = b;
                }
            }
            return list.ToArray();
        }

        private static bool Less(int[] a, int[] b)
        {
            bool ao = !(a[0] == 0 && a[1] == 0);
            bool bo = !(b[0] == 0 && b[1] == 0);
            if (ao != bo) return !ao;
            if (a[1] != b[1]) return a[1] < b[1];
            return a[0] < b[0];
        }
    }
}
