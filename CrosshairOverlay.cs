using System;
using System.Drawing;
using System.Windows.Forms;

namespace BaslerCameraForm
{
    public class CrosshairOverlay : IDisposable
    {
        private readonly PictureBox _pictureBox;
        private Bitmap _overlayBitmap;
        private Graphics _overlayGraphics;
        private bool _showCrosshair = true;
        private Color _crosshairColor = Color.Blue;
        private float _lineThickness = 1.0f;

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

            InitializeOverlay();

            // Subscribe to events
            _pictureBox.Paint += PictureBox_Paint;
            _pictureBox.Resize += PictureBox_Resize;
        }

        private void InitializeOverlay()
        {
            _overlayBitmap?.Dispose();
            _overlayGraphics?.Dispose();

            _overlayBitmap = new Bitmap(_pictureBox.Width, _pictureBox.Height);
            _overlayGraphics = Graphics.FromImage(_overlayBitmap);

            if (_showCrosshair)
            {
                DrawCrosshair();
            }
        }

        public void DrawCrosshair()
        {
            if (_overlayGraphics == null || !_showCrosshair)
                return;

            ClearOverlay();

            int centerX = _pictureBox.Width / 2;
            int centerY = _pictureBox.Height / 2;

            using (Pen pen = new Pen(_crosshairColor, _lineThickness))
            {
                // Draw vertical line
                _overlayGraphics.DrawLine(pen, centerX, 0, centerX, _pictureBox.Height);
                // Draw horizontal line
                _overlayGraphics.DrawLine(pen, 0, centerY, _pictureBox.Width, centerY);
            }

            _pictureBox.Invalidate();
        }

        private void ClearOverlay()
        {
            _overlayGraphics.Clear(Color.Transparent);
            _pictureBox.Invalidate();
        }

        private void PictureBox_Paint(object sender, PaintEventArgs e)
        {
            if (_overlayBitmap != null)
            {
                e.Graphics.DrawImage(_overlayBitmap, 0, 0);
            }
        }

        private void PictureBox_Resize(object sender, EventArgs e)
        {
            InitializeOverlay();
        }

        public void Dispose()
        {
            _pictureBox.Paint -= PictureBox_Paint;
            _pictureBox.Resize -= PictureBox_Resize;
            _overlayGraphics?.Dispose();
            _overlayBitmap?.Dispose();
        }
    }
}