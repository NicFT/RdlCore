using SkiaSharp;
using System;
using System.IO;

namespace Microsoft.ReportingServices.Rendering.SPBProcessing
{
    internal class ImageInfo : IDisposable
    {
        public Stream ImageData;

        public int Width;

        public int Height;

        // g agora é SKCanvas (era System.Drawing.Graphics).
        public void RenderAndDispose(SKCanvas g, int x, int y)
        {
            byte[] bytes = ReadAll(ImageData);
            using (SKBitmap bitmap = SKBitmap.Decode(bytes))
            {
                if (bitmap != null)
                {
                    // Desenha na posição (x, y) sem escala, equivalente ao GDI+ DrawImage(image, Point).
                    g.DrawBitmap(bitmap, x, y);
                }
            }
            Dispose();
        }

        public void Dispose()
        {
            if (ImageData != null)
            {
                ImageData.Dispose();
                ImageData = null;
            }
            GC.SuppressFinalize(this);
        }

        private static byte[] ReadAll(Stream stream)
        {
            if (stream is MemoryStream ms)
            {
                return ms.ToArray();
            }
            using (MemoryStream copy = new MemoryStream())
            {
                if (stream.CanSeek)
                {
                    stream.Position = 0L;
                }
                stream.CopyTo(copy);
                return copy.ToArray();
            }
        }
    }
}
