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
            isProcessing = false;
            camera?.StreamGrabber.Stop();
            imageQueue.CompleteAdding();
            processingTask?.Wait();
            _logger.Information("Live view stopped");
        }

        private void ImageView_Paint(object sender, PaintEventArgs e)
        {
            lock (imageLock)
            {
                if (currentImage != null)
                {
                    e.Graphics.DrawImage(currentImage, 0, 0, imageView.Width, imageView.Height);
                }
            }
        }

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
                            Bitmap bitmap = new Bitmap(grabResult.Width, grabResult.Height, PixelFormat.Format32bppRgb);
                            BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, bitmap.PixelFormat);
                            converter.Convert(bmpData.Scan0, bmpData.Stride * bitmap.Height, grabResult);
                            bitmap.UnlockBits(bmpData);

                            await Task.Run(() =>
                            {
                                imageView.Invoke((MethodInvoker)delegate
                                {
                                    lock (imageLock)
                                    {
                                        currentImage?.Dispose();
                                        currentImage = bitmap;
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
            StopLiveView();
            if (camera != null && camera.StreamGrabber != null)
            {
                camera.StreamGrabber.ImageGrabbed -= OnImageGrabbed;
            }
            camera?.Dispose();
            camera = null;
            currentImage?.Dispose();
            imageView.Paint -= ImageView_Paint;
            imageView.MouseClick -= ImageView_MouseClick;
        }
    }
}