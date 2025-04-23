using MouseTester.Raw;

namespace MouseTester
{
    public sealed class DPIMeasureForm : Form
    {
        private readonly TextBox distanceBox;
        private readonly Button   startBtn;
        private readonly Label    infoLabel;
        private readonly Label    statusLabel;

        private RawInputListener? _raw;
        private long   _sumX, _sumY;
        private bool   _tracking;

        public double CalculatedDpi { get; private set; }

        public DPIMeasureForm()
        {
            Text = "DPI Tester (raw-input)";
            BackColor     = Color.FromArgb(28, 28, 30);
            ClientSize    = new Size(420, 210);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; TopMost = true;
            StartPosition = FormStartPosition.CenterParent;

            infoLabel = new Label
            {
                ForeColor = Color.White,
                Text = "1. Enter physical travel distance (inches).\r\n" +
                       "2. Click Start, HOLD left button, move straight,\r\n" +
                       "   release to finish (raw counts are captured).",
                Location = new Point(14, 10),
                Size     = new Size(390, 60)
            };

            var distLbl = new Label
            {
                Text = "Distance (in):",
                ForeColor = Color.White,
                Location = new Point(14, 80),
                AutoSize = true
            };
            distanceBox = new TextBox
            {
                Location = new Point(110, 77),
                Size = new Size(80, 23),
                Text = "8",
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            startBtn = new Button
            {
                Text = "Start",
                Location = new Point(200, 75),
                Size = new Size(75, 25),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 65),
                ForeColor = Color.White
            };
            startBtn.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
            startBtn.Click += (_, _) => BeginTracking();

            statusLabel = new Label
            {
                ForeColor = Color.Yellow,
                Location = new Point(14, 120),
                Size = new Size(390, 40)
            };

            Controls.AddRange(new Control[] { infoLabel, distLbl, distanceBox, startBtn, statusLabel });
        }

        private void BeginTracking()
        {
            if (!double.TryParse(distanceBox.Text, out var inches) || inches <= 0)
            {
                MessageBox.Show("Enter a positive distance.", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _sumX = _sumY = 0;
            _tracking = true;
            distanceBox.Enabled = false;
            startBtn.Enabled    = false;
            statusLabel.Text    = "Hold LMB - move mouse…";

            _raw = new RawInputListener(Handle);
            _raw.MouseInput += Raw_MouseInput;
        }

        private void Raw_MouseInput(object? s, RawMouseEventArgs e)
        {
            const ushort LMB_DOWN = 0x0001, LMB_UP = 0x0002;

            if (e.ButtonFlags == LMB_DOWN)
            {
                _sumX = _sumY = 0;          // fresh run
                _tracking = true;
                return;
            }
            if (e.ButtonFlags == LMB_UP && _tracking)
            {
                FinishMeasurement();
                return;
            }

            if (_tracking)
            {
                _sumX += e.Dx;
                _sumY += e.Dy;
            }
        }

        private void FinishMeasurement()
        {
            _tracking = false;
            if (_raw is not null) { _raw.Dispose(); _raw = null; }

            double inches = double.Parse(distanceBox.Text);
            double counts = Math.Sqrt(_sumX * _sumX + _sumY * _sumY);
            CalculatedDpi = counts / inches;

            statusLabel.Text = $"Result: {CalculatedDpi:F0} CPI";
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _raw?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
