using SkiaSharp;
using System;

namespace Microsoft.ReportingServices
{
    class InvalidImage
    {
        // Antes: System.Drawing.Bitmap (GDI+). Agora: SKBitmap (SkiaSharp, cross-platform).
        // SKBitmap.Decode entende o BMP embutido em ImageData normalmente.
        public static SKBitmap Image { get { return SKBitmap.Decode(ImageData); } }
        public static byte[] ImageData { get; }

        static InvalidImage()
        {
            ImageData = Convert.FromBase64String("Qk3+AAAAAAAAAHYAAAAoAAAADwAAABEAAAABAAQAAAAAAAAAAADEDgAAxA4AABAAAAAQAAAAAAAA/wAAgP8AgAD/AICA/4AAAP+AAID/gIAA/8DAwP+AgID/AAD//wD/AP8A/////wAA//8A/////wD//////3AAAAAAAAAAeHd3d3d3dwB4///////3AHj///////cAeP//////9wB4///////3AHj/+Z/5n/cAeP//mZn/9wB4///5n//3AHj//5mZ//cAeP/5n/mf9wB4///////3AHj///////cAeP//////9wB4///////3AHiIiIiIiIiAd3d3d3d3d3A=");
        }
    }
}
