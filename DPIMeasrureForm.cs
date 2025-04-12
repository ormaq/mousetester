namespace SimpleWinFormsApp
{
    public class DPIMeasureForm : Form
    {
        private TextBox distanceTextBox;
        private Label instructionsLabel;
        private Label statusLabel;
        private Button startButton;
        private Button cancelButton;
        public double CalculatedDPI { get; private set; }

        private GlobalMouseHook mouseHook;
        private Point startPoint;
        private bool isMeasuring = false;

        public DPIMeasureForm()
        {
            InitializeComponents();
            ConfigureForm();
        }

        private void InitializeComponents()
        {
            // Instructions Label
            instructionsLabel = new Label
            {
                Text = "Press the left mouse button and move your mouse the specified distance to check your DPI.\r\n" +
                      "(Recommended: Use a ruler to measure the distance)\r\n" +
                      "Alternatively, enter your exact mousepad size and sweep the full distance.",
                ForeColor = Color.White,
                Location = new Point(20, 20),
                Size = new Size(360, 60),
                AutoSize = false
            };

            // Distance Label
            var distanceLabel = new Label
            {
                Text = "Distance (inches):",
                ForeColor = Color.White,
                Location = new Point(20, 100),
                AutoSize = true
            };

            // Distance TextBox
            distanceTextBox = new TextBox
            {
                Location = new Point(120, 97),
                Size = new Size(100, 23),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Text = "8"  // Default to 8 inches
            };

            // Status Label
            statusLabel = new Label
            {
                Text = "",
                ForeColor = Color.Yellow,
                Location = new Point(20, 130),
                Size = new Size(360, 20),
                AutoSize = false
            };

            // Start Button
            startButton = new Button
            {
                Text = "Start Measurement",
                Location = new Point(20, 160),
                Size = new Size(150, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 65),
                ForeColor = Color.White
            };
            startButton.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
            startButton.Click += StartButton_Click;

            // Cancel Button
            cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(180, 160),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 65),
                ForeColor = Color.White,
                DialogResult = DialogResult.Cancel
            };
            cancelButton.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);

            // Add controls
            Controls.AddRange(new Control[] {
                instructionsLabel,
                distanceLabel,
                distanceTextBox,
                statusLabel,
                startButton,
                cancelButton
            });
        }

        private void ConfigureForm()
        {
            Text = "Measure DPI";
            ClientSize = new Size(400, 210);
            BackColor = Color.FromArgb(28, 28, 30);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            TopMost = true;
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            if (!double.TryParse(distanceTextBox.Text, out double distance) || distance <= 0)
            {
                MessageBox.Show("Please enter a valid distance in inches.", "Invalid Input",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Initialize mouse hook
            mouseHook = new GlobalMouseHook();
            mouseHook.MouseLeftButtonDown += MouseHook_MouseLeftButtonDown;
            mouseHook.MouseLeftButtonUp += MouseHook_MouseLeftButtonUp;

            isMeasuring = true;
            statusLabel.Text = "Tracking... Press and hold the left mouse button and move your mouse.";
            startButton.Enabled = false;
            distanceTextBox.Enabled = false;
        }

        private void MouseHook_MouseLeftButtonDown(object sender, EventArgs e)
        {
            if (isMeasuring)
            {
                startPoint = Cursor.Position;
                // Optionally, you can provide feedback to the user here
            }
        }

        private void MouseHook_MouseLeftButtonUp(object sender, EventArgs e)
        {
            if (isMeasuring)
            {
                var endPoint = Cursor.Position;
                var pixelsMoved = Math.Sqrt(
                    Math.Pow(endPoint.X - startPoint.X, 2) +
                    Math.Pow(endPoint.Y - startPoint.Y, 2)
                );

                if (double.TryParse(distanceTextBox.Text, out double inches) && inches > 0)
                {
                    CalculatedDPI = pixelsMoved / inches;
                    isMeasuring = false;
                    statusLabel.Text = "Measurement complete.";
                    mouseHook.Dispose();
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (mouseHook != null)
            {
                mouseHook.Dispose();
            }
            base.OnFormClosing(e);
        }
    }
}
