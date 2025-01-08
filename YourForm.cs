using BaslerCamera;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BaslerCameraForm
{
    public partial class YourForm : Form
    {
        private ImageOverlayManager overlayManager;
        private CameraManager cameraManager;
        private ILogger _logger;
        private PictureBox pictureBox;
        private ClickImageSaver clickImageSaver;
        private MouseCrosshairOverlay mouseCrosshairOverlay;
        private CrosshairOverlay crosshairOverlay; // Add reference to CrosshairOverlay

        public YourForm()
        {
            InitializeComponent();
            _logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File("YourFormlog.txt")
                .CreateLogger();
            InitializeComponents();
        }



        private async void InitializeComponents()
        {
            try
            {
                // Create and configure PictureBox
                pictureBox = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.Black
                };
                Controls.Add(pictureBox);

                // Initialize click image saver
                clickImageSaver = new ClickImageSaver(_logger);

                // Initialize mouse crosshair overlay with automatic click handling
                mouseCrosshairOverlay = new MouseCrosshairOverlay(pictureBox, _logger);
                mouseCrosshairOverlay.MouseLocationClicked += MouseCrosshairOverlay_MouseLocationClicked;

                // Initialize crosshair overlay with desired settings
                crosshairOverlay = new CrosshairOverlay(pictureBox)
                {
                    ShowCrosshair = true,  // Show the crosshair
                    ShowLabels = false,    // Don't show labels initially
                    CrosshairColor = Color.Yellow,  // Set color (optional)
                    LineThickness = 1.0f   // Set line thickness (optional)
                };

                // Initialize overlay manager and add overlays
                overlayManager = new ImageOverlayManager(pictureBox);
                overlayManager.AddOverlay(crosshairOverlay);  // Add configured crosshair
                overlayManager.AddOverlay(mouseCrosshairOverlay);

                // Initialize camera manager and start camera operations
                cameraManager = new CameraManager(pictureBox, _logger);

                // Connect to camera
                if (cameraManager.ConnectToCamera())
                {
                    cameraManager.StartLiveView();
                    _logger.Information("Camera connected and live view started successfully");
                }
                else
                {
                    _logger.Error("Failed to connect to camera");
                    MessageBox.Show("Failed to connect to camera.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error initializing camera and overlays");
                MessageBox.Show($"Error initializing camera: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MouseCrosshairOverlay_MouseLocationClicked(object sender, MouseLocationEventArgs e)
        {
            try
            {
                _logger.Information("Mouse location clicked at: ({X}, {Y})", e.ScaledLocation.X, e.ScaledLocation.Y);

                if (cameraManager == null)
                {
                    _logger.Warning("Cannot save image - CameraManager is null");
                    return;
                }

                using (var currentImage = cameraManager.GetCurrentImage())
                {
                    if (currentImage == null)
                    {
                        _logger.Warning("Cannot save image - No current image available from camera");
                        return;
                    }

                    clickImageSaver.SaveClickedImage(currentImage, e);
                    _logger.Information("Image and metadata saved successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error handling mouse click and saving image");
                MessageBox.Show($"Error saving clicked image: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                if (cameraManager != null)
                {
                    cameraManager.StopLiveView();
                    cameraManager.DisconnectCamera();
                    cameraManager.Dispose();
                }

                // Cleanup click-related resources
                if (mouseCrosshairOverlay != null)
                {
                    mouseCrosshairOverlay.MouseLocationClicked -= MouseCrosshairOverlay_MouseLocationClicked;
                    mouseCrosshairOverlay.Dispose();
                }

                clickImageSaver?.Dispose();
                overlayManager?.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Error during form closing");
            }
            base.OnFormClosing(e);
        }

        public void UpdateZoom(float zoomFactor)
        {
            try
            {
                overlayManager?.UpdateZoom(zoomFactor);
                cameraManager?.SetZoom(zoomFactor);
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Error updating zoom");
            }
        }
    }
}
