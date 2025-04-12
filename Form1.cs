using System.Diagnostics;

namespace SimpleWinFormsApp
{    
    public partial class Form1 : Form
    {
        private TextBox resolutionTextBox;
        private Button measureButton;
        private Label resolutionLabel;
        private Button collectButton;
        private Label mouseDataLabel;
        private Label statusLabel;
        private bool isCollecting = false;
        private GlobalMouseHook mouseHook;
        private List<MouseDataPoint> mouseData;
        private Stopwatch stopwatch;
        private System.Windows.Forms.Timer updateTimer;
        private Point lastPosition;
        private long lastTimestamp;

        private class MouseDataPoint
        {
            public long Timestamp { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public double XVelocity { get; set; }
            public double YVelocity { get; set; }
            public long Interval { get; set; }
        }     
          public Form1()
        {
            InitializeComponent();
            InitializeCustomComponents();
            ConfigureForm();
            mouseData = new List<MouseDataPoint>();
            stopwatch = new Stopwatch();
            
            // Initialize timer for continuous monitoring
            updateTimer = new System.Windows.Forms.Timer
            {
                Interval = 1 // 1ms interval for high precision
            };
            updateTimer.Tick += UpdateTimer_Tick;
        }
        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            if (isCollecting)
            {
                RecordMouseData();
            }
        }
        private void InitializeCustomComponents()
        {
            // Resolution Label
            resolutionLabel = new Label
            {
                Text = "Resolution (DPI):",
                ForeColor = Color.White,
                Location = new Point(20, 20),
                AutoSize = true
            };

            // Resolution TextBox
            resolutionTextBox = new TextBox
            {
                Location = new Point(150, 20),
                Size = new Size(100, 23),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = false
            };

            // Measure Button
            measureButton = new Button
            {
                Text = "Measure",
                Location = new Point(260, 19),
                Size = new Size(75, 25),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 65),
                ForeColor = Color.White
            };
            measureButton.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
            measureButton.Click += MeasureButton_Click;

            mouseDataLabel = new Label
            {
                Text = "Mouse Data:",
                ForeColor = Color.White,
                Location = new Point(20, 60),
                AutoSize = true
            };

            collectButton = new Button
            {
                Text = "Collect",
                Location = new Point(150, 55),
                Size = new Size(75, 25),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 65),
                ForeColor = Color.White
            };
            collectButton.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
            collectButton.Click += CollectButton_Click;

            statusLabel = new Label
            {
                Text = "Press Collect to record mouse data",
                ForeColor = Color.White,
                Location = new Point(20, 100),
                AutoSize = true
            };

            // Add all controls to form
            Controls.Add(resolutionLabel);
            Controls.Add(resolutionTextBox);
            Controls.Add(measureButton);
            Controls.Add(mouseDataLabel);
            Controls.Add(collectButton);
            Controls.Add(statusLabel);
        }

        private void ConfigureForm()
        {
            Text = "Input Device Tester";
            ClientSize = new Size(400, 140); // Increased height to accommodate new controls
            BackColor = Color.FromArgb(28, 28, 30);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            TopMost = true;
            StartPosition = FormStartPosition.CenterScreen;
        }

        private void MeasureButton_Click(object sender, EventArgs e)
        {
            using (var measureForm = new DPIMeasureForm())
            {
                if (measureForm.ShowDialog() == DialogResult.OK)
                {
                    resolutionTextBox.Text = measureForm.CalculatedDPI.ToString("F0");
                }
            }
        }

        private void CollectButton_Click(object sender, EventArgs e)
        {
            if (!isCollecting)
            {
                StartCollecting();
            }
            else
            {
                StopCollecting();
            }
        }

        private void StartCollecting()
        {
            mouseData.Clear();
            isCollecting = true;
            collectButton.Text = "Stop";
            statusLabel.Text = "Press Stop to save data";
            
            mouseHook = new GlobalMouseHook();
            mouseHook.MouseLeftButtonDown += MouseHook_MouseLeftButtonDown;
            mouseHook.MouseLeftButtonUp += MouseHook_MouseLeftButtonUp;
            
            lastPosition = Cursor.Position;
            lastTimestamp = 0;
            
            stopwatch.Reset();
            stopwatch.Start();
            updateTimer.Start();
        }

        private void StopCollecting()
        {
            updateTimer.Stop();
            isCollecting = false;
            collectButton.Text = "Collect";
            statusLabel.Text = "Press Collect to record mouse data";
            stopwatch.Stop();
            
            if (mouseHook != null)
            {
                mouseHook.Dispose();
                mouseHook = null;
            }

            SaveMouseData();
        }

        private void MouseHook_MouseLeftButtonDown(object sender, EventArgs e)
        {
            RecordMouseData();
        }

        private void MouseHook_MouseLeftButtonUp(object sender, EventArgs e)
        {
            RecordMouseData();
        }

        private void RecordMouseData()
        {
            var currentPosition = Cursor.Position;
            var timestamp = stopwatch.ElapsedMilliseconds;

            // Only record if there's movement or it's a click event
            if (currentPosition != lastPosition || timestamp - lastTimestamp >= 100) // Record at least every 100ms
            {
                var dataPoint = new MouseDataPoint
                {
                    Timestamp = timestamp,
                    X = currentPosition.X,
                    Y = currentPosition.Y,
                    Interval = mouseData.Count > 0 ? timestamp - mouseData[mouseData.Count - 1].Timestamp : 0
                };

                if (mouseData.Count > 0)
                {
                    var prevPoint = mouseData[mouseData.Count - 1];
                    var deltaTime = (double)(timestamp - prevPoint.Timestamp) / 1000.0;
                    if (deltaTime > 0)
                    {
                        dataPoint.XVelocity = (currentPosition.X - prevPoint.X) / deltaTime;
                        dataPoint.YVelocity = (currentPosition.Y - prevPoint.Y) / deltaTime;
                    }
                }

                mouseData.Add(dataPoint);
                lastPosition = currentPosition;
                lastTimestamp = timestamp;
            }
        }

       private void SaveMouseData()
        {
            if (mouseData.Count == 0)
            {
                MessageBox.Show("No data collected.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                saveFileDialog.FilterIndex = 1;
                saveFileDialog.DefaultExt = "csv";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        using (var writer = new System.IO.StreamWriter(saveFileDialog.FileName))
                        {
                            writer.WriteLine("Timestamp,X,Y,XVelocity,YVelocity,Interval");
                            foreach (var point in mouseData)
                            {
                                writer.WriteLine($"{point.Timestamp},{point.X},{point.Y},{point.XVelocity:F2},{point.YVelocity:F2},{point.Interval}");
                            }
                        }
                        MessageBox.Show("Data saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        }
    }
}
