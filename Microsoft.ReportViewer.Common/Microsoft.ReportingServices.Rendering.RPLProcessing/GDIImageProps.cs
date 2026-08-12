using SkiaSharp;

namespace Microsoft.ReportingServices.Rendering.RPLProcessing
{
    internal sealed class GDIImageProps
    {
        private int m_width;

        private int m_height;

        private float m_horizontalResolution;

        private float m_verticalResolution;

        private SKEncodedImageFormat m_rawFormat;

        public int Width
        {
            get
            {
                return m_width;
            }
            set
            {
                m_width = value;
            }
        }

        public int Height
        {
            get
            {
                return m_height;
            }
            set
            {
                m_height = value;
            }
        }

        public float VerticalResolution
        {
            get
            {
                return m_verticalResolution;
            }
            set
            {
                m_verticalResolution = value;
            }
        }

        public float HorizontalResolution
        {
            get
            {
                return m_horizontalResolution;
            }
            set
            {
                m_horizontalResolution = value;
            }
        }

        public SKEncodedImageFormat RawFormat
        {
            get
            {
                return m_rawFormat;
            }
            set
            {
                m_rawFormat = value;
            }
        }

        internal GDIImageProps()
        {
        }

        // Caminho Windows/GDI+: mantido para os consumidores já portados que usam
        // System.Drawing.Image apenas quando OperatingSystem.IsWindows() (ex.: ImageWriter).
        // Esse construtor NUNCA é chamado no Linux, então não inicializa GDI+ lá.
        public GDIImageProps(System.Drawing.Image image)
        {
            m_width = image.Width;
            m_height = image.Height;
            m_horizontalResolution = image.HorizontalResolution;
            m_verticalResolution = image.VerticalResolution;
            m_rawFormat = SKEncodedImageFormat.Png;
        }

        // Caminho Skia (Linux e também Windows quando o item foi decodificado via SkiaSharp).
        // SKBitmap não carrega DPI nem formato de origem, então são informados pelo chamador.
        public GDIImageProps(SKBitmap image, float horizontalResolution = 96f, float verticalResolution = 96f, SKEncodedImageFormat rawFormat = SKEncodedImageFormat.Png)
        {
            m_width = image.Width;
            m_height = image.Height;
            m_horizontalResolution = horizontalResolution;
            m_verticalResolution = verticalResolution;
            m_rawFormat = rawFormat;
        }
    }
}
