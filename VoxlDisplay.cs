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

    internal sealed class MainForm : Form
    {
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
        private readonly CheckBox _clipboard = new CheckBox();
        private readonly CheckBox _audio = new CheckBox();
        private readonly CheckBox _readOnly = new CheckBox();
        private readonly TextBox _password = new TextBox();
        private readonly Label _status = new Label();
        private readonly Label _hint = new Label();
        private readonly Button _start = new Button();
        private readonly Button _stop = new Button();
        private readonly Button _copy = new Button();
        private readonly Button _qr = new Button();
        private readonly Button _reset = new Button();
        private readonly Button _reload = new Button();
        private readonly Button _install = new Button();
        private readonly NotifyIcon _tray;
        private Process _voxl;
        private bool _busy;
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

            AutoScaleMode = AutoScaleMode.None;
            Text = "Voxl Display - second monitor";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(640, 644);
            BackColor = Color.FromArgb(23, 23, 27);
            ForeColor = Color.FromArgb(235, 235, 240);
            Font = new Font("Segoe UI", 9.75f);

            var title = new Label();
            title.Text = "Second monitor for your tablet";
            title.Font = new Font("Segoe UI Semibold", 15f);
            title.AutoSize = true;
            title.Location = new Point(24, 18);
            Controls.Add(title);

            var sub = new Label();
            sub.Text = "Virtual display plus the voxl stream, LAN only.";
            sub.ForeColor = Color.FromArgb(150, 150, 160);
            sub.AutoSize = true;
            sub.Location = new Point(26, 54);
            Controls.Add(sub);

            AddLabel("Display resolution", 26, 92);
            Combo(_resolution, 26, 112, 220);
            _resolution.DropDownStyle = ComboBoxStyle.DropDown; // custom values allowed
            Controls.Add(_resolution);

            AddLabel("Scaling", 266, 92);
            Combo(_scale, 266, 112, 220);
            Controls.Add(_scale);

            AddLabel("Stream quality", 26, 156);
            Combo(_quality, 26, 176, 220);
            foreach (string q in QualityNames) _quality.Items.Add(q);
            _quality.SelectedIndexChanged += QualityChanged;
            Controls.Add(_quality);

            AddLabel("FPS", 266, 156);
            Combo(_fps, 266, 176, 220);
            foreach (int f in new[] { 30, 50, 60, 90, 120 }) _fps.Items.Add(f.ToString());
            Controls.Add(_fps);

            AddLabel("Encoder preset (latency)", 26, 220);
            Combo(_preset, 26, 240, 220);
            foreach (string p in PresetNames) _preset.Items.Add(p);
            Controls.Add(_preset);

            AddLabel("Max bitrate (Mbps)", 266, 220);
            Number(_maxMbps, 266, 240, 220, 5, 2000);
            Controls.Add(_maxMbps);

            AddLabel("CRF (lower is sharper)", 26, 284);
            Number(_crf, 26, 304, 220, 10, 40);
            Controls.Add(_crf);

            AddLabel("Encoder threads", 266, 284);
            Number(_threads, 266, 304, 220, 1, 32);
            Controls.Add(_threads);

            Check(_clipboard, "Clipboard", 26, 350);
            Check(_audio, "Audio", 150, 350);
            Check(_readOnly, "Read-only viewer", 250, 350);

            AddLabel("Password (type it on the tablet)", 26, 384);
            _password.Location = new Point(26, 404);
            _password.Size = new Size(404, 26);
            _password.BackColor = Color.FromArgb(35, 35, 42);
            _password.ForeColor = Color.White;
            _password.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(_password);

            Button(_qr, "QR code", 440, 402, Color.FromArgb(48, 48, 58), 154);
            _qr.Click += QrClicked;

            var who = new Label();
            who.Text = "Username: any (leave it empty or type anything)";
            who.AutoSize = true;
            who.Location = new Point(26, 434);
            who.ForeColor = Color.FromArgb(120, 160, 140);
            Controls.Add(who);

            Button(_start, "Start", 26, 462, Color.FromArgb(58, 158, 96), 150);
            _start.Click += StartClicked;
            Button(_stop, "Stop", 186, 462, Color.FromArgb(48, 48, 58), 150);
            _stop.Click += StopClicked;
            Button(_copy, "Copy link", 346, 462, Color.FromArgb(48, 48, 58), 150);
            _copy.Click += CopyClicked;

            Button(_reset, "Defaults", 506, 462, Color.FromArgb(48, 48, 58), 110);
            _reset.Click += ResetClicked;

            _status.Text = "stopped";
            _status.Location = new Point(26, 516);
            _status.Size = new Size(560, 22);
            _status.ForeColor = Color.FromArgb(150, 150, 160);
            Controls.Add(_status);

            _hint.Text = "";
            _hint.Location = new Point(26, 536);
            _hint.Size = new Size(560, 40);
            _hint.ForeColor = Color.FromArgb(110, 110, 120);
            Controls.Add(_hint);

            Button(_reload, "Reload displays", 26, 582, Color.FromArgb(48, 48, 58), 170);
            _reload.Click += delegate { LoadDisplays(); ApplySettings(); };
            Button(_install, "Install driver", 206, 582, Color.FromArgb(58, 158, 96), 170);
            _install.Click += InstallDriverClicked;

            _tray = new NotifyIcon();
            _tray.Text = "Voxl Display";
            try { _tray.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { _tray.Icon = SystemIcons.Application; }
            _tray.Visible = true;

            LoadDisplays();
            ApplySettings();
            ScaleToDpi();
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
            Controls.Add(b);
        }

        private void Check(CheckBox c, string text, int x, int y)
        {
            c.Text = text;
            c.Location = new Point(x, y);
            c.AutoSize = true;
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

        private void ScaleToDpi()
        {
            if (DeviceDpi > 96)
            {
                float f = DeviceDpi / 96f;
                SuspendLayout();
                Scale(new SizeF(f, f));
                ClientSize = new Size((int)(640 * f), (int)(644 * f));
                ResumeLayout(true);
            }
            if (!File.Exists(_s.VoxlPath))
                _hint.Text = "voxl.exe was not found next to this app. Put both in one folder.";
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

        private void StartClicked(object sender, EventArgs e)
        {
            if (_busy) return;
            if (_device == null) { MessageBox.Show("The virtual display was not found."); return; }
            if (!File.Exists(_s.VoxlPath)) { MessageBox.Show("voxl.exe was not found: " + _s.VoxlPath); return; }

            _busy = true;
            try
            {
                Status("applying display settings...", false);

                int w, h;
                if (!Display.ParseMode(_resolution.Text.Trim(), out w, out h))
                {
                    MessageBox.Show("Resolution must look like 2560x1600.", "Voxl Display");
                    return;
                }
                if (!_resolution.Items.Contains(_resolution.Text.Trim()))
                {
                    // The driver only offers the modes in its settings file. Ask before
                    // writing a new one; the device restarts for a second.
                    DialogResult add = MessageBox.Show(
                        "The driver does not offer " + w + "x" + h + ".\r\n\r\n" +
                        "Add it to the driver's mode list? The display restarts for a second.",
                        "Add custom resolution", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (add == DialogResult.Yes)
                    {
                        Status("adding " + w + "x" + h + " to the driver...", false);
                        string problem = Display.AddMode(w, h, 60);
                        if (problem != null)
                        {
                            MessageBox.Show("Could not add the mode: " + problem, "Voxl Display");
                            return;
                        }
                        _resolution.Items.Clear();
                        foreach (string m in Display.ListModes(_device)) _resolution.Items.Add(m);
                        _resolution.Text = w + "x" + h;
                        // The device restarted, so its GDI name and the scaling steps are
                        // new: re-resolve before touching the display again.
                        string refreshed = Display.FindDeviceName(MonitorTag);
                        if (refreshed == null)
                        {
                            MessageBox.Show("The virtual display did not come back after the restart.", "Voxl Display");
                            return;
                        }
                        _device = refreshed;
                        _resolution.Items.Clear();
                        foreach (string m in Display.ListModes(_device)) _resolution.Items.Add(m);
                        _resolution.Text = w + "x" + h;
                        Status("added " + w + "x" + h + ", applying...", false);
                    }
                    else
                    {
                        string current = Display.CurrentMode(_device);
                        if (!Display.ParseMode(current, out w, out h))
                        {
                            MessageBox.Show("The display reports no usable mode.", "Voxl Display");
                            return;
                        }
                        Status("keeping " + current, false);
                        _resolution.Text = current;
                    }
                }
                string r = Display.SetMode(_device, w, h, 60);
                if (r != "ok") { Status("resolution: " + r, true); return; }
                _s.Resolution = _resolution.Text.Trim();

                int pct;
                if (int.TryParse(_scale.SelectedItem.ToString().TrimEnd('%'), out pct))
                {
                    int idx = Display.IndexOfPercent(MonitorTag, pct);
                    if (idx >= 0) Display.SetScale(MonitorTag, idx);
                    _s.ScalePercent = pct;
                }

                _s.Quality = _quality.SelectedItem as string;
                _s.Preset = _preset.SelectedItem as string;
                _s.Crf = (int)_crf.Value;
                _s.MaxMbps = (int)_maxMbps.Value;
                _s.Fps = int.Parse(_fps.SelectedItem.ToString());
                _s.Threads = (int)_threads.Value;
                _s.Clipboard = _clipboard.Checked;
                _s.Audio = _audio.Checked;
                _s.ReadOnly = _readOnly.Checked;
                _s.Password = _password.Text;
                _s.Save();

                KillVoxl();
                _voxl = Display.StartVoxl(_s, _device);
                System.Threading.Thread.Sleep(1500);
                if (_voxl.HasExited)
                {
                    Status("voxl exited with code " + _voxl.ExitCode + ", see voxl.log", true);
                    return;
                }
                Status("running at https://" + _s.Addr + ":" + _s.Port, false);
                _hint.Text = "Open the link above on the tablet, or scan the QR code.";
            }
            catch (Exception ex)
            {
                Status("error: " + ex.Message, true);
            }
            finally { _busy = false; }
        }

        private static string[] Items(ComboBox c)
        {
            var list = new List<string>();
            foreach (object o in c.Items) list.Add(o.ToString());
            return list.ToArray();
        }

        private void StopClicked(object sender, EventArgs e)
        {
            KillVoxl();
            Status("stopped", false);
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
            _status.ForeColor = error ? Color.FromArgb(230, 130, 130) : Color.FromArgb(150, 150, 160);
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            _tray.Visible = false;
            _tray.Dispose();
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
