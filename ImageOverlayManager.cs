using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

// 3. Create a manager class for the overlays
public class ImageOverlayManager
{
    private readonly List<IImageOverlay> _overlays = new List<IImageOverlay>();
    private readonly PictureBox _pictureBox;

    public ImageOverlayManager(PictureBox pictureBox)
    {
        _pictureBox = pictureBox;
    }

    public void AddOverlay(IImageOverlay overlay)
    {
        _overlays.Add(overlay);
    }

    public void UpdateZoom(float zoomFactor)
    {
        foreach (var overlay in _overlays)
        {
            overlay.UpdateZoom(zoomFactor);
        }
    }

    public void Dispose()
    {
        foreach (var overlay in _overlays)
        {
            overlay.Dispose();
        }
        _overlays.Clear();
    }
}