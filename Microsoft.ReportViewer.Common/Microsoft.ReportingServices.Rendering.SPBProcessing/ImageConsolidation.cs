using Microsoft.ReportingServices.Interfaces;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Microsoft.ReportingServices.Rendering.SPBProcessing
{
    internal class ImageConsolidation
    {
        protected const float SUPPORTED_DPI = 96f;

        protected const float DPI_TOLERANCE = 0.02f;

        private const float OUTPUT_DPI = 96f;

        public static string STREAMPREFIX = "IMGCON_";

        public List<ImageInfo> ImageInfos = new List<ImageInfo>();

        public int MaxHeight;

        public int MaxWidth;

        public int CurrentOffset;

        private static int MAXIMAGECONSOLIDATION_TOTALSIZE = 200000;

        private static int MAXIMAGECONSOLIDATION_PERIMAGESIZE = MAXIMAGECONSOLIDATION_TOTALSIZE / 10;

        private CreateAndRegisterStream m_createAndRegisterStream;

        private int m_currentByteCount;

        private int m_ignoreOffsetTill = -1;

        private string m_imagePrefix;

        public int IgnoreOffsetTill => m_ignoreOffsetTill;

        public ImageConsolidation(CreateAndRegisterStream createAndRegisterStream)
            : this(createAndRegisterStream, -1)
        {
        }

        public ImageConsolidation(CreateAndRegisterStream createAndRegisterStream, int ignoreOffsetTill)
        {
            m_createAndRegisterStream = createAndRegisterStream;
            m_ignoreOffsetTill = ignoreOffsetTill;
        }

        // Retorna SKRectI (era System.Drawing.Rectangle). SKRectI.Empty == default.
        public SKRectI AppendImage(Stream imageStream)
        {
            if (imageStream == null)
            {
                return SKRectI.Empty;
            }
            long length = imageStream.Length;
            if (length > MAXIMAGECONSOLIDATION_PERIMAGESIZE)
            {
                return SKRectI.Empty;
            }
            if (m_currentByteCount + length > MAXIMAGECONSOLIDATION_TOTALSIZE)
            {
                RenderToStream();
                if (m_ignoreOffsetTill > -1 && m_ignoreOffsetTill + 1 == CurrentOffset)
                {
                    return SKRectI.Empty;
                }
            }
            ImageInfo imageInfo = new ImageInfo();
            imageInfo.ImageData = imageStream;
            long position = imageStream.Position;
            bool isPng = false;
            int frameCount = 1;
            try
            {
                // Lê os bytes para decodificar via SKCodec/SKBitmap sem GDI+.
                byte[] buffer = ReadAll(imageStream);
                imageStream.Position = position;
                using (SKData data = SKData.CreateCopy(buffer))
                using (SKCodec codec = SKCodec.Create(data))
                {
                    if (codec == null)
                    {
                        return SKRectI.Empty;
                    }
                    imageInfo.Width = codec.Info.Width;
                    imageInfo.Height = codec.Info.Height;
                    isPng = (codec.EncodedFormat == SKEncodedImageFormat.Png);
                    // SKCodec.FrameInfo cobre imagens animadas (ex.: GIF). PNG estático => 0 ou 1.
                    frameCount = (codec.FrameCount <= 1) ? 1 : codec.FrameCount;
                }
            }
            catch (Exception)
            {
                return SKRectI.Empty;
            }
            // SkiaSharp já decodifica em 96 DPI lógico; mantemos a checagem de PNG e frame único.
            if (!isPng || frameCount != 1)
            {
                return SKRectI.Empty;
            }
            SKRectI result = SKRectI.Empty;
            if (CurrentOffset >= m_ignoreOffsetTill)
            {
                ImageInfos.Add(imageInfo);
                imageStream.Position = position;
                result = new SKRectI(0, MaxHeight, imageInfo.Width, MaxHeight + imageInfo.Height);
                MaxHeight += imageInfo.Height;
                MaxWidth = Math.Max(MaxWidth, imageInfo.Width);
            }
            m_currentByteCount += (int)length;
            return result;
        }

        // Retorna SKBitmap (era System.Drawing.Image).
        public SKBitmap Render()
        {
            if (ImageInfos.Count == 0 || MaxWidth == 0 || MaxHeight == 0)
            {
                return null;
            }
            SKBitmap bitmap = new SKBitmap(MaxWidth, MaxHeight);
            using (SKCanvas g = new SKCanvas(bitmap))
            {
                g.Clear(SKColors.Transparent);
                int num = 0;
                foreach (ImageInfo imageInfo in ImageInfos)
                {
                    imageInfo.RenderAndDispose(g, 0, num);
                    num += imageInfo.Height;
                }
            }
            ImageInfos.Clear();
            return bitmap;
        }

        public static string GetStreamName(string reportName, int page)
        {
            if (page > 0)
            {
                return STREAMPREFIX + page.ToString(CultureInfo.InvariantCulture);
            }
            return STREAMPREFIX;
        }

        public string GetStreamName()
        {
            return m_imagePrefix + CurrentOffset;
        }

        public void SetName(string reportName, int pageNumber)
        {
            m_imagePrefix = STREAMPREFIX + pageNumber.ToString(CultureInfo.InvariantCulture) + "_";
        }

        public void RenderToStream()
        {
            if (m_currentByteCount > 0 && ImageInfos.Count > 0)
            {
                string streamName = GetStreamName();
                Stream stream = m_createAndRegisterStream(streamName, "png", null, PageContext.PNG_MIME_TYPE, willSeek: false, StreamOper.CreateAndRegister);
                using (SKBitmap image = Render())
                {
                    if (image != null)
                    {
                        using (SKImage skImage = SKImage.FromBitmap(image))
                        using (SKData encoded = skImage.Encode(SKEncodedImageFormat.Png, 100))
                        {
                            encoded?.SaveTo(stream);
                        }
                    }
                }
            }
            CurrentOffset++;
            m_currentByteCount = 0;
            MaxHeight = 0;
            MaxWidth = 0;
        }

        public void ResetCancelPage()
        {
            if (CurrentOffset > 0 && m_ignoreOffsetTill < CurrentOffset)
            {
                m_ignoreOffsetTill = CurrentOffset;
            }
            CurrentOffset = 0;
            foreach (ImageInfo imageInfo in ImageInfos)
            {
                imageInfo.Dispose();
            }
            ImageInfos.Clear();
            m_currentByteCount = 0;
            MaxHeight = 0;
            MaxWidth = 0;
        }

        public void Reset()
        {
            CurrentOffset = 0;
        }

        private static byte[] ReadAll(Stream stream)
        {
            if (stream is MemoryStream ms)
            {
                return ms.ToArray();
            }
            using (MemoryStream copy = new MemoryStream())
            {
                long pos = stream.CanSeek ? stream.Position : 0;
                stream.CopyTo(copy);
                if (stream.CanSeek)
                {
                    stream.Position = pos;
                }
                return copy.ToArray();
            }
        }

        private bool IsDPISupported(float dpiX, float dpiY)
        {
            if (95.98f < dpiX && 96.02f > dpiX && 95.98f < dpiY)
            {
                return 96.02f > dpiY;
            }
            return false;
        }
    }
}
