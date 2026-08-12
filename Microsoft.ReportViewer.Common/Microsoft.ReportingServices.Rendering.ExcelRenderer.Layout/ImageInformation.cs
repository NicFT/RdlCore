using Microsoft.ReportingServices.OnDemandReportRendering;
using Microsoft.ReportingServices.Rendering.ExcelRenderer.Excel;
using Microsoft.ReportingServices.Rendering.RPLProcessing;
using SkiaSharp;
using System.IO;

namespace Microsoft.ReportingServices.Rendering.ExcelRenderer.Layout
{
	internal class ImageInformation
	{
		internal const string GIFMIMETYPE = "image/gif";

		internal const string JPGMIMETYPE = "image/jpg";

		internal const string JPEGMIMETYPE = "image/jpeg";

		internal const string PNGMIMETYPE = "image/png";

		internal const string BMPMIMETYPE = "image/bmp";

		internal const string XPNGMIMETYPE = "image/x-png";

		private Stream m_imageData;

		private RPLFormat.Sizings m_imageSizings = RPLFormat.Sizings.Fit;

		private ImageFormatType m_imageFormat = ImageFormatType.Unknown;

		private string m_imageName;

		private int m_width;

		private int m_height;

		private float m_horizontalResolution;

		private float m_verticalResolution;

		private string m_hyperlinkURL;

		private bool m_hyperlinkIsBookmark;

		private PaddingInformation m_paddings;

		internal Stream ImageData
		{
			get
			{
				return m_imageData;
			}
			set
			{
				m_imageData = value;
			}
		}

		internal string ImageName
		{
			get
			{
				return m_imageName;
			}
			set
			{
				m_imageName = value;
			}
		}

		internal RPLFormat.Sizings ImageSizings
		{
			get
			{
				return m_imageSizings;
			}
			set
			{
				m_imageSizings = value;
			}
		}

		internal ImageFormatType ImageFormat
		{
			get
			{
				if (m_imageFormat == ImageFormatType.Unknown)
				{
					CalculateMetrics();
				}
				return m_imageFormat;
			}
			set
			{
				m_imageFormat = value;
			}
		}

		internal int Width
		{
			get
			{
				if (m_width == 0)
				{
					CalculateMetrics();
				}
				return m_width;
			}
			set
			{
				m_width = value;
			}
		}

		internal int Height
		{
			get
			{
				if (m_height == 0)
				{
					CalculateMetrics();
				}
				return m_height;
			}
			set
			{
				m_height = value;
			}
		}

		internal float HorizontalResolution
		{
			get
			{
				if (m_horizontalResolution == 0f)
				{
					CalculateMetrics();
				}
				return m_horizontalResolution;
			}
			set
			{
				m_horizontalResolution = value;
			}
		}

		internal float VerticalResolution
		{
			get
			{
				if (m_verticalResolution == 0f)
				{
					CalculateMetrics();
				}
				return m_verticalResolution;
			}
			set
			{
				m_verticalResolution = value;
			}
		}

		internal string HyperlinkURL
		{
			get
			{
				return m_hyperlinkURL;
			}
			set
			{
				m_hyperlinkURL = value;
			}
		}

		internal bool HyperlinkIsBookmark
		{
			get
			{
				return m_hyperlinkIsBookmark;
			}
			set
			{
				m_hyperlinkIsBookmark = value;
			}
		}

		internal PaddingInformation Paddings
		{
			get
			{
				return m_paddings;
			}
			set
			{
				m_paddings = value;
			}
		}

		internal RPLFormat.Sizings Sizings
		{
			set
			{
				m_imageSizings = value;
			}
		}

		internal ImageInformation()
		{
		}

		internal void ReadImage(IExcelGenerator excel, RPLImageData image, string imageName, RPLReport report)
		{
			if (excel == null || image == null || report == null || imageName == null)
			{
				return;
			}
			SetMimeType(image.ImageMimeType);
			m_imageName = imageName;
			if (image.ImageData != null)
			{
				m_imageData = excel.CreateStream(imageName);
				m_imageData.Write(image.ImageData, 0, image.ImageData.Length);
			}
			else
			{
				if (image.ImageDataOffset <= 0)
				{
					m_imageData = null;
					image.ImageData = null;
					return;
				}
				m_imageData = excel.CreateStream(imageName);
				report.GetImage(image.ImageDataOffset, m_imageData);
			}
			image.ImageData = null;
			if (image.GDIImageProps != null)
			{
				m_width = image.GDIImageProps.Width;
				m_height = image.GDIImageProps.Height;
				m_verticalResolution = image.GDIImageProps.VerticalResolution;
				m_horizontalResolution = image.GDIImageProps.HorizontalResolution;
                m_imageFormat = FromSkiaImageFormat(image.GDIImageProps.RawFormat);
            }
		}
        private static ImageFormatType FromSkiaImageFormat(SKEncodedImageFormat format)
        {
            switch (format)
            {
                case SKEncodedImageFormat.Png:
                    return ImageFormatType.Png;
                case SKEncodedImageFormat.Jpeg:
                    return ImageFormatType.Jpeg;
                case SKEncodedImageFormat.Gif:
                    return ImageFormatType.Gif;
                case SKEncodedImageFormat.Bmp:
                    return ImageFormatType.Bmp;
                default:
                    return ImageFormatType.Png; // fallback seguro
            }
        }
        internal void SetMimeType(string mimeType)
		{
			if (mimeType == null)
			{
				return;
			}
			m_imageFormat = ImageFormatTypeHelper.FromMimeType(mimeType);
			if (m_imageFormat == ImageFormatType.Unknown)
			{
				throw new ReportRenderingException(ExcelRenderRes.UnknownImageFormat(mimeType));
			}
		}

		private void CalculateMetrics()
		{
			if (m_imageData != null && m_imageData.Length != 0L)
			{
				m_imageData.Position = 0L;
				using SKCodec codec = SKCodec.Create(m_imageData);
				if (codec != null)
				{
					m_imageData.Position = 0L;
					Width = codec.Info.Width;
					Height = codec.Info.Height;
					// SkiaSharp carries no embedded resolution metadata; assume the same
					// fixed 96 DPI baseline used elsewhere in the cross-platform renderers
					// (docs/decisions.md, "ImageLoader's DPI-mismatch rescaling was dropped").
					HorizontalResolution = 96f;
					VerticalResolution = 96f;
					if (m_imageFormat == ImageFormatType.Unknown)
					{
						m_imageFormat = DetermineImageFormat();
					}
				}
			}
		}

		private ImageFormatType DetermineImageFormat()
		{
			if (m_imageData == null || m_imageData.Length == 0)
			{
				return ImageFormatType.Png;
			}
			return ImageFormatTypeHelper.DetectFromStream(m_imageData);
		}
	}
}
