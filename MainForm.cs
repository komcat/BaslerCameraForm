using System;
using System.Drawing;
using System.Windows.Forms;
using BaslerCamera;
using Serilog;

namespace BaslerCameraForm
{
    public partial class MainForm : Form
    {
        private CameraManager cameraManager;
        private ILogger _logger;
        // Form controls
        private PictureBox pictureBox;
        private Button btnConnect;
        private Button btnStartGrab;
        private Button btnStopGrab;
        private Button btnCameraInfo;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblStatus;
        // Add to the MainForm class after the existing button declarations
        private Button btnSaveFrame;
        // Add these private fields at the class level
        private CrosshairOverlay crosshairOverlay;
        private CheckBox chkShowCrosshair;
        private ComboBox cmbCrosshairColor;
        private NumericUpDown nudLineThickness;



        // Add this field at the class level in MainForm.cs
        private MouseCrosshairOverlay mouseCrosshairOverlay;

        public MainForm()
        {
            InitializeComponent();
            InitializeLogger();
            InitializeComponents();
            AddZoomControls(); // Add this line
            AddCrosshairUnitControl(); // Add this line
            SetupEventHandlers();
        }

        private void InitializeLogger()
        {
            _logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console() // Add console output
                .WriteTo.File("camera_log.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();
        }

        private void InitializeComponents()
        {


            pictureBox = new PictureBox
            {
                Dock = DockStyle.None,
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.Black,
                Size = new Size(800, 600),  // Set fixed size
                Location = new Point(0, 0)
            };

            // Add panel to contain PictureBox
            Panel picturePanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.Black,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            picturePanel.Controls.Add(pictureBox);
            //Controls.Add(picturePanel);

            // Create buttons
            btnConnect = new Button
            {
                Text = "Connect Camera",
                Dock = DockStyle.Bottom,
                Height = 30
            };

            btnStartGrab = new Button
            {
                Text = "Start Grab",
                Dock = DockStyle.Bottom,
                Height = 30,
                Enabled = false
            };

            btnStopGrab = new Button
            {
                Text = "Stop Grab",
                Dock = DockStyle.Bottom,
                Height = 30,
                Enabled = false
            };

            btnCameraInfo = new Button
            {
                Text = "Camera Info",
                Dock = DockStyle.Bottom,
                Height = 30,
                Enabled = false
            };

            // Create status strip
            statusStrip = new StatusStrip();
            lblStatus = new ToolStripStatusLabel("Not connected");
            statusStrip.Items.Add(lblStatus);

            // Add controls to form
            Controls.Add(pictureBox);
            Controls.Add(btnStopGrab);
            Controls.Add(btnStartGrab);
            Controls.Add(btnConnect);
            Controls.Add(btnCameraInfo);
            Controls.Add(statusStrip);

            // Create crosshair controls
            chkShowCrosshair = new CheckBox
            {
                Text = "Show Crosshair",
                Dock = DockStyle.Bottom,
                Height = 30,
                Checked = true
            };

            // Create color dropdown
            cmbCrosshairColor = new ComboBox
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbCrosshairColor.Items.AddRange(new object[] { "Blue", "Red", "Green", "Yellow", "White" });
            cmbCrosshairColor.SelectedIndex = 3;

            // Create line thickness control
            nudLineThickness = new NumericUpDown
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                Minimum = 1,
                Maximum = 10,
                Value = 1,
                DecimalPlaces = 1,
                Increment = 0.5M
            };

            // Create label for line thickness
            Label lblLineThickness = new Label
            {
                Text = "Line Thickness:",
                Dock = DockStyle.Bottom,
                Height = 20
            };

            // Create label for color
            Label lblColor = new Label
            {
                Text = "Crosshair Color:",
                Dock = DockStyle.Bottom,
                Height = 20
            };

            // Add controls to form (add these lines before adding the existing buttons)
            Controls.Add(nudLineThickness);
            Controls.Add(lblLineThickness);
            Controls.Add(cmbCrosshairColor);
            Controls.Add(lblColor);
            Controls.Add(chkShowCrosshair);
            // Set form properties
            Text = "Basler Camera Control";
            Size = new System.Drawing.Size(1281, 1025);

            // Add to InitializeComponents() method after the existing button initializations
            btnSaveFrame = new Button
            {
                Text = "Save Frame",
                Dock = DockStyle.Bottom,
                Height = 30,
                Enabled = false
            };

            // Add to the controls section in InitializeComponents()
            Controls.Add(btnSaveFrame);

            // Add to SetupEventHandlers()
            btnSaveFrame.Click += BtnSaveFrame_Click;

            // Update the initialization of mouseCrosshairOverlay in InitializeComponents():
            mouseCrosshairOverlay = new MouseCrosshairOverlay(pictureBox, _logger);

        }

        private void SetupEventHandlers()
        {
            btnConnect.Click += BtnConnect_Click;
            btnStartGrab.Click += BtnStartGrab_Click;
            btnStopGrab.Click += BtnStopGrab_Click;
            btnCameraInfo.Click += BtnCameraInfo_Click;
            FormClosing += MainForm_FormClosing;


            // Initialize crosshair overlay after PictureBox is created
            crosshairOverlay = new CrosshairOverlay(pictureBox);

            // Add crosshair control event handlers
            chkShowCrosshair.CheckedChanged += (s, e) => crosshairOverlay.ShowCrosshair = chkShowCrosshair.Checked;

            cmbCrosshairColor.SelectedIndexChanged += (s, e) =>
            {
                Color selectedColor = Color.Blue; // Default color
                switch (cmbCrosshairColor.SelectedItem.ToString())
                {
                    case "Red":
                        selectedColor = Color.Red;
                        break;
                    case "Green":
                        selectedColor = Color.Green;
                        break;
                    case "Yellow":
                        selectedColor = Color.Yellow;
                        break;
                    case "White":
                        selectedColor = Color.White;
                        break;
                }
                crosshairOverlay.CrosshairColor = selectedColor;
            };

            nudLineThickness.ValueChanged += (s, e) =>
                crosshairOverlay.LineThickness = (float)nudLineThickness.Value;

            // Add the event handler setup in SetupEventHandlers():
            mouseCrosshairOverlay.MouseLocationClicked += MouseCrosshairOverlay_MouseLocationClicked;

        }

        // Add the event handler method:

        // Update the MouseCrosshairOverlay_MouseLocationClicked method
        // Update the MouseCrosshairOverlay_MouseLocationClicked method
        private void MouseCrosshairOverlay_MouseLocationClicked(object sender, MouseLocationEventArgs e)
        {
            // Update status strip with click location including physical distances
            lblStatus.Text = $"Clicked at: ({e.ScaledLocation.X}, {e.ScaledLocation.Y}) " +
                             $"Physical: ({e.PhysicalDistanceX:F1}µm, {e.PhysicalDistanceY:F1}µm) " +
                             $"Zoom: {e.ZoomFactor:F1}x";

            // Log the click event with all information
            _logger.Information(
                "Mouse clicked - Screen: ({ScreenX}, {ScreenY}), " +
                "Scaled: ({ScaledX}, {ScaledY}), " +
                "Physical: ({DistanceX:F1}µm, {DistanceY:F1}µm), " +
                "Zoom: {Zoom:F1}x",
                e.ScreenLocation.X, e.ScreenLocation.Y,
                e.ScaledLocation.X, e.ScaledLocation.Y,
                e.PhysicalDistanceX, e.PhysicalDistanceY,
                e.ZoomFactor
            );
        }
        // Add this new method to handle the save frame functionality
        private void BtnSaveFrame_Click(object sender, EventArgs e)
        {
            try
            {
                using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = "PNG Image|*.png|JPEG Image|*.jpg|Bitmap Image|*.bmp";
                    saveFileDialog.Title = "Save Camera Frame";
                    saveFileDialog.DefaultExt = "png";

                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        if (cameraManager.SaveCurrentFrame(saveFileDialog.FileName))
                        {
                            lblStatus.Text = "Frame saved successfully";
                        }
                        else
                        {
                            MessageBox.Show("Failed to save frame.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error saving frame");
                MessageBox.Show($"Error saving frame: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }



        // Modify BtnConnect_Click to enable the checkbox
        private void BtnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                cameraManager = new CameraManager(pictureBox, _logger);
                if (cameraManager.ConnectToCamera())
                {
                    lblStatus.Text = "Camera connected";
                    btnConnect.Enabled = false;
                    btnStartGrab.Enabled = true;
                    btnCameraInfo.Enabled = true;
                    btnSaveFrame.Enabled = true;

                }
                else
                {
                    MessageBox.Show("Failed to connect to camera.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error connecting to camera");
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void BtnStartGrab_Click(object sender, EventArgs e)
        {
            try
            {
                cameraManager?.StartLiveView();
                btnStartGrab.Enabled = false;
                btnStopGrab.Enabled = true;
                lblStatus.Text = "Grabbing";
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error starting grab");
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnStopGrab_Click(object sender, EventArgs e)
        {
            try
            {
                cameraManager?.StopLiveView();
                btnStartGrab.Enabled = true;
                btnStopGrab.Enabled = false;
                lblStatus.Text = "Stopped";
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error stopping grab");
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnCameraInfo_Click(object sender, EventArgs e)
        {
            try
            {
                string cameraInfo = cameraManager?.GetCameraInfo();
                MessageBox.Show(cameraInfo, "Camera Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting camera info");
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Modify the MainForm_FormClosing method to dispose of the crosshair overlay:
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                cameraManager.DisconnectCamera();
                // Stop live view if it's running
                if (cameraManager != null)
                {
                    if (btnStopGrab.Enabled)  // This indicates live view is active
                    {
                        cameraManager.StopLiveView();
                    }

                    // Update UI to reflect stopped state
                    btnStartGrab.Enabled = true;
                    btnStopGrab.Enabled = false;
                    lblStatus.Text = "Stopped";
                }

                // Dispose of overlays and controls
                crosshairOverlay?.Dispose();

                // Clean up camera manager
                if (cameraManager != null)
                {
                    cameraManager.Dispose();
                    cameraManager = null;
                }



                // Reset UI state
                btnConnect.Enabled = true;
                btnStartGrab.Enabled = false;
                btnStopGrab.Enabled = false;
                btnCameraInfo.Enabled = false;
                btnSaveFrame.Enabled = false;
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Error during form closing");
                MessageBox.Show($"Error while closing: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            try
            {
                if (mouseCrosshairOverlay != null)
                {
                    mouseCrosshairOverlay.MouseLocationClicked -= MouseCrosshairOverlay_MouseLocationClicked;
                    mouseCrosshairOverlay.Dispose();
                }

                // Rest of the existing closing code...
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Error during form closing");
                MessageBox.Show($"Error while closing: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // In MainForm.cs, add these new fields
        private ComboBox cmbZoom;
        private float currentZoom = 1.0f;

        // Add this to InitializeComponents() in MainForm.cs
        private void AddZoomControls()
        {
            // Create zoom dropdown
            cmbZoom = new ComboBox
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            cmbZoom.Items.AddRange(new object[] { "0.5x", "1.0x", "1.5x", "2.0x" });
            cmbZoom.SelectedIndex = 1; // Default to 1.0x

            // Create label for zoom
            Label lblZoom = new Label
            {
                Text = "Zoom:",
                Dock = DockStyle.Bottom,
                Height = 20
            };

            // Add controls to form
            Controls.Add(cmbZoom);
            Controls.Add(lblZoom);

            // Set up event handler
            cmbZoom.SelectedIndexChanged += CmbZoom_SelectedIndexChanged;
        }

        // Add this method to MainForm.cs
        // Update the CmbZoom_SelectedIndexChanged method in MainForm.cs
        // Update the CmbZoom_SelectedIndexChanged method in MainForm.cs
        // Update the CmbZoom_SelectedIndexChanged method in MainForm.cs
        private void CmbZoom_SelectedIndexChanged(object sender, EventArgs e)
        {
            string zoomText = cmbZoom.SelectedItem.ToString();
            float newZoom = float.Parse(zoomText.TrimEnd('x'));

            // Update zoom level
            currentZoom = newZoom;

            // Update camera manager zoom
            if (cameraManager != null)
            {
                cameraManager.SetZoom(currentZoom);
            }

            // Update crosshair overlay
            if (crosshairOverlay != null)
            {
                crosshairOverlay.UpdateZoom(currentZoom);
            }

            // Update mouse crosshair overlay
            if (mouseCrosshairOverlay != null)
            {
                mouseCrosshairOverlay.UpdateZoom(currentZoom);
            }
        }
        private void UpdatePictureBoxLayout()
        {
            if (pictureBox.Image != null)
            {
                // Set appropriate PictureBox properties for zooming
                pictureBox.SizeMode = PictureBoxSizeMode.CenterImage;

                // Calculate container dimensions
                int containerWidth = ClientSize.Width;
                int containerHeight = ClientSize.Height - (ClientSize.Height - pictureBox.Height);

                // Center the PictureBox in its container
                pictureBox.Left = (containerWidth - pictureBox.Width) / 2;
                pictureBox.Top = (containerHeight - pictureBox.Height) / 2;
            }
        }
        private void UpdateImageViewForZoom()
        {
            if (currentZoom == 1.0f)
            {
                pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            }
            else
            {
                pictureBox.SizeMode = PictureBoxSizeMode.CenterImage;
                if (pictureBox.Image != null)
                {
                    int newWidth = (int)(pictureBox.Image.Width * currentZoom);
                    int newHeight = (int)(pictureBox.Image.Height * currentZoom);

                    // Center the zoomed image
                    pictureBox.Width = newWidth;
                    pictureBox.Height = newHeight;
                    pictureBox.Left = (ClientSize.Width - newWidth) / 2;
                    pictureBox.Top = (ClientSize.Height - newHeight) / 2;
                }
            }
            pictureBox.Invalidate();
        }

        // Add this to the class-level declarations in MainForm.cs
        private CheckBox chkShowMicrometers;

        // Add this to InitializeComponents() after the other crosshair controls
        private void AddCrosshairUnitControl()
        {
            // Create micrometers/pixels toggle
            chkShowMicrometers = new CheckBox
            {
                Text = "Show in Micrometers",
                Dock = DockStyle.Bottom,
                Height = 30,
                Checked = true
            };

            // Add the control to the form
            Controls.Add(chkShowMicrometers);

            // Add event handler
            chkShowMicrometers.CheckedChanged += (s, e) =>
            {
                if (crosshairOverlay != null)
                {
                    crosshairOverlay.ShowInMicrometers = chkShowMicrometers.Checked;
                }
            };
        }
    }
}