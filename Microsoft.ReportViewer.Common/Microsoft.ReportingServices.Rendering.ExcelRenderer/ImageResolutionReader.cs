using System;

namespace Microsoft.ReportingServices.Rendering.ExcelRenderer
{
    /// <summary>
    /// Lê a resolução (DPI) real embutida nos metadados de um arquivo de imagem,
    /// sem depender de System.Drawing/GDI+ - necessário porque SKBitmap não expõe
    /// essa informação, mas o antigo caminho GDI+ (Image.HorizontalResolution/
    /// VerticalResolution) lia automaticamente, causando divergência de tamanho/
    /// proporção entre o Windows (GDI+) e o Linux (SkiaSharp) quando a imagem não é 96 DPI.
    /// </summary>
    internal static class ImageResolutionReader
    {
        private const float DefaultDpi = 96f;

        /// <summary>
        /// Tenta ler o DPI horizontal/vertical real dos bytes da imagem.
        /// Retorna (96, 96) se o formato não for suportado ou não tiver metadados de resolução.
        /// </summary>
        internal static (float HorizontalDpi, float VerticalDpi) ReadDpi(byte[] imageData)
        {
            if (imageData == null || imageData.Length < 8)
            {
                return (DefaultDpi, DefaultDpi);
            }

            // PNG: assinatura 89 50 4E 47 0D 0A 1A 0A
            if (imageData[0] == 0x89 && imageData[1] == 0x50 && imageData[2] == 0x4E && imageData[3] == 0x47)
            {
                if (TryReadPngDpi(imageData, out float pngHx, out float pngVy))
                {
                    return (pngHx, pngVy);
                }
                return (DefaultDpi, DefaultDpi);
            }

            // JPEG: assinatura FF D8 FF
            if (imageData[0] == 0xFF && imageData[1] == 0xD8 && imageData[2] == 0xFF)
            {
                if (TryReadJpegDpi(imageData, out float jpgHx, out float jpgVy))
                {
                    return (jpgHx, jpgVy);
                }
                return (DefaultDpi, DefaultDpi);
            }

            // BMP/GIF e outros: sem metadados de resolução relevantes - mantém 96x96
            return (DefaultDpi, DefaultDpi);
        }

        /// <summary>
        /// Lê o chunk "pHYs" do PNG (pixels por metro), convertendo para DPI (pixels por polegada).
        /// </summary>
        private static bool TryReadPngDpi(byte[] data, out float horizontalDpi, out float verticalDpi)
        {
            horizontalDpi = DefaultDpi;
            verticalDpi = DefaultDpi;

            int pos = 8; // pula a assinatura PNG
            while (pos + 8 <= data.Length)
            {
                uint chunkLength = ReadUInt32BigEndian(data, pos);
                string chunkType = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);

                if (chunkType == "pHYs" && pos + 8 + 9 <= data.Length)
                {
                    int chunkDataStart = pos + 8;
                    uint pixelsPerUnitX = ReadUInt32BigEndian(data, chunkDataStart);
                    uint pixelsPerUnitY = ReadUInt32BigEndian(data, chunkDataStart + 4);
                    byte unitSpecifier = data[chunkDataStart + 8];

                    if (unitSpecifier == 1 && pixelsPerUnitX > 0 && pixelsPerUnitY > 0)
                    {
                        // pixels por metro -> pixels por polegada (1 polegada = 0.0254 metros)
                        horizontalDpi = pixelsPerUnitX * 0.0254f;
                        verticalDpi = pixelsPerUnitY * 0.0254f;
                        return true;
                    }
                    return false; // unitSpecifier == 0 significa "proporção desconhecida", sem DPI real
                }

                if (chunkType == "IDAT" || chunkType == "IEND")
                {
                    break; // pHYs sempre vem antes do IDAT; se chegou aqui, não existe no arquivo
                }

                pos += 8 + (int)chunkLength + 4; // avança: header + dados + CRC
            }

            return false;
        }

        /// <summary>
        /// Lê o marcador JFIF (APP0) do JPEG, que guarda densidade + unidade.
        /// </summary>
        private static bool TryReadJpegDpi(byte[] data, out float horizontalDpi, out float verticalDpi)
        {
            horizontalDpi = DefaultDpi;
            verticalDpi = DefaultDpi;

            int pos = 2; // pula FF D8
            while (pos + 4 <= data.Length)
            {
                if (data[pos] != 0xFF)
                {
                    break;
                }
                byte marker = data[pos + 1];
                int segmentLength = (data[pos + 2] << 8) | data[pos + 3];

                if (marker == 0xE0 && pos + 4 + 9 <= data.Length) // APP0 (JFIF)
                {
                    int segStart = pos + 4;
                    bool isJfif = data[segStart] == 'J' && data[segStart + 1] == 'F' && data[segStart + 2] == 'I' && data[segStart + 3] == 'F';
                    if (isJfif)
                    {
                        byte units = data[segStart + 7];
                        int xDensity = (data[segStart + 8] << 8) | data[segStart + 9];
                        int yDensity = (data[segStart + 10] << 8) | data[segStart + 11];

                        if (units == 1 && xDensity > 0 && yDensity > 0) // pixels por polegada
                        {
                            horizontalDpi = xDensity;
                            verticalDpi = yDensity;
                            return true;
                        }
                        if (units == 2 && xDensity > 0 && yDensity > 0) // pixels por cm -> por polegada
                        {
                            horizontalDpi = xDensity * 2.54f;
                            verticalDpi = yDensity * 2.54f;
                            return true;
                        }
                        return false;
                    }
                }

                if (marker == 0xD8 || marker == 0xD9 || (marker >= 0xD0 && marker <= 0xD7))
                {
                    pos += 2;
                    continue;
                }

                pos += 2 + segmentLength;
            }

            return false;
        }

        private static uint ReadUInt32BigEndian(byte[] data, int offset)
        {
            return (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
        }
    }
}
