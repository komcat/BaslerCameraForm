using Basler.Pylon;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;
using System.Collections.Concurrent;
using Serilog;
using System.Text;

namespace BaslerCamera
{
    public class CameraManager : IDisposable
    {
        private Camera camera = null;
        private PixelDataConverter converter = new PixelDataConverter();
        private PictureBox imageView;
        private Task grabTask;
        private CancellationTokenSource cancellationTokenSource;
        private readonly object imageLock = new object();
        private Bitmap currentImage;
        private readonly ILogger _logger;

        public event EventHandler<Point> ImageClicked;

        private readonly BlockingCollection<IGrabResult> imageQueue = new BlockingCollection<IGrabResult>();
        private Task processingTask;
        private volatile bool isProcessing = false;

        private const int MaxFrameRate = 30;
        private DateTime lastFrameTime = DateTime.MinValue;
        private float currentZoom = 1.0f;
        private Size originalImageSize;

        private const int MaxWidth = 1281;
        private const int MaxHeight = 1025;
        public CameraManager(PictureBox imageView, ILogger logger)
        {
            this.imageView = imageView;
            this._logger = logger.ForContext<CameraManager>();
            converter.OutputPixelFormat = PixelType.BGRA8packed;
            imageView.MouseClick += ImageView_MouseClick;
            imageView.Paint += ImageView_Paint;
            processingTask = Task.Run(ProcessImagesAsync);
        }

        public bool ConnectToCamera()
        {
            try
            {
                camera = new Camera();
                camera.CameraOpened += Configuration.AcquireContinuous;
                camera.Open();

                camera.StreamGrabber.ImageGrabbed += OnImageGrabbed;

                _logger.Information("Camera connected successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error initializing camera");
                return false;
            }
        }
        public void DisconnectCamera()
        {
            try
            {
                if (camera != null)
                {
                    // Stop live view if it's running
                    if (camera.StreamGrabber.IsGrabbing)
                    {
                        StopLiveView();
                    }

                    // Remove event handlers
                    if (camera.StreamGrabber != null)
                    {
                        camera.StreamGrabber.ImageGrabbed -= OnImageGrabbed;
                    }

                    // Close camera connection
                    if (camera.IsOpen)
                    {
                        camera.Close();
                        _logger.Information("Camera disconnected successfully");
                    }

                    // Dispose camera object
                    camera.Dispose();
                    camera = null;

                    // Clean up image resources
                    lock (imageLock)
                    {
                        if (currentImage != null)
                        {
                            currentImage.Dispose();
                            currentImage = null;
                        }
                    }

                    // Reset any camera-related properties
                    originalImageSize = Size.Empty;
                    currentZoom = 1.0f;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error disconnecting camera");
                throw;
            }
        }
        public void StartLiveView()
        {
            if (camera == null || !camera.IsOpen)
            {
                _logger.Warning("Camera is not initialized or opened. Cannot start live view.");
                return;
            }

            try
            {
                camera.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.Continuous);

                if (!camera.StreamGrabber.IsGrabbing)
                {
                    camera.StreamGrabber.Start(GrabStrategy.LatestImages, GrabLoop.ProvidedByStreamGrabber);
                    _logger.Information("StreamGrabber started");
                }

                isProcessing = true;
                _logger.Information("Live view started");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error starting live view");
                MessageBox.Show($"Error starting live view: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void StopLiveView()
        {
            try
            {
                isProcessing = false;

                // Stop the camera grabber first
                if (camera?.StreamGrabber != null && camera.StreamGrabber.IsGrabbing)
                {
                    camera.StreamGrabber.Stop();
                }

                // Clear any remaining items in the queue before marking it complete
                while (imageQueue.TryTake(out _)) { }

                // Mark the queue as complete
                imageQueue.CompleteAdding();

                // Wait for processing task to complete with timeout
                if (processingTask != null)
                {
                    if (!processingTask.Wait(TimeSpan.FromSeconds(3)))
                    {
                        _logger.Warning("ProcessImagesAsync task did not complete within timeout");
                    }
                }

                _logger.Information("Live view stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error stopping live view");
                throw;
            }
        }

        // In CameraManager.cs, modify the ImageView_Paint method:
        private void ImageView_Paint(object sender, PaintEventArgs e)
        {
            lock (imageLock)
            {
                if (currentImage != null)
                {
                    // Draw the camera image
                    e.Graphics.DrawImage(currentImage, 0, 0, imageView.Width, imageView.Height);

                    // Signal that the base image has been drawn
                    imageView.InvokePaintLayers(e);
                }
            }
        }

        // Modify OnImageGrabbed to handle zooming
        private void OnImageGrabbed(object sender, ImageGrabbedEventArgs e)
        {
            try
            {
                if ((DateTime.Now - lastFrameTime).TotalMilliseconds < 1000.0 / MaxFrameRate)
                {
                    return;
                }

                using (IGrabResult grabResult = e.GrabResult)
                {
                    if (grabResult.GrabSucceeded)
                    {
                        imageQueue.Add(grabResult.Clone());
                        lastFrameTime = DateTime.Now;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in OnImageGrabbed");
            }
        }

        // Modify ProcessImagesAsync to apply zoom
        // Modify ProcessImagesAsync method in CameraManager.cs

        // In CameraManager.cs, modify ProcessImagesAsync method


        // In CameraManager.cs, modify ProcessImagesAsync method
        private async Task ProcessImagesAsync()
        {
            while (!imageQueue.IsCompleted)
            {
                try
                {
                    IGrabResult grabResult = await Task.Run(() => imageQueue.Take());
                    using (grabResult)
                    {
                        if (grabResult.GrabSucceeded)
                        {
                            // Create base image
                            Bitmap bitmap = new Bitmap(grabResult.Width, grabResult.Height, PixelFormat.Format32bppRgb);
                            BitmapData bmpData = bitmap.LockBits(
                                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                                ImageLockMode.WriteOnly,
                                bitmap.PixelFormat);

                            converter.Convert(bmpData.Scan0, bmpData.Stride * bitmap.Height, grabResult);
                            bitmap.UnlockBits(bmpData);

                            await Task.Run(() =>
                            {
                                imageView.Invoke((MethodInvoker)delegate
                                {
                                    lock (imageLock)
                                    {
                                        if (originalImageSize.IsEmpty)
                                        {
                                            originalImageSize = new Size(bitmap.Width, bitmap.Height);
                                        }

                                        // Calculate dimensions while preserving aspect ratio
                                        int zoomedWidth = (int)(originalImageSize.Width * currentZoom);
                                        int zoomedHeight = (int)(originalImageSize.Height * currentZoom);

                                        // Create and crop zoomed image
                                        Bitmap processedImage = CropZoomedImage(bitmap, zoomedWidth, zoomedHeight);

                                        // Update current image without changing PictureBox size
                                        currentImage?.Dispose();
                                        currentImage = processedImage;

                                        // Center the image in the PictureBox if needed
                                        if (imageView.Parent != null && imageView.Parent is Panel panel)
                                        {
                                            imageView.Location = new Point(
                                                Math.Max(0, (panel.ClientSize.Width - imageView.Width) / 2),
                                                Math.Max(0, (panel.ClientSize.Height - imageView.Height) / 2)
                                            );
                                        }
                                        bitmap.Dispose();
                                    }
                                    imageView.Invalidate();
                                });
                            });
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error processing image");
                }
            }
        }
        private void ImageView_MouseClick(object sender, MouseEventArgs e)
        {
            if (currentImage == null)
            {
                MessageBox.Show("No image available.", "Click Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            float scaleX = (float)currentImage.Width / imageView.Width;
            float scaleY = (float)currentImage.Height / imageView.Height;

            int centerX = imageView.Width / 2;
            int centerY = imageView.Height / 2;

            int relativeX = (int)((e.X - centerX) * scaleX);
            int relativeY = (int)((e.Y - centerY) * scaleY);

            ImageClicked?.Invoke(this, new Point(relativeX, relativeY));
            _logger.Information("Image clicked at relative position: ({RelativeX}, {RelativeY})", relativeX, relativeY);
        }

        public string GetCameraInfo()
        {
            if (camera == null || !camera.IsOpen)
            {
                return "Camera is not connected.";
            }

            StringBuilder info = new StringBuilder();
            info.AppendLine("Camera Information:");
            info.AppendLine($"Model: {camera.CameraInfo[CameraInfoKey.ModelName]}");
            info.AppendLine($"Serial Number: {camera.CameraInfo[CameraInfoKey.SerialNumber]}");
            info.AppendLine($"Vendor: {camera.CameraInfo[CameraInfoKey.VendorName]}");
            info.AppendLine($"User ID: {camera.CameraInfo[CameraInfoKey.UserDefinedName]}");

            info.AppendLine("\nImage Properties:");
            info.AppendLine($"Width: {camera.Parameters[PLCamera.Width].GetValue()} pixels");
            info.AppendLine($"Height: {camera.Parameters[PLCamera.Height].GetValue()} pixels");
            info.AppendLine($"Pixel Format: {camera.Parameters[PLCamera.PixelFormat].GetValue()}");

            info.AppendLine("\nSensor Information:");
            info.AppendLine($"Sensor Size: {camera.Parameters[PLCamera.SensorWidth].GetValue()} x {camera.Parameters[PLCamera.SensorHeight].GetValue()} pixels");

            if (camera.Parameters.Contains(PLCamera.AcquisitionFrameRate))
            {
                info.AppendLine($"\nFrame Rate: {camera.Parameters[PLCamera.AcquisitionFrameRate].GetValue()} fps");
            }

            if (camera.Parameters.Contains(PLCamera.ExposureTime))
            {
                info.AppendLine($"Exposure Time: {camera.Parameters[PLCamera.ExposureTime].GetValue()} µs");
            }

            return info.ToString();
        }

        public bool SaveCurrentFrame(string filePath)
        {
            try
            {
                lock (imageLock)
                {
                    if (currentImage == null)
                    {
                        _logger.Warning("No image available to save");
                        return false;
                    }

                    using (Bitmap savedImage = new Bitmap(currentImage))
                    {
                        ImageFormat format = ImageFormat.Png;
                        string extension = System.IO.Path.GetExtension(filePath).ToLower();

                        switch (extension)
                        {
                            case ".jpg":
                            case ".jpeg":
                                format = ImageFormat.Jpeg;
                                break;
                            case ".bmp":
                                format = ImageFormat.Bmp;
                                break;
                            case ".png":
                                format = ImageFormat.Png;
                                break;
                            default:
                                _logger.Warning("Unsupported file format. Defaulting to PNG");
                                break;
                        }

                        savedImage.Save(filePath, format);
                        _logger.Information("Frame saved successfully to: {FilePath}", filePath);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error saving frame");
                return false;
            }
        }

        public void Dispose()
        {
            try
            {
                DisconnectCamera();
                imageView.Paint -= ImageView_Paint;
                imageView.MouseClick -= ImageView_MouseClick;
                converter?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in Dispose");
                throw;
            }
        }


        public void SetZoom(float zoomFactor)
        {
            currentZoom = zoomFactor;
            lock (imageLock)
            {
                if (currentImage != null && !originalImageSize.IsEmpty)
                {
                    int zoomedWidth = (int)(originalImageSize.Width * currentZoom);
                    int zoomedHeight = (int)(originalImageSize.Height * currentZoom);

                    // Create and crop zoomed image
                    Bitmap processedImage = CropZoomedImage(currentImage, zoomedWidth, zoomedHeight);

                    // Update current image without changing PictureBox size
                    var oldImage = currentImage;
                    currentImage = processedImage;
                    oldImage?.Dispose();

                    // Center the image if needed
                    if (imageView.Parent != null && imageView.Parent is Panel panel)
                    {
                        imageView.Location = new Point(
                            Math.Max(0, (panel.ClientSize.Width - imageView.Width) / 2),
                            Math.Max(0, (panel.ClientSize.Height - imageView.Height) / 2)
                        );
                    }
                    imageView.Invalidate();
                }
            }
        }
        private Bitmap CropZoomedImage(Bitmap sourceImage, int zoomedWidth, int zoomedHeight)
        {
            // Calculate dimensions that maintain aspect ratio and fit within max bounds
            double aspectRatio = (double)sourceImage.Width / sourceImage.Height;
            int finalWidth = zoomedWidth;
            int finalHeight = zoomedHeight;

            if (finalWidth > MaxWidth)
            {
                finalWidth = MaxWidth;
                finalHeight = (int)(MaxWidth / aspectRatio);
            }

            if (finalHeight > MaxHeight)
            {
                finalHeight = MaxHeight;
                finalWidth = (int)(MaxHeight * aspectRatio);
            }

            // Create zoomed bitmap
            var zoomedBitmap = new Bitmap(zoomedWidth, zoomedHeight);
            using (var graphics = Graphics.FromImage(zoomedBitmap))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(sourceImage, 0, 0, zoomedWidth, zoomedHeight);
            }

            // If the zoomed image is within bounds, return it directly
            if (zoomedWidth <= MaxWidth && zoomedHeight <= MaxHeight)
            {
                return zoomedBitmap;
            }

            // Calculate crop area to maintain center
            int cropX = (zoomedWidth - finalWidth) / 2;
            int cropY = (zoomedHeight - finalHeight) / 2;

            // Create cropped bitmap
            var croppedBitmap = new Bitmap(finalWidth, finalHeight);
            using (var graphics = Graphics.FromImage(croppedBitmap))
            {
                graphics.DrawImage(zoomedBitmap,
                    new Rectangle(0, 0, finalWidth, finalHeight),
                    new Rectangle(cropX, cropY, finalWidth, finalHeight),
                    GraphicsUnit.Pixel);
            }

            zoomedBitmap.Dispose();
            return croppedBitmap;
        }

        private void UpdateZoomedImage()
        {
            if (currentImage == null) return;

            lock (imageLock)
            {
                // Store original size if not set
                if (originalImageSize.IsEmpty)
                {
                    originalImageSize = new Size(currentImage.Width, currentImage.Height);
                }

                // Calculate new dimensions
                int newWidth = (int)(originalImageSize.Width * currentZoom);
                int newHeight = (int)(originalImageSize.Height * currentZoom);

                // Create new bitmap with zoomed dimensions
                var zoomedImage = new Bitmap(newWidth, newHeight);
                using (var graphics = Graphics.FromImage(zoomedImage))
                {
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(currentImage, 0, 0, newWidth, newHeight);
                }

                // Dispose old zoomed image and set new one
                var oldImage = currentImage;
                currentImage = zoomedImage;
                oldImage?.Dispose();

                imageView.Invoke((MethodInvoker)delegate
                {
                    imageView.Invalidate();
                });
            }
        }

    }

    // Add this extension method in a new file or at the bottom of CameraManager.cs:
    public static class PictureBoxExtensions
    {
        public static event EventHandler<PaintEventArgs> PaintLayers;

        public static void InvokePaintLayers(this PictureBox pictureBox, PaintEventArgs e)
        {
            PaintLayers?.Invoke(pictureBox, e);
        }
    }
}