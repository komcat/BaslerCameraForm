using System;
using System.Drawing;
using System.Windows.Forms;
using BaslerCamera;
using Serilog;

public class MouseCrosshairOverlay : IImageOverlay
{
    private readonly PictureBox _pictureBox;
    private readonly ILogger _logger;
    private Point _currentMousePosition;
    private bool _isMouseOver = false;
    private readonly Color _crosshairColor = Color.Cyan;
    private readonly float _lineThickness = 1.0f;
    private float _currentZoom = 1.0f;

    // Physical size per pixel in micrometers (at 1.0x zoom)
    private const double PixelSizeX = 5.3;
    private const double PixelSizeY = 5.3;

    public event EventHandler<MouseLocationEventArgs> MouseLocationClicked;


    bool IImageOverlay.Visible { get; set; }

    public MouseCrosshairOverlay(PictureBox pictureBox, ILogger logger)
    {
        _pictureBox = pictureBox ?? throw new ArgumentNullException(nameof(pictureBox));
        _logger = logger?.ForContext<MouseCrosshairOverlay>() ?? throw new ArgumentNullException(nameof(logger));

        _pictureBox.MouseMove += PictureBox_MouseMove;
        _pictureBox.MouseEnter += PictureBox_MouseEnter;
        _pictureBox.MouseLeave += PictureBox_MouseLeave;
        _pictureBox.Click += PictureBox_Click;
        PictureBoxExtensions.PaintLayers += PictureBox_PaintLayers;
    }

    public void UpdateZoom(float zoomFactor)
    {
        _currentZoom = zoomFactor;
        _pictureBox.Invalidate();
    }

    private (Point scaled, double physicalX, double physicalY) CalculateCoordinates(Point screenPoint)
    {
        // Get center of the picture box
        int centerX = _pictureBox.Width / 2;
        int centerY = _pictureBox.Height / 2;

        // Calculate offset from center in screen coordinates
        int screenOffsetX = screenPoint.X - centerX;
        int screenOffsetY = screenPoint.Y - centerY;

        // Calculate scaling based on zoom and image size
        float scaleX = 1.0f;
        float scaleY = 1.0f;

        if (_pictureBox.Image != null)
        {
            scaleX = (float)_pictureBox.Image.Width / (_pictureBox.Width * _currentZoom);
            scaleY = (float)_pictureBox.Image.Height / (_pictureBox.Height * _currentZoom);
        }

        // Calculate scaled pixel coordinates (relative to center)
        int scaledX = (int)(screenOffsetX * scaleX);
        int scaledY = (int)(screenOffsetY * scaleY);

        // Calculate physical distances in micrometers
        // At higher zoom, each pixel represents a smaller physical distance
        double physicalX = (scaledX * PixelSizeX) / _currentZoom;
        double physicalY = (scaledY * PixelSizeY) / _currentZoom;

        return (new Point(scaledX, scaledY), physicalX, physicalY);
    }

    private void PictureBox_Click(object sender, EventArgs e)
    {
        if (e is MouseEventArgs mouseEvent)
        {
            var clickLocation = new Point(mouseEvent.X, mouseEvent.Y);
            var (scaledLocation, physicalX, physicalY) = CalculateCoordinates(clickLocation);

            _logger.Information(
                "Mouse clicked - Screen: ({ScreenX}, {ScreenY}), Scaled: ({ScaledX}, {ScaledY}), Physical: ({DistanceX:F1}µm, {DistanceY:F1}µm), Zoom: {Zoom:F1}x",
                clickLocation.X, clickLocation.Y,
                scaledLocation.X, scaledLocation.Y,
                physicalX, physicalY,
                _currentZoom
            );

            MouseLocationClicked?.Invoke(this,
                new MouseLocationEventArgs(clickLocation, scaledLocation, physicalX, physicalY, _currentZoom));
        }
    }

    private void PictureBox_PaintLayers(object sender, PaintEventArgs e)
    {
        if (!_isMouseOver) return;

        using (Pen pen = new Pen(_crosshairColor, _lineThickness))
        {
            // Draw crosshair lines
            e.Graphics.DrawLine(pen, 0, _currentMousePosition.Y, _pictureBox.Width, _currentMousePosition.Y);
            e.Graphics.DrawLine(pen, _currentMousePosition.X, 0, _currentMousePosition.X, _pictureBox.Height);

            // Draw intersection circle
            const int circleRadius = 2;
            e.Graphics.DrawEllipse(pen,
                _currentMousePosition.X - circleRadius,
                _currentMousePosition.Y - circleRadius,
                circleRadius * 2,
                circleRadius * 2);

            // Calculate and display coordinates
            var (scaledLocation, physicalX, physicalY) = CalculateCoordinates(_currentMousePosition);

            using (Font font = new Font("Arial", 8))
            using (SolidBrush brush = new SolidBrush(_crosshairColor))
            {
                string coordinates = $"Pixels: ({scaledLocation.X}, {scaledLocation.Y})\n" +
                                   $"Physical: ({physicalX:F1}µm, {physicalY:F1}µm)\n" +
                                   $"Zoom: {_currentZoom:F1}x";

                // Position the text to avoid screen edges
                float textX = _currentMousePosition.X + 10;
                float textY = _currentMousePosition.Y + 10;

                // Measure text size to ensure it stays within bounds
                SizeF textSize = e.Graphics.MeasureString(coordinates, font);
                if (textX + textSize.Width > _pictureBox.Width)
                {
                    textX = _currentMousePosition.X - textSize.Width - 10;
                }
                if (textY + textSize.Height > _pictureBox.Height)
                {
                    textY = _currentMousePosition.Y - textSize.Height - 10;
                }

                e.Graphics.DrawString(coordinates,
                    font,
                    brush,
                    textX,
                    textY);
            }
        }
    }

    private void PictureBox_MouseMove(object sender, MouseEventArgs e)
    {
        _currentMousePosition = e.Location;
        _pictureBox.Invalidate();
    }

    private void PictureBox_MouseEnter(object sender, EventArgs e)
    {
        _isMouseOver = true;
        _pictureBox.Invalidate();
    }

    private void PictureBox_MouseLeave(object sender, EventArgs e)
    {
        _isMouseOver = false;
        _pictureBox.Invalidate();
    }

    public void Dispose()
    {
        _pictureBox.MouseMove -= PictureBox_MouseMove;
        _pictureBox.MouseEnter -= PictureBox_MouseEnter;
        _pictureBox.MouseLeave -= PictureBox_MouseLeave;
        _pictureBox.Click -= PictureBox_Click;
        PictureBoxExtensions.PaintLayers -= PictureBox_PaintLayers;
    }
}

public class MouseLocationEventArgs : EventArgs
{
    public Point ScreenLocation { get; }
    public Point ScaledLocation { get; }
    public double PhysicalDistanceX { get; }
    public double PhysicalDistanceY { get; }
    public float ZoomFactor { get; }

    public MouseLocationEventArgs(Point screenLocation, Point scaledLocation,
        double physicalDistanceX, double physicalDistanceY, float zoomFactor)
    {
        ScreenLocation = screenLocation;
        ScaledLocation = scaledLocation;
        PhysicalDistanceX = physicalDistanceX;
        PhysicalDistanceY = physicalDistanceY;
        ZoomFactor = zoomFactor;
    }
}