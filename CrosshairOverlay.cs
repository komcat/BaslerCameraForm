using System.Drawing;
using System.Windows.Forms;
using System;
using System.Collections.Generic;
using BaslerCamera;

public class CrosshairOverlay : IDisposable
{
    private readonly PictureBox _pictureBox;
    private Bitmap _overlayBitmap;
    private Graphics _overlayGraphics;
    private bool _showCrosshair = true;
    private Color _crosshairColor = Color.Blue;
    private float _lineThickness = 1.0f;
    private bool _isInitialized = false;

    // Pixel size in micrometers
    private const double PixelSizeX = 5.3;
    private const double PixelSizeY = 5.3;

    // Define tick intervals in micrometers
    private readonly int[] _tickIntervals = new[] { 100, 200, 300 }; // 100µm, 200µm, 300µm
    private const int TickLength = 10; // Length of tick marks in pixels
    private readonly Font _labelFont = new Font("Arial", 6);
    private readonly Font _labelFontVertical = new Font("Arial", 6);

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
            // Calculate pixel distance for the current interval
            int pixelsX = (int)(interval / PixelSizeX);
            int pixelsY = (int)(interval / PixelSizeY);

            // Draw X-axis ticks and labels (positive and negative)
            DrawTick(g, centerX + pixelsX, centerY, true, pen, $"+{interval}µm");
            DrawTick(g, centerX - pixelsX, centerY, true, pen, $"-{interval}µm");

            // Draw Y-axis ticks and labels (positive and negative)
            DrawTick(g, centerX, centerY + pixelsY, false, pen, $"+{interval}µm");
            DrawTick(g, centerX, centerY - pixelsY, false, pen, $"-{interval}µm");
        }
    }

    private void DrawTick(Graphics g, int x, int y, bool isXAxis, Pen pen, string label)
    {
        if (isXAxis)
        {
            // Draw vertical tick for X-axis
            g.DrawLine(pen, x, y - TickLength / 2, x, y + TickLength / 2);

            // Draw vertical label for X-axis
            using (var brush = new SolidBrush(_crosshairColor))
            {
                var size = g.MeasureString(label, _labelFontVertical);
                // Save the current graphics state
                var state = g.Save();

                // Translate to the text location and rotate
                g.TranslateTransform(x, y + TickLength);
                g.RotateTransform(90);

                // Draw the rotated text
                g.DrawString(label, _labelFontVertical, brush, 0, -size.Width / 2);

                // Restore the graphics state
                g.Restore(state);
            }
        }
        else
        {
            // Draw horizontal tick for Y-axis
            g.DrawLine(pen, x - TickLength / 2, y, x + TickLength / 2, y);

            // Draw label
            using (var brush = new SolidBrush(_crosshairColor))
            {
                var size = g.MeasureString(label, _labelFont);
                g.DrawString(label, _labelFont, brush, x + TickLength, y - size.Height / 2);
            }
        }
    }

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