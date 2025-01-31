using System;
using System.Drawing;
using System.Windows.Forms;
using Serilog;
using BaslerCamera;
using System.IO;

namespace BaslerCameraForm
{
    public class CameraDisplay : IDisposable
    {
        private readonly ILogger _logger;
        private readonly PictureBox _pictureBox;
        private readonly ImageOverlayManager _overlayManager;
        private readonly CrosshairOverlay _crosshairOverlay;
        private readonly MouseCrosshairOverlay _mouseCrosshairOverlay;
        private CameraManager _cameraManager;
        private ClickImageSaver _clickImageSaver;
        private float _currentZoom = 1.0f;

        public event EventHandler<MouseLocationEventArgs> ImageClicked;

        public CameraDisplay(Form parentForm, ILogger logger, PictureBox displayPictureBox)
        {
            _logger = logger.ForContext<CameraDisplay>();

            // Initialize PictureBox
            //_pictureBox = new PictureBox
            //{
            //    //Dock = DockStyle.Fill,
            //    SizeMode = PictureBoxSizeMode.Zoom,
            //    BackColor = Color.Black
            //};
            //parentForm.Controls.Add(_pictureBox);

            //use external display box.
            
            _pictureBox = displayPictureBox;
            _pictureBox.SizeMode = PictureBoxSizeMode.CenterImage;
            _pictureBox.Size = new Size(800,600);

            // Initialize overlays
            _clickImageSaver = new ClickImageSaver(_logger);
            _mouseCrosshairOverlay = new MouseCrosshairOverlay(_pictureBox, _logger);
            _crosshairOverlay = new CrosshairOverlay(_pictureBox)
            {
                ShowCrosshair = true,
                ShowLabels = false,
                CrosshairColor = Color.Yellow,
                LineThickness = 1.0f
            };

            // Initialize overlay manager
            _overlayManager = new ImageOverlayManager(_pictureBox);
            _overlayManager.AddOverlay(_crosshairOverlay);
            _overlayManager.AddOverlay(_mouseCrosshairOverlay);

            // Set up event handlers
            _mouseCrosshairOverlay.MouseLocationClicked += OnMouseLocationClicked;
        }

        public bool InitializeCamera()
        {
            try
            {
                _cameraManager = new CameraManager(_pictureBox, _logger);
                if (_cameraManager.ConnectToCamera())
                {
                    _cameraManager.StartLiveView();
                    _logger.Information("Camera connected and live view started successfully");
                    return true;
                }

                _logger.Error("Failed to connect to camera");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error initializing camera");
                return false;
            }
        }

        public void SetCrosshairProperties(bool visible, Color color, float thickness, bool showLabels)
        {
            _crosshairOverlay.ShowCrosshair = visible;
            _crosshairOverlay.CrosshairColor = color;
            _crosshairOverlay.LineThickness = thickness;
            _crosshairOverlay.ShowLabels = showLabels;
        }

        public void SetZoom(float zoomFactor)
        {
            _currentZoom = zoomFactor;
            _overlayManager.UpdateZoom(zoomFactor);
            _cameraManager?.SetZoom(zoomFactor);
        }

        private void OnMouseLocationClicked(object sender, MouseLocationEventArgs e)
        {
            try
            {
                _logger.Information("Mouse location clicked at: ({X}, {Y})", e.ScaledLocation.X, e.ScaledLocation.Y);

                using (var currentImage = _cameraManager?.GetCurrentImage())
                {
                    if (currentImage == null)
                    {
                        _logger.Warning("No image available from camera");
                        return;
                    }

                    _clickImageSaver.SaveClickedImage(currentImage, e);
                    _logger.Information("Image and metadata saved successfully");
                }

                // Raise the event for subscribers
                ImageClicked?.Invoke(this, e);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error handling mouse click and saving image");
                throw;
            }
        }

        public string GetCameraInfo()
        {
            return _cameraManager?.GetCameraInfo() ?? "Camera not initialized";
        }

        public void StartLiveView()
        {
            _cameraManager?.StartLiveView();
        }

        public void StopLiveView()
        {
            _cameraManager?.StopLiveView();
        }

        public bool SaveCurrentFrame(string filePath)
        {
            return _cameraManager?.SaveCurrentFrame(filePath) ?? false;
        }
        public bool SaveImageWithContext(string context)
        {
            try
            {
                // Create base directory if it doesn't exist
                string baseDirectory = Path.Combine(Application.StartupPath, "SavedImages");
                if (!Directory.Exists(baseDirectory))
                {
                    Directory.CreateDirectory(baseDirectory);
                }

                // Create dated folder
                string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
                string saveDirectory = Path.Combine(baseDirectory, dateFolder);
                if (!Directory.Exists(saveDirectory))
                {
                    Directory.CreateDirectory(saveDirectory);
                }

                // Generate filename with context and timestamp
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filename = $"{context}_{timestamp}.png";
                string fullPath = Path.Combine(saveDirectory, filename);

                // Save the image
                bool saved = SaveCurrentFrame(fullPath);

                if (saved)
                {
                    _logger.Information("Image saved successfully with context: {Context}, Path: {Path}", context, fullPath);
                }
                else
                {
                    _logger.Error("Failed to save image with context: {Context}", context);
                }

                return saved;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error saving image with context: {Context}", context);
                return false;
            }
        }
        public void Dispose()
        {
            try
            {
                if (_cameraManager != null)
                {
                    _cameraManager.StopLiveView();
                    _cameraManager.DisconnectCamera();
                    _cameraManager.Dispose();
                }

                _mouseCrosshairOverlay.MouseLocationClicked -= OnMouseLocationClicked;
                _mouseCrosshairOverlay.Dispose();
                _clickImageSaver?.Dispose();
                _overlayManager?.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Error disposing CameraDisplay", ex);
            }
        }
    }
}