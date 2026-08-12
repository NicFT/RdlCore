using SkiaSharp;
using System;
using System.IO;

namespace Microsoft.ReportingServices.Rendering.SPBProcessing
{
    internal class ImageConverter
    {
        // Converte para PNG qualquer formato que não seja PNG/JPEG, usando SkiaSharp (sem GDI+).
        public static bool Convert(ref byte[] imageData, ref string imageMimeType)
        {
            try
            {
                SKEncodedImageFormat sourceFormat;
                using (SKData data = SKData.CreateCopy(imageData))
                using (SKCodec codec = SKCodec.Create(data))
                {
                    if (codec == null)
                    {
                        // Não foi possível decodificar -> comportamento equivalente ao antigo
                        // ArgumentException/ExternalException do GDI+.
                        throw new ArgumentOutOfRangeException();
                    }
                    sourceFormat = codec.EncodedFormat;
                }

                if (NeedsToConvert(sourceFormat))
                {
                    using (SKBitmap bitmap = SKBitmap.Decode(imageData))
                    {
                        if (bitmap == null)
                        {
                            throw new ArgumentOutOfRangeException();
                        }
                        using (SKImage image = SKImage.FromBitmap(bitmap))
                        using (SKData encoded = image.Encode(SKEncodedImageFormat.Png, 100))
                        {
                            imageData = encoded.ToArray();
                            imageMimeType = PageContext.PNG_MIME_TYPE;
                            return true;
                        }
                    }
                }
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                throw;
            }
            catch (ArgumentException)
            {
                throw new ArgumentOutOfRangeException();
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static bool NeedsToConvert(SKEncodedImageFormat rawFormat)
        {
            if (rawFormat != SKEncodedImageFormat.Png)
            {
                return rawFormat != SKEncodedImageFormat.Jpeg;
            }
            return false;
        }
    }
}
