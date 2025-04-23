using ScottPlot.WinForms;
using Color = System.Drawing.Color;
using SD = System.Drawing.Color;
using SP = ScottPlot.Color;

namespace MouseTester
{
    internal sealed class GraphForm : Form
    {
        private readonly FormsPlot     _fp;
        private readonly NumericUpDown _nudStart;
        private readonly NumericUpDown _nudEnd;
        private readonly ComboBox      _cmbPlot;
        private readonly IList<PollSample> _samples;

        public GraphForm(IList<PollSample> samples)
        {
            _samples = samples;

            // ------- window look ----------
            Text          = "Polling-data graphs";
            BackColor     = Color.FromArgb(28, 28, 30);
            ForeColor     = Color.White;
            ClientSize    = new Size(900, 550);
            StartPosition = FormStartPosition.CenterParent;

            // ------- ScottPlot control ----
            _fp = new FormsPlot
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 48)
            };

            var plt = _fp.Plot;
            SP dark = SP.FromColor(SD.FromArgb(45, 45, 48));
            plt.FigureBackground.Color = dark;
            plt.DataBackground.Color   = dark;

            plt.Axes.Color(SP.FromColor(SD.White));

            // ------- top panel ------------
            var pnl = new Panel { Dock = DockStyle.Top, Height = 40 };
            Controls.AddRange(new Control[] { _fp, pnl });

            // plot‐type selector
            _cmbPlot = new ComboBox
            {
                Location      = new Point(10, 8),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width         = 170
            };
            _cmbPlot.Items.AddRange(new[]
            {
                "ΔX vs time",
                "ΔY vs time",
                "√(ΔX²+ΔY²) vs time",
                "Interval vs time",
                "X-velocity vs time",
                "Y-velocity vs time",
                "XY-velocity vs time",
                "Sum X vs Sum Y"
            });
            _cmbPlot.SelectedIndex = 0;
            _cmbPlot.SelectedIndexChanged += (_, _) => Render();
            pnl.Controls.Add(_cmbPlot);

            // range selectors
            int max = _samples.Count - 1;
            var btnReset = new Button()
            {
                Text      = "Reset View",
                Location  = new Point(330, 8),
                AutoSize  = true,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White
            };
            _nudStart = new NumericUpDown();
            _nudEnd   = new NumericUpDown();
            _nudStart.SetRange(1, max);
            _nudEnd  .SetRange(0, max);
            _nudEnd.Value = max;
            _nudStart.Location = new Point(200, 8);
            _nudEnd  .Location = new Point(260, 8);
            _nudStart.ValueChanged += (_, _) => Render();
            _nudEnd  .ValueChanged += (_, _) => Render();
            btnReset.Click += (_, _) =>
            {
                // reset X/Y limits to fit all data
                _fp.Plot.Axes.AutoScale();      // :contentReference[oaicite:0]{index=0}
                _fp.Refresh();
            };
            pnl.Controls.Add(btnReset);
            pnl.Controls.AddRange(new Control[] { _nudStart, _nudEnd });

            Render();
        }

        private void Render()
        {
            if (_samples.Count < 2) return;

            int i0 = (int)_nudStart.Value;
            int i1 = (int)_nudEnd.Value;
            if (i1 <= i0) i1 = i0 + 1;
            if (i1 >= _samples.Count) i1 = _samples.Count - 1;

            var range = _samples.Skip(i0).Take(i1 - i0 + 1).ToArray();

            double[] t    = range.Select(s => s.TimeMs                    ).ToArray();
            double[] dx   = range.Select(s => (double)s.Dx                ).ToArray();
            double[] dy   = range.Select(s => (double)s.Dy                ).ToArray();
            double[] dxy  = range.Select(s => Math.Sqrt(s.Dx*s.Dx + s.Dy*s.Dy)).ToArray();
            double[] ivl  = range.Select(s => s.IntervalMs                ).ToArray();
            double[] vx   = range.Select(s => s.XVelocity                 ).ToArray();
            double[] vy   = range.Select(s => s.YVelocity                 ).ToArray();
            double[] vxy  = range.Select(s => s.VelMag                    ).ToArray();
            double[] sumX = range.Select(s => s.SumX                      ).ToArray();
            double[] sumY = range.Select(s => s.SumY                      ).ToArray();

            var plt = _fp.Plot;
            plt.Clear();

            // use the new Add.Scatter API :contentReference[oaicite:1]{index=1}
            switch (_cmbPlot.SelectedItem?.ToString())
            {
                case "ΔX vs time":          plt.Add.Scatter(t, dx);                     break;
                case "ΔY vs time":          plt.Add.Scatter(t, dy);                     break;
                case "√(ΔX²+ΔY²) vs time":  plt.Add.Scatter(t, dxy);                    break;
                case "Interval vs time":    plt.Add.Scatter(t, ivl);                    break;
                case "X-velocity vs time":  plt.Add.Scatter(t, vx);                     break;
                case "Y-velocity vs time":  plt.Add.Scatter(t, vy);                     break;
                case "XY-velocity vs time": plt.Add.Scatter(t, vxy);                    break;
                case "Sum X vs CSm Y":      plt.Add.Scatter(sumX, sumY);                break;
            }

            plt.Title(_cmbPlot.SelectedItem?.ToString());
            _fp.Refresh();
        }
    }

    
    internal static class NudExt
    {
        public static void SetRange(this NumericUpDown nud, int min, int max)
        {
            nud.Minimum   = min;
            nud.Maximum   = max;
            nud.Width     = 55;
            nud.ForeColor = Color.White;
            nud.BackColor = Color.FromArgb(45, 45, 48);
        }
    }
}
