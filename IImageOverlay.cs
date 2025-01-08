using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// 1. Create an interface for the overlays
public interface IImageOverlay : IDisposable
{
    void UpdateZoom(float zoomFactor);
    bool Visible { get; set; }
}
