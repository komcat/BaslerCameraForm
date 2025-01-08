using BaslerCamera;
using System.Drawing;
using System.Windows.Forms;
using System;


public class CrosshairOverlay : IImageOverlay
{
    private readonly PictureBox _pictureBox;
    private Bitmap _overlayBitmap;
    private Graphics _overlayGraphics;
    private bool _showCrosshair = false;
    private bool _showLabels = false;  // New field for controlling label visibility
    private Color _crosshairColor = Color.Blue;
    private float _lineThickness = 1.0f;
    private bool _isInitialized = false;
    private bool _showInMicrometers = true; // New property to toggle between μm and pixels

    // Pixel size in micrometers
    private const double PixelSizeX = 5.3;
    private const double PixelSizeY = 5.3;

    // Define tick intervals in micrometers
    private readonly int[] _tickIntervals = new[] { 100, 200, 300 }; // 100µm, 200µm, 300µm
    private const int TickLength = 10; // Length of tick marks in pixels
    private readonly Font _labelFont = new Font("Arial", 6);
    private readonly Font _labelFontVertical = new Font("Arial", 6);
    private float _zoomFactor = 1.0f;
    public bool Visible { get; set; }
    public bool ShowInMicrometers
    {
        get => _showInMicrometers;
        set
        {
            _showInMicrometers = value;
            if (_showCrosshair)
            {
                DrawCrosshair();
            }
        }
    }

    public bool ShowCrosshair
    {
        get => _showCrosshair;
        set
        {
            _showCrosshair = value;
            if (!value)
            {
                ClearOverlay();
            }
            else
            {
                EnsureInitialized();
                DrawCrosshair();
            }
        }
    }
    // Add new property for label visibility
    public bool ShowLabels
    {
        get => _showLabels;
        set
        {
            _showLabels = value;
            if (_showCrosshair)
            {
                DrawCrosshair();
            }
        }
    }
    public Color CrosshairColor
    {
        get => _crosshairColor;
        set
        {
            _crosshairColor = value;
            if (_showCrosshair)
            {
                DrawCrosshair();
            }
        }
    }

    public float LineThickness
    {
        get => _lineThickness;
        set
        {
            _lineThickness = value;
            if (_showCrosshair)
            {
                DrawCrosshair();
            }
        }
    }

    public CrosshairOverlay(PictureBox pictureBox)
    {
        _pictureBox = pictureBox ?? throw new ArgumentNullException(nameof(pictureBox));
        PictureBoxExtensions.PaintLayers += PictureBox_PaintLayers;
        _pictureBox.Resize += PictureBox_Resize;
    }

    private void PictureBox_PaintLayers(object sender, PaintEventArgs e)
    {
        if (!_showCrosshair) return;

        int centerX = _pictureBox.Width / 2;
        int centerY = _pictureBox.Height / 2;

        using (Pen pen = new Pen(_crosshairColor, _lineThickness))
        {
            // Draw main crosshair lines
            e.Graphics.DrawLine(pen, centerX, 0, centerX, _pictureBox.Height);
            e.Graphics.DrawLine(pen, 0, centerY, _pictureBox.Width, centerY);

            // Draw tick marks and labels
            DrawTicksAndLabels(e.Graphics, centerX, centerY, pen);
        }
    }

    private void DrawTicksAndLabels(Graphics g, int centerX, int centerY, Pen pen)
    {
        foreach (int interval in _tickIntervals)
        {
            // Calculate pixel distance for the current interval, accounting for zoom
            int pixelsX = (int)((interval / PixelSizeX) * _zoomFactor);
            int pixelsY = (int)((interval / PixelSizeY) * _zoomFactor);

            string labelPositive, labelNegative;
            if (_showInMicrometers)
            {
                labelPositive = $"+{interval}µm";
                labelNegative = $"-{interval}µm";
            }
            else
            {
                labelPositive = $"+{pixelsX}px";
                labelNegative = $"-{pixelsX}px";
            }

            // Draw X-axis ticks and labels
            DrawTick(g, centerX + pixelsX, centerY, true, pen, labelPositive);
            DrawTick(g, centerX - pixelsX, centerY, true, pen, labelNegative);

            // For Y-axis, use pixelsY if showing in pixels
            if (!_showInMicrometers)
            {
                labelPositive = $"+{pixelsY}px";
                labelNegative = $"-{pixelsY}px";
            }

            // Draw Y-axis ticks and labels
            DrawTick(g, centerX, centerY + pixelsY, false, pen, labelPositive);
            DrawTick(g, centerX, centerY - pixelsY, false, pen, labelNegative);
        }
    }

    private void DrawTick(Graphics g, int x, int y, bool isXAxis, Pen pen, string label)
    {
        if (isXAxis)
        {
            // Draw vertical tick for X-axis
            g.DrawLine(pen, x, y - TickLength / 2, x, y + TickLength / 2);

            // Draw label only if ShowLabels is true
            if (_showLabels)
            {
                using (var brush = new SolidBrush(_crosshairColor))
                {
                    var size = g.MeasureString(label, _labelFontVertical);
                    var state = g.Save();
                    g.TranslateTransform(x, y + TickLength);
                    g.RotateTransform(90);
                    g.DrawString(label, _labelFontVertical, brush, 0, -size.Width / 2);
                    g.Restore(state);
                }
            }
        }
        else
        {
            // Draw horizontal tick for Y-axis
            g.DrawLine(pen, x - TickLength / 2, y, x + TickLength / 2, y);

            // Draw label only if ShowLabels is true
            if (_showLabels)
            {
                using (var brush = new SolidBrush(_crosshairColor))
                {
                    var size = g.MeasureString(label, _labelFont);
                    g.DrawString(label, _labelFont, brush, x + TickLength, y - size.Height / 2);
                }
            }
        }
    }
    // Rest of the existing methods remain the same...
    private void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            InitializeOverlay();
        }
    }

    private void InitializeOverlay()
    {
        if (_overlayBitmap != null)
        {
            _overlayGraphics?.Dispose();
            _overlayBitmap.Dispose();
        }

        _overlayBitmap = new Bitmap(_pictureBox.Width, _pictureBox.Height);
        _overlayGraphics = Graphics.FromImage(_overlayBitmap);
        _isInitialized = true;

        if (_showCrosshair)
        {
            DrawCrosshair();
        }
    }

    public void DrawCrosshair()
    {
        if (!_isInitialized || !_showCrosshair)
            return;

        ClearOverlay();
        _pictureBox.Invalidate();
    }

    private void ClearOverlay()
    {
        if (_isInitialized && _overlayGraphics != null)
        {
            _overlayGraphics.Clear(Color.Transparent);
            _pictureBox.Invalidate();
        }
    }

    private void PictureBox_Resize(object sender, EventArgs e)
    {
        InitializeOverlay();
    }

    public void UpdateZoom(float zoom)
    {
        _zoomFactor = zoom;
        if (_showCrosshair)
        {
            DrawCrosshair();
        }
    }

    public void Dispose()
    {
        _pictureBox.Resize -= PictureBox_Resize;
        PictureBoxExtensions.PaintLayers -= PictureBox_PaintLayers;
        _overlayGraphics?.Dispose();
        _overlayBitmap?.Dispose();
        _labelFont?.Dispose();
        _labelFontVertical?.Dispose();
        _isInitialized = false;
    }
}