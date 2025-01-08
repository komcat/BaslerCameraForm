﻿using System;
using System.Windows.Forms;
using BaslerCamera;
using Serilog;

namespace BaslerCameraForm
{
    public partial class MainForm : Form
    {
        private CameraManager cameraManager;
        private ILogger _logger;

        public MainForm()
        {
            InitializeComponent();
            InitializeLogger();
            InitializeComponents();
            SetupEventHandlers();
        }

        private void InitializeLogger()
        {
            _logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File("camera_log.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();
        }

        private void InitializeComponents()
        {


            // Create and configure PictureBox
            pictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom
            };

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


        }

        private void SetupEventHandlers()
        {
            btnConnect.Click += BtnConnect_Click;
            btnStartGrab.Click += BtnStartGrab_Click;
            btnStopGrab.Click += BtnStopGrab_Click;
            btnCameraInfo.Click += BtnCameraInfo_Click;
            FormClosing += MainForm_FormClosing;
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

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                cameraManager?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error disposing camera manager");
            }
        }

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

    }
}