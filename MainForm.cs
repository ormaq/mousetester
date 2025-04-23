using MouseTester.Raw;
using System.Diagnostics;

namespace MouseTester
{

     internal sealed record PollSample(
        int    Index,
        double TimeMs,
        int    Dx,  int Dy,
        double SumX, double SumY,
        double IntervalMs,
        double XVelocity,
        double YVelocity,
        double VelMag);


    public sealed class MainForm : Form
    {
        // ── UI Controls ───────────────────────────────────────────────
        private readonly ListView _infoView;

        private readonly TextBox  _dpiBox;
        private readonly Button   _measureBtn;

        private readonly ListBox  _eventLog;

        private readonly Button   _ignoreMoveBtn;
        private readonly Button   _ignoreButtonsBtn;
        private readonly Button   _topMostBtn;

        private readonly Button   _pollBtn;
        private readonly Label    _pollLbl;

        private readonly Button   _csvBtn;
        private readonly Label    _csvLbl;

        // ── State ─────────────────────────────────────────────────────
        private readonly RawInputListener _raw;
        private readonly Stopwatch _sw = new();
        private readonly List<MouseSample> _csv = new();

        private readonly Dictionary<ushort, long> _downTimestamps = new();

        private readonly List<PollSample> _pollSamples = new();

        private readonly Button _graphBtn;

        private bool _csvRec;
        private bool _ignoreMove;
        private bool _ignoreButtons;

        // polling
        private bool _polling;
        private readonly List<long> _intervals = new();
        private long _lastTs;
        private long _sumX, _sumY, _path;

        private struct MouseSample
        {
            public long Ts;
            public int  Dx, Dy;
        }

        public MainForm()
        {
            Text = "🖱  Mouse-Tester Kit";
            BackColor = Color.FromArgb(28, 28, 30);
            ClientSize = new Size(780, 500);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            // ===== Device info ==================================================
            var infoGrp = new GroupBox
            {
                Text = "Device info",
                ForeColor = Color.White,
                Location = new Point(10, 10),
                Size = new Size(360, 185)
            };
            _infoView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                HeaderStyle = ColumnHeaderStyle.None,
                BackColor = Color.FromArgb(45,45,48),
                ForeColor = Color.White
            };
            _infoView.Columns.Add("", 340);
            infoGrp.Controls.Add(_infoView);

            // Populate
            foreach (var m in MouseInfoProvider.GetConnectedMice())
            {
                string header = string.IsNullOrEmpty(m.InterfaceType)
                    ? m.Name
                    : $"{m.Name}  [{m.InterfaceType}]";
                _infoView.Items.Add(header);
                _infoView.Items.Add($"⤷ Mfr: {m.Manufacturer}");
                _infoView.Items.Add($"⤷ Driver: {m.DriverVersion}");
                _infoView.Items.Add($"⤷ PNP: {m.PNPDeviceID}");
                _infoView.Items.Add("");
            }

            // ===== DPI tester ===================================================
            var dpiGrp = new GroupBox
            {
                Text = "DPI tester",
                ForeColor = Color.White,
                Location = new Point(10, 205),
                Size = new Size(360, 85)
            };
            _dpiBox = new TextBox
            {
                ReadOnly = false,
                Location = new Point(18, 32),
                Size = new Size(80, 23),
                BackColor = Color.FromArgb(45,45,48),
                ForeColor = Color.White,
                Text = "1600"
            };
            _measureBtn = StyledButton("Measure…", new Point(110, 31), MeasureDpi);
            dpiGrp.Controls.AddRange([_dpiBox, _measureBtn ]);

            // ===== Polling tester ==============================================
            var pollGrp = new GroupBox
            {
                Text = "Polling-rate tester",
                ForeColor = Color.White,
                Location = new Point(10, 300),
                Size = new Size(360, 180)         
            };
            _pollBtn  = StyledButton("Start", new Point(18, 28), TogglePolling);
            _pollLbl  = new Label
            {
                ForeColor = Color.Yellow,
                Location = new Point(18, 60),
                Size = new Size(320, 85)
            };
            _graphBtn = StyledButton("Graph…", new Point(260, 28), ShowGraph);
            _graphBtn.Enabled = false;
            pollGrp.Controls.AddRange([ _pollBtn, _graphBtn, _pollLbl ]);


            // ===== Event log ====================================================
            _eventLog = new ListBox
            {
                Location = new Point(380, 10),
                Size = new Size(380, 330),
                BackColor = Color.FromArgb(45,45,48),
                ForeColor = Color.White
            };

            // ===== Misc buttons =================================================
            _ignoreMoveBtn    = StyledButton("Ignore move: OFF",   new Point(380, 355), ToggleIgnoreMove, 120);
            _ignoreButtonsBtn = StyledButton("Ignore buttons: OFF",new Point(510, 355), ToggleIgnoreButtons, 140);
            _topMostBtn       = StyledButton("TopMost: OFF",       new Point(660, 355), ToggleTopMost, 90);

            // ===== CSV ==========================================================
            _csvBtn = StyledButton("Record CSV", new Point(380, 410), ToggleCsv, 100);
            _csvLbl = new Label
            {
                ForeColor = Color.White,
                Location = new Point(490, 415),
                Size = new Size(270, 25)
            };

            Controls.AddRange(new Control[]
            {
                infoGrp, dpiGrp, pollGrp,
                _eventLog,
                _ignoreMoveBtn, _ignoreButtonsBtn, _topMostBtn,
                _csvBtn, _csvLbl
            });

            // ==== RAWINPUT subscription (global) ===============================
            _raw = new RawInputListener(Handle);
            _raw.MouseInput += Raw_MouseInput;
        }

            // ───────────────────────────────────────────────────────────────────
            //  UI helper
            // ───────────────────────────────────────────────────────────────────
            private static Button StyledButton(string text, Point loc,
                                            EventHandler onClick, int w = 90)
            {
                var btn = new Button
                {
                    Text      = text,
                    Location  = loc,
                    Size      = new Size(w, 25),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(60, 60, 65),
                    ForeColor = Color.White
                };
                btn.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
                btn.Click += onClick;      

                return btn;
            }
        // (C# 9 target-typed new allowed, but kept explicit for clarity)

        // ───────────────────────────────────────────────────────────────────
        //  Button handlers
        private void ToggleIgnoreMove(object? s, EventArgs e)
        {
            _ignoreMove = !_ignoreMove;
            _ignoreMoveBtn.Text = $"Ignore move: {(_ignoreMove ? "ON" : "OFF")}";
        }
        private void ToggleIgnoreButtons(object? s, EventArgs e)
        {
            _ignoreButtons = !_ignoreButtons;
            _ignoreButtonsBtn.Text = $"Ignore buttons: {(_ignoreButtons ? "ON" : "OFF")}";
        }
        private void ToggleTopMost(object? s, EventArgs e)
        {
            TopMost = !TopMost;
            _topMostBtn.Text = $"TopMost: {(TopMost ? "ON" : "OFF")}";
        }

        // ───────────────────────────────────────────────────────────────────
        //  DPI tester
        private void MeasureDpi(object? s, EventArgs e)
        {
            using var dlg = new DPIMeasureForm();
            if (dlg.ShowDialog(this) == DialogResult.OK)
                _dpiBox.Text = dlg.CalculatedDpi.ToString("F0");
        }

        // ───────────────────────────────────────────────────────────────────
        //  Polling tester
        private void TogglePolling(object? s, EventArgs e)
        {
            if (_polling) FinishPolling();
            else StartPolling();
        }
         private void StartPolling()
        {
            _polling = true;
            _graphBtn.Enabled = false;
            _pollBtn.Text = "Stop";
            _pollLbl.Text = "Move mouse…";

            _intervals.Clear();
            _lastTs = 0;
            _sumX = _sumY = _path = 0;
            _pollSamples.Clear();
        }
        private void FinishPolling()
        {
            _polling = false;
            _pollBtn.Text = "Start";
            _graphBtn.Enabled = _pollSamples.Count > 1;

            if (_intervals.Count < 2)
            {
                _pollLbl.Text = "Not enough data.";
                return;
            }
            double avgMs = _intervals.Average();
            double hz    = 1000.0 / avgMs;

            _pollLbl.Text =
                $"Events: {_intervals.Count}\r\n" +
                $"Rate: {hz:F0} Hz (avg {avgMs:F1} ms)\r\n" +
                $"Sum X: {_sumX} counts  {ToCm(_sumX):F1} cm\r\n" +
                $"Sum Y: {_sumY} counts  {ToCm(_sumY):F1} cm\r\n" +
                $"Path : {_path} counts  {ToCm(_path):F1} cm";
        }

         //  Graph button
        private void ShowGraph(object? s, EventArgs e)
        {
            if (_pollSamples.Count > 1)
                using (var g = new GraphForm(_pollSamples))
                    g.ShowDialog(this);
        }
        private double ToCm(long counts)
        {
            if (!double.TryParse(_dpiBox.Text, out var dpi) || dpi <= 0) return 0;
            return counts / dpi * 2.54;
        }

        // ───────────────────────────────────────────────────────────────────
        //  CSV recorder
        private void ToggleCsv(object? s, EventArgs e)
        {
            if (_csvRec) StopCsv(); else StartCsv();
        }
        private void StartCsv()
        {
            _csvRec = true;
            _csvBtn.Text = "Stop";
            _csvLbl.Text = "Recording…";
            _csv.Clear();
            _sw.Restart();
        }
        private void StopCsv()
        {
            _csvRec = false;
            _csvBtn.Text = "Record CSV";
            _sw.Stop();

            if (_csv.Count == 0) { _csvLbl.Text = "No samples."; return; }

            using var sfd = new SaveFileDialog
            {
                Filter = "CSV files|*.csv",
                DefaultExt = "csv"
            };
            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                System.IO.File.WriteAllLines(sfd.FileName,
                    new[] { "Timestamp_ms,Dx,Dy" }
                    .Concat(_csv.Select(p => $"{p.Ts},{p.Dx},{p.Dy}")));
                _csvLbl.Text = "Saved.";
            }
            else _csvLbl.Text = "Cancelled.";
        }

        // ───────────────────────────────────────────────────────────────────
        //  RAWINPUT handler
        private void Raw_MouseInput(object? s, RawMouseEventArgs e)
        {
            // CSV
            if (_csvRec) _csv.Add(new MouseSample{ Ts=_sw.ElapsedMilliseconds,
                                                   Dx=e.Dx, Dy=e.Dy });

            // Poll
             if (_polling)
            {
                if (_lastTs != 0) _intervals.Add(e.Timestamp - _lastTs);
                double interval = _lastTs == 0 ? 0 : (e.Timestamp - _lastTs);
                _lastTs = e.Timestamp;

                _sumX += e.Dx;
                _sumY += e.Dy;
                _path += (long)Math.Sqrt(e.Dx * e.Dx + e.Dy * e.Dy);

                double vx = interval > 0 ? e.Dx / interval * 1000 : 0; // counts/s
                double vy = interval > 0 ? e.Dy / interval * 1000 : 0;

                _pollSamples.Add(new PollSample(
                    _pollSamples.Count,
                    (_lastTs - _pollSamples.FirstOrDefault()?.TimeMs) ?? 0,
                    e.Dx, e.Dy,
                    _sumX, _sumY,
                    interval,
                    vx, vy, Math.Sqrt(vx*vx + vy*vy)));
            }

            // Logging (obey ignore switches)
            var msgs = new List<string>();

            if (!_ignoreMove && (e.Dx != 0 || e.Dy != 0))
                msgs.Add($"Move Δx={e.Dx} Δy={e.Dy}");

            if (!_ignoreButtons)
            {
                // wheel
                if (e.WheelDelta != 0)
                    msgs.Add($"Wheel {(e.WheelDelta > 0 ? "↑" : "↓")} {e.WheelDelta}");

                // buttons
                var bf = e.ButtonFlags;
                void log(ushort flag, string txt)
                { if ((bf & flag) != 0) msgs.Add(txt); }

                log(0x0001, "LMB down");  log(0x0002, "LMB up");
                log(0x0004, "RMB down");  log(0x0008, "RMB up");
                log(0x0010, "MMB down");  log(0x0020, "MMB up");
                log(0x0040, "X1 down");   log(0x0080, "X1 up");
                log(0x0100, "X2 down");   log(0x0200, "X2 up");

                // click-duration
                void clickDur(ushort downFlag, ushort upFlag)
                {
                    if ((bf & downFlag) != 0)
                        _downTimestamps[downFlag] = e.Timestamp;
                    if ((bf & upFlag) != 0 && _downTimestamps.TryGetValue(downFlag, out long t0))
                    {
                        msgs.Add($"Click {upFlag switch {0x0002=>"LMB",0x0008=>"RMB",0x0020=>"MMB",0x0080=>"X1",0x0200=>"X2", _=>"?"}} duration {e.Timestamp-t0} ms");
                        _downTimestamps.Remove(downFlag);
                    }
                }
                clickDur(0x0001,0x0002);
                clickDur(0x0004,0x0008);
                clickDur(0x0010,0x0020);
                clickDur(0x0040,0x0080);
                clickDur(0x0100,0x0200);
            }

            foreach (var m in msgs)
            {
                _eventLog.Items.Insert(0, $"{DateTime.Now:HH:mm:ss.fff}  {m}");
                if (_eventLog.Items.Count > 200) _eventLog.Items.RemoveAt(200);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _raw.Dispose();
            base.OnFormClosing(e);
        }
    }
}
