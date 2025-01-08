using System.Collections.Generic;
using System.Windows.Forms;
using System;

public class ImageOverlayManager : IDisposable
{
    private readonly List<IImageOverlay> _overlays = new List<IImageOverlay>();
    private readonly PictureBox _pictureBox;
    private bool _disposed;

    public ImageOverlayManager(PictureBox pictureBox)
    {
        _pictureBox = pictureBox ?? throw new ArgumentNullException(nameof(pictureBox));
    }

    public void AddOverlay(IImageOverlay overlay)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ImageOverlayManager));
        _overlays.Add(overlay);
    }

    public void UpdateZoom(float zoomFactor)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ImageOverlayManager));
        foreach (var overlay in _overlays)
        {
            overlay.UpdateZoom(zoomFactor);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var overlay in _overlays)
            {
                overlay?.Dispose();
            }
            _overlays.Clear();
            _disposed = true;
        }
    }
}