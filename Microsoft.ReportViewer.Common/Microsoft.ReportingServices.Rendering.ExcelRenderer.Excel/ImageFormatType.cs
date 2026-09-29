using SkiaSharp;
using System;
using System.Drawing.Imaging;
using System.IO;

namespace Microsoft.ReportingServices.Rendering.ExcelRenderer.Excel
{
	/// <summary>
	/// Cross-platform image format enumeration for Excel rendering.
	/// Replaces System.Drawing.Imaging.ImageFormat to maintain cross-platform compatibility.
	/// </summary>
	internal enum ImageFormatType
	{
		/// <summary>BMP (Bitmap) format</summary>
		Bmp,

		/// <summary>GIF (Graphics Interchange Format)</summary>
		Gif,

		/// <summary>JPEG (Joint Photographic Experts Group)</summary>
		Jpeg,

		/// <summary>PNG (Portable Network Graphics)</summary>
		Png,

		/// <summary>Unknown or unsupported format (defaults to PNG)</summary>
		Unknown
	}

	/// <summary>
	/// Helper class for ImageFormatType operations.
	/// </summary>
	internal static class ImageFormatTypeHelper
	{
		/// <summary>
		/// Convert ImageFormatType to file extension.
		/// </summary>
		public static string ToFileExtension(ImageFormatType format)
		{
			return format switch
			{
				ImageFormatType.Bmp => "bmp",
				ImageFormatType.Gif => "gif",
				ImageFormatType.Jpeg => "jpg",
				ImageFormatType.Png => "png",
				_ => "png" // Default to PNG
			};
		}

		/// <summary>
		/// Convert ImageFormatType to MIME type.
		/// </summary>
		public static string ToMimeType(ImageFormatType format)
		{
			return format switch
			{
				ImageFormatType.Bmp => "image/bmp",
				ImageFormatType.Gif => "image/gif",
				ImageFormatType.Jpeg => "image/jpeg",
				ImageFormatType.Png => "image/png",
				_ => "image/png" // Default to PNG
			};
		}

		/// <summary>
		/// Detect image format from MIME type string.
		/// </summary>
		public static ImageFormatType FromMimeType(string mimeType)
		{
			if (string.IsNullOrEmpty(mimeType))
				return ImageFormatType.Unknown;

			return mimeType.ToLowerInvariant() switch
			{
				"image/bmp" or "image/x-windows-bmp" => ImageFormatType.Bmp,
				"image/gif" => ImageFormatType.Gif,
				"image/jpeg" or "image/jpg" => ImageFormatType.Jpeg,
				"image/png" or "image/x-png" => ImageFormatType.Png,
				_ => ImageFormatType.Unknown
			};
		}

		/// <summary>
		/// Detect image format from image data stream using SkiaSharp.
		/// </summary>
		public static ImageFormatType DetectFromStream(Stream imageStream)
		{
			if (imageStream == null || imageStream.Length == 0)
				return ImageFormatType.Unknown;

			try
			{
				imageStream.Position = 0;
				// Non-owning wrapper: SKCodec.Create(Stream) would close the caller's stream on dispose.
				using SKManagedStream skStream = new SKManagedStream(imageStream, disposeManagedStream: false);
				using SKCodec codec = SKCodec.Create(skStream);
				imageStream.Position = 0;
				if (codec == null)
					return ImageFormatType.Unknown;

				return codec.EncodedFormat switch
				{
					SKEncodedImageFormat.Bmp => ImageFormatType.Bmp,
					SKEncodedImageFormat.Gif => ImageFormatType.Gif,
					SKEncodedImageFormat.Jpeg => ImageFormatType.Jpeg,
					SKEncodedImageFormat.Png => ImageFormatType.Png,
					_ => ImageFormatType.Unknown
				};
			}
			catch
			{
				return ImageFormatType.Unknown;
			}
		}

		/// <summary>
		/// Convert System.Drawing.Imaging.ImageFormat to ImageFormatType.
		/// </summary>
		public static ImageFormatType FromSystemDrawingImageFormat(ImageFormat format)
		{
			if (format == null)
				return ImageFormatType.Unknown;

			if (format.Guid == ImageFormat.Bmp.Guid)
				return ImageFormatType.Bmp;
			if (format.Guid == ImageFormat.Gif.Guid)
				return ImageFormatType.Gif;
			if (format.Guid == ImageFormat.Jpeg.Guid)
				return ImageFormatType.Jpeg;
			if (format.Guid == ImageFormat.Png.Guid)
				return ImageFormatType.Png;

			return ImageFormatType.Unknown;
		}
	}
}
