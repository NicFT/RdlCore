using Microsoft.ReportingServices.Common;
using SkiaSharp;
using System;
using System.IO;

namespace Microsoft.ReportingServices.OnDemandReportRendering
{
    internal static class ImageUtility
    {
        // Antes o método devolvia System.Drawing.Imaging.ImageFormat.
        // Agora devolve SKEncodedImageFormat (Png ou Jpeg).
        private static SKEncodedImageFormat GetTargetImageFormat(SKEncodedImageFormat imageFormat)
        {
            SKEncodedImageFormat result = SKEncodedImageFormat.Png;
            if (imageFormat == SKEncodedImageFormat.Jpeg)
            {
                result = SKEncodedImageFormat.Jpeg;
            }
            return result;
        }

        private static bool IsSupportedBySilverlight(SKEncodedImageFormat imageFormat)
        {
            if (!(imageFormat == SKEncodedImageFormat.Jpeg))
            {
                return imageFormat == SKEncodedImageFormat.Png;
            }
            return true;
        }

        private static double ConvertToPixels(RVUnit unit)
        {
            return unit.ToMillimeters() * 96.0 / 25.4;
        }

        internal static byte[] ScaleImage(byte[] sourceImageBytes, RVUnit frameWidth, RVUnit frameHeight)
        {
            SKEncodedImageFormat imageFormat;
            return ScaleImage(sourceImageBytes, (int)ConvertToPixels(frameWidth), (int)ConvertToPixels(frameHeight), out imageFormat);
        }

        internal static byte[] ScaleImage(byte[] sourceImageBytes, int frameWidth, int frameHeight, out SKEncodedImageFormat imageFormat)
        {
            // Evita divisão por zero quando o frame vier com dimensão 0.
            if (frameWidth <= 0)
            {
                frameWidth = 1;
            }
            if (frameHeight <= 0)
            {
                frameHeight = 1;
            }

            SKEncodedImageFormat sourceFormat;
            SKBitmap sourceBitmap;
            try
            {
                using (SKData data = SKData.CreateCopy(sourceImageBytes))
                using (SKCodec codec = SKCodec.Create(data))
                {
                    if (codec == null)
                    {
                        imageFormat = SKEncodedImageFormat.Png;
                        return null;
                    }
                    sourceFormat = codec.EncodedFormat;
                }

                sourceBitmap = SKBitmap.Decode(sourceImageBytes);
                if (sourceBitmap == null)
                {
                    imageFormat = SKEncodedImageFormat.Png;
                    return null;
                }

                imageFormat = GetTargetImageFormat(sourceFormat);
            }
            catch
            {
                imageFormat = SKEncodedImageFormat.Png;
                return null;
            }

            using (sourceBitmap)
            {
                int num = Math.Max(sourceBitmap.Width / frameWidth, sourceBitmap.Height / frameHeight);
                if (num > 1)
                {
                    int width = sourceBitmap.Width / num;
                    int height = sourceBitmap.Height / num;
                    if (width < 1)
                    {
                        width = 1;
                    }
                    if (height < 1)
                    {
                        height = 1;
                    }

                    SKImageInfo info = new SKImageInfo(width, height, sourceBitmap.ColorType, sourceBitmap.AlphaType);
                    using (SKBitmap scaled = sourceBitmap.Resize(info, SKFilterQuality.High))
                    {
                        if (scaled == null)
                        {
                            return null;
                        }
                        return Encode(scaled, imageFormat);
                    }
                }
                else
                {
                    // Imagem não precisa ser reduzida.
                    if (IsSupportedBySilverlight(sourceFormat))
                    {
                        return sourceImageBytes;
                    }
                    return Encode(sourceBitmap, imageFormat);
                }
            }
        }

        private static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format)
        {
            using (SKImage image = SKImage.FromBitmap(bitmap))
            using (SKData encoded = image.Encode(format, 90))
            {
                if (encoded == null)
                {
                    return null;
                }
                return encoded.ToArray();
            }
        }
    }
}
