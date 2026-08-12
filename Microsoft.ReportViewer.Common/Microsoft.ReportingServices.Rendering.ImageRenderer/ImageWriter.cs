using Microsoft.ReportingServices.Interfaces;
using Microsoft.ReportingServices.Rendering.HPBProcessing;
using Microsoft.ReportingServices.Rendering.RichText;
using Microsoft.ReportingServices.Rendering.RPLProcessing;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace Microsoft.ReportingServices.Rendering.ImageRenderer
{
	internal sealed class ImageWriter : WriterBase
	{
		internal const char StreamNameSeparator = '_';

		private Graphics m_graphics;

		private Dictionary<string, PortableImage> m_cachedImages = new Dictionary<string, PortableImage>();

		internal PaginationSettings.FormatEncoding OutputFormat;

		private RectangleF MetafileRectangle = RectangleF.Empty;

		private Dictionary<string, Pen> m_pens = new Dictionary<string, Pen>();

		private Dictionary<string, Brush> m_brushes = new Dictionary<string, Brush>();

		private System.Drawing.Rectangle m_bodyRect = System.Drawing.Rectangle.Empty;

		private Microsoft.ReportingServices.Rendering.RichText.Win32.POINT m_prevViewportOrg;

		private int m_dpiX;

		private int m_dpiY;

		private int m_measureImageDpiX;

		private int m_measureImageDpiY;

		private int DEFAULT_RESOLUTION_X = 96;

		private int DYNAMIC_IMAGE_MIN_RESOLUTION_X = 300;

		private int DEFAULT_RESOLUTION_Y = 96;

		private int DYNAMIC_IMAGE_MIN_RESOLUTION_Y = 300;

		private ShapedFontCache m_shapedFontCache;

		private ShapedFontCache ShapedFontCache => m_shapedFontCache ??= new ShapedFontCache();

		internal bool IsEmf
		{
			get
			{
				if (OutputFormat != PaginationSettings.FormatEncoding.EMFPLUS)
				{
					return OutputFormat == PaginationSettings.FormatEncoding.EMF;
				}
				return true;
			}
		}

		internal Stream OutputStream
		{
			set
			{
				m_outputStream = value;
			}
		}

		internal ImageWriter(Renderer renderer, Stream stream, bool disposeRenderer, CreateAndRegisterStream createAndRegisterStream, int measureImageDpiX, int measureImageDpiY)
			: base(renderer, stream, disposeRenderer, createAndRegisterStream)
		{
			m_measureImageDpiX = measureImageDpiX;
			m_measureImageDpiY = measureImageDpiY;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (m_cachedImages != null)
				{
					foreach (string key in m_cachedImages.Keys)
					{
						m_cachedImages[key].Dispose();
					}
					m_cachedImages = null;
				}
				if (m_pens != null)
				{
					foreach (string key2 in m_pens.Keys)
					{
						m_pens[key2].Dispose();
					}
					m_pens = null;
				}
				if (m_brushes != null)
				{
					foreach (string key3 in m_brushes.Keys)
					{
						m_brushes[key3].Dispose();
					}
					m_brushes = null;
				}
				if (m_graphics != null)
				{
					m_graphics.Dispose();
					m_graphics = null;
				}
				if (m_shapedFontCache != null)
				{
					m_shapedFontCache.Dispose();
					m_shapedFontCache = null;
				}
			}
			base.Dispose(disposing);
		}

		~ImageWriter()
		{
			Dispose(disposing: false);
		}

		internal override void BeginReport(int dpiX, int dpiY)
		{
			m_dpiX = dpiX;
			m_dpiY = dpiY;
			if (!IsEmf)
			{
				m_graphics = new Graphics(dpiX, dpiY);
			}
			else
			{
				m_graphics = new MetafileGraphics(dpiX, dpiY);
			}
			m_commonGraphics = m_graphics;
		}

		internal override void BeginPage(float pageWidth, float pageHeight)
		{
			if (!IsEmf)
			{
				m_graphics.NewPage(pageWidth, pageHeight, m_commonGraphics.DpiX, m_commonGraphics.DpiY);
			}
			else
			{
				((MetafileGraphics)m_graphics).NewPage(m_outputStream, OutputFormat, pageWidth, pageHeight, m_commonGraphics.DpiX, m_commonGraphics.DpiY);
			}
		}

		internal override void BeginPageSection(RectangleF bounds)
		{
			base.BeginPageSection(bounds);
			int dpiX = m_commonGraphics.DpiX;
			int dpiY = m_commonGraphics.DpiY;
			m_bodyRect = new System.Drawing.Rectangle(SharedRenderer.ConvertToPixels(bounds.X, dpiX), SharedRenderer.ConvertToPixels(bounds.Y, dpiY), SharedRenderer.ConvertToPixels(bounds.Width + HalfPixelWidthX, dpiX), SharedRenderer.ConvertToPixels(bounds.Height + HalfPixelWidthY, dpiY));
			m_graphics.ResetClipAndTransform(new RectangleF(bounds.Left, bounds.Top, bounds.Width + HalfPixelWidthX, bounds.Height + HalfPixelWidthY));
		}

		internal override RectangleF CalculateColumnBounds(RPLReportSection reportSection, RPLPageLayout pageLayout, RPLItemMeasurement column, int columnNumber, float top, float columnHeight, float columnWidth)
		{
			return HardPageBreakShared.CalculateColumnBounds(reportSection, pageLayout, columnNumber, top, columnHeight);
		}

		internal override RectangleF CalculateHeaderBounds(RPLReportSection section, RPLPageLayout pageLayout, float top, float width)
		{
			return HardPageBreakShared.CalculateHeaderBounds(section, pageLayout, top, width);
		}

		internal override RectangleF CalculateFooterBounds(RPLReportSection section, RPLPageLayout pageLayout, float top, float width)
		{
			return HardPageBreakShared.CalculateFooterBounds(section, pageLayout, top, width);
		}

		internal override void DrawBackgroundImage(RPLImageData imageData, RPLFormat.BackgroundRepeatTypes repeat, PointF start, RectangleF position)
		{
			PortableImage image;
			bool image2 = GetImage(imageData.ImageName, imageData.ImageData, imageData.ImageDataOffset, dynamicImage: false, out image);
			if (image == null)
			{
				return;
			}
			RectangleF destination;
			RectangleF source;
			if (repeat == RPLFormat.BackgroundRepeatTypes.Clip)
			{
				if (SharedRenderer.CalculateImageClippedUnscaledBounds(this, position, image.Width, image.Height, start.X, start.Y, m_measureImageDpiX, m_measureImageDpiY, out destination, out source))
				{
					DrawPortableImage(image, destination, source);
				}
			}
			else
			{
				float num = SharedRenderer.ConvertToMillimeters(image.Width, m_measureImageDpiX);
				float num2 = SharedRenderer.ConvertToMillimeters(image.Height, m_measureImageDpiY);
				float num3 = position.Width;
				if (repeat == RPLFormat.BackgroundRepeatTypes.RepeatY)
				{
					num3 = num;
				}
				float num4 = position.Height;
				if (repeat == RPLFormat.BackgroundRepeatTypes.RepeatX)
				{
					num4 = num2;
				}
				for (float num5 = start.X; num5 < num3; num5 += num)
				{
					for (float num6 = start.Y; num6 < num4; num6 += num2)
					{
						if (SharedRenderer.CalculateImageClippedUnscaledBounds(this, position, image.Width, image.Height, num5, num6, m_measureImageDpiX, m_measureImageDpiY, out destination, out source))
						{
							DrawPortableImage(image, destination, source);
						}
					}
				}
			}
			if (!image2)
			{
				image.Dispose();
				image = null;
			}
		}

		internal override void DrawLine(Color color, float size, RPLFormat.BorderStyles style, float x1, float y1, float x2, float y2)
		{
			if (OperatingSystem.IsWindows())
			{
				m_graphics.DrawLine(GDIPen.GetPen(m_pens, color, ConvertToPixels(size), style), x1, y1, x2, y2);
			}
			else
			{
				m_graphics.DrawLine(color, ConvertToPixels(size), style, x1, y1, x2, y2);
			}
		}

		/// <summary>
		/// Draws a decoded PortableImage on whichever backend is active - GDI+ on Windows, Skia
		/// (via the already-decoded BGRA32 buffer) elsewhere. tile is honored on the GDI+ path
		/// only - the Skia path doesn't implement tiling yet (see Graphics.DrawImage's byte[] overload).
		/// </summary>
		private void DrawPortableImage(PortableImage image, RectangleF destination, RectangleF source, bool tile = true)
		{
			if (image.IsGdiBacked)
			{
				m_graphics.DrawImage(image.GdiImage, destination, source, tile);
			}
			else
			{
				m_graphics.DrawImage(image.Bgra32Pixels, image.Width, image.Height, destination, source);
			}
		}

		internal void GetDefaultImage(out PortableImage image)
		{
            throw new PlatformNotSupportedException("The default/placeholder image fallback is not yet supported on non-Windows platforms.");
			/*
            string key = "__int__InvalidImage";
			if (m_cachedImages.TryGetValue(key, out image))
			{
				return;
			}
			if (!OperatingSystem.IsWindows())
			{
				// The "InvalidImage" placeholder is itself a GDI+ Bitmap resource
				// (Renderer.ImageResources) - not yet ported to a portable equivalent.
				// Only reached when the primary image decode fails (corrupt/unsupported
				// image), an edge case - see tasks/image-renderer-cross-platform.md.
				throw new PlatformNotSupportedException("The default/placeholder image fallback is not yet supported on non-Windows platforms.");
			}
			Bitmap bitmap = Renderer.ImageResources["InvalidImage"];
			Bitmap bitmap2 = null;
			lock (bitmap)
			{
				using (MemoryStream stream = new MemoryStream())
				{
					bitmap.Save(stream, bitmap.RawFormat);
					bitmap2 = new Bitmap(stream);
				}
			}
			bitmap2.SetResolution(m_commonGraphics.DpiX, m_commonGraphics.DpiY);
			image = PortableImage.FromGdiImage(bitmap2);
			m_cachedImages.Add(key, image);*/
		}

		internal override void DrawDynamicImage(string imageName, Stream imageStream, long imageDataOffset, RectangleF position)
		{
			PortableImage image;
			bool flag = GetImage(imageName, imageStream, imageDataOffset, dynamicImage: true, out image);
			if (image == null)
			{
				GetDefaultImage(out image);
				flag = true;
			}
			GetScreenDpi(out int dpiX, out int _);
			float num = 1f * (float)DEFAULT_RESOLUTION_X / (float)dpiX;
			RectangleF source = new RectangleF(0f, 0f, num * (float)image.Width, num * (float)image.Height);
			DrawPortableImage(image, position, source, tile: false);
			if (!flag)
			{
				image.Dispose();
				image = null;
			}
		}

		internal override void DrawImage(RectangleF position, RPLImage image, RPLImageProps instanceProperties, RPLImagePropsDef definitionProperties)
		{
			RPLImageData image2 = instanceProperties.Image;
			PortableImage image3;
			bool flag = GetImage(image2.ImageName, image2.ImageData, image2.ImageDataOffset, dynamicImage: false, out image3);
			RPLFormat.Sizings sizing = definitionProperties.Sizing;
			if (image3 == null)
			{
				GetDefaultImage(out image3);
				flag = true;
				sizing = RPLFormat.Sizings.Clip;
			}
			GDIImageProps gDIImageProps = image3.IsGdiBacked
				? new GDIImageProps(image3.GdiImage)
				: new GDIImageProps { Width = image3.Width, Height = image3.Height, HorizontalResolution = image3.HorizontalResolution, VerticalResolution = image3.VerticalResolution };
			SharedRenderer.CalculateImageRectangle(position, gDIImageProps.Width, gDIImageProps.Height, m_measureImageDpiX, m_measureImageDpiY, sizing, out RectangleF imagePositionAndSize, out RectangleF imagePortion);
			DrawPortableImage(image3, imagePositionAndSize, imagePortion);
			if (!flag)
			{
				image3.Dispose();
				image3 = null;
			}
		}

		internal override void DrawRectangle(Color color, float size, RPLFormat.BorderStyles style, RectangleF rectangle)
		{
			if (OperatingSystem.IsWindows())
			{
				m_graphics.DrawRectangle(GDIPen.GetPen(m_pens, color, ConvertToPixels(size), style), rectangle);
			}
			else
			{
				m_graphics.DrawRectangle(color, ConvertToPixels(size), style, rectangle);
			}
		}

		internal override void DrawTextRun(Win32DCSafeHandle hdc, FontCache fontCache, ReportTextBox textBox, Microsoft.ReportingServices.Rendering.RichText.TextRun run, TypeCode typeCode, RPLFormat.TextAlignments textAlign, RPLFormat.VerticalAlignments verticalAlign, RPLFormat.WritingModes writingMode, RPLFormat.Directions direction, Point pointPosition, System.Drawing.Rectangle layoutRectangle, int lineHeight, int baselineY)
		{
			if (!string.IsNullOrEmpty(run.Text))
			{
				int x;
				int baselineY2;
				switch (writingMode)
				{
				case RPLFormat.WritingModes.Horizontal:
					x = layoutRectangle.X + pointPosition.X;
					baselineY2 = layoutRectangle.Y + baselineY;
					break;
				case RPLFormat.WritingModes.Vertical:
					x = layoutRectangle.X + (layoutRectangle.Width - baselineY);
					baselineY2 = layoutRectangle.Y + pointPosition.X;
					break;
				case RPLFormat.WritingModes.Rotate270:
					x = layoutRectangle.X + baselineY;
					baselineY2 = layoutRectangle.Y + layoutRectangle.Height - pointPosition.X;
					break;
				default:
					throw new NotSupportedException();
				}
				Underline underline = null;
				if (run.UnderlineHeight > 0)
				{
					underline = new Underline(run, hdc, fontCache, layoutRectangle, pointPosition.X, baselineY, writingMode);
				}
				if (!IsEmf)
				{
					Microsoft.ReportingServices.Rendering.RichText.TextBox.DrawTextRun(run, hdc, fontCache, x, baselineY2, underline);
				}
				else
				{
					Microsoft.ReportingServices.Rendering.RichText.TextBox.ExtDrawTextRun(run, hdc, fontCache, x, baselineY2, underline);
				}
			}
		}

		/// <summary>
		/// Phase 3 (tasks/image-renderer-cross-platform.md): the non-Windows text path. Only
		/// called on non-Windows (see WriterBase.SupportsCrossPlatformRichTextPipeline's default
		/// false plus Renderer.ProcessSimpleTextBox's OS check - ImageWriter never overrides that
		/// flag, so Windows keeps using DrawTextRun's original Win32 HDC/Uniscribe path
		/// unaffected). Reuses PDFWriter's ShapedFontCache/ShapedTextWrapper/ShapedTextMetrics
		/// infrastructure for wrapping/measurement - only the actual glyph drawing differs
		/// (Graphics.DrawText's SKCanvas.DrawText call instead of PDF Tj operators).
		/// </summary>
		internal override void DrawWrappedText(RectangleF textPosition, PointF offset, string text, ITextRunProps style, RPLFormat.TextAlignments alignment)
		{
			if (string.IsNullOrEmpty(text) || textPosition.Width <= 0f || textPosition.Height <= 0f)
			{
				return;
			}
			int dpi = m_commonGraphics.DpiX;
			float fontSizePixels = style.FontSize * dpi / 72f;
			SkiaCachedFont skiaFont = ShapedFontCache.GetFont(style.FontFamily, fontSizePixels, style.Bold, style.Italic);
			float maxWidthPixels = ConvertToPixels(textPosition.Width);
			List<string> lines = ShapedTextWrapper.Wrap(text, style.FontFamily, fontSizePixels, style.Bold, style.Italic, maxWidthPixels, ShapedFontCache);
			float lineHeightPixels = fontSizePixels * 1.2f;
			float boxLeftPixels = ConvertToPixels(textPosition.X + offset.X);
			float boxTopPixels = ConvertToPixels(textPosition.Y + offset.Y);
			float ascentPixels = skiaFont.GetAscent();
			for (int i = 0; i < lines.Count; i++)
			{
				float lineWidthPixels = ShapedTextMetrics.MeasureTotalWidthPoints(lines[i], style.FontFamily, fontSizePixels, style.Bold, style.Italic, ShapedFontCache);
				float lineX = ComputeLineStartX(alignment, boxLeftPixels, maxWidthPixels, lineWidthPixels);
				float baselineY = boxTopPixels + ascentPixels + i * lineHeightPixels;
				m_graphics.DrawText(lines[i], skiaFont, style.Color, lineX, baselineY);
			}
		}

		/// <summary>Multi-run counterpart to <see cref="DrawWrappedText"/> - see its remarks.</summary>
		internal override void DrawWrappedRichText(RectangleF textPosition, PointF offset, List<(RPLFormat.TextAlignments Alignment, List<(string Text, ITextRunProps Style)> Runs)> paragraphs)
		{
			if (paragraphs == null || paragraphs.Count == 0 || textPosition.Width <= 0f || textPosition.Height <= 0f)
			{
				return;
			}
			int dpi = m_commonGraphics.DpiX;
			float maxWidthPixels = ConvertToPixels(textPosition.Width);
			float maxFontSizePixels = 1f;
			foreach ((RPLFormat.TextAlignments _, List<(string Text, ITextRunProps Style)> paragraphRuns) in paragraphs)
			{
				foreach ((string _, ITextRunProps style) in paragraphRuns)
				{
					float sizePixels = style.FontSize * dpi / 72f;
					if (sizePixels > maxFontSizePixels)
					{
						maxFontSizePixels = sizePixels;
					}
				}
			}
			float lineHeightPixels = maxFontSizePixels * 1.2f;
			float boxLeftPixels = ConvertToPixels(textPosition.X + offset.X);
			float boxTopPixels = ConvertToPixels(textPosition.Y + offset.Y);
			float ascentPixels = maxFontSizePixels * 0.8f;
			float currentBaselineY = boxTopPixels + ascentPixels;
			float previousLineX = 0f;
			bool firstLineOverall = true;

			foreach ((RPLFormat.TextAlignments alignment, List<(string Text, ITextRunProps Style)> paragraphRuns) in paragraphs)
			{
				var pixelRuns = new List<(string Text, ITextRunProps Style)>(paragraphRuns.Count);
				foreach ((string runText, ITextRunProps style) in paragraphRuns)
				{
					pixelRuns.Add((runText, new PixelScaledTextRunProps(style, dpi)));
				}
				List<List<StyledLineFragment>> wrappedLines = ShapedStyledTextWrapper.WrapParagraph(pixelRuns, maxWidthPixels, ShapedFontCache);
				foreach (List<StyledLineFragment> line in wrappedLines)
				{
					float lineWidthPixels = 0f;
					foreach (StyledLineFragment lineFragment in line)
					{
						lineWidthPixels += ShapedTextMetrics.MeasureTotalWidthPoints(lineFragment.Text, lineFragment.Style.FontFamily, lineFragment.Style.FontSize, lineFragment.Style.Bold, lineFragment.Style.Italic, ShapedFontCache);
					}
					float lineX = ComputeLineStartX(alignment, boxLeftPixels, maxWidthPixels, lineWidthPixels);

					if (!firstLineOverall)
					{
						currentBaselineY += lineHeightPixels;
					}
					firstLineOverall = false;
					previousLineX = lineX;

					float currentX = lineX;
					foreach (StyledLineFragment fragment in line)
					{
						SkiaCachedFont fragmentSkiaFont = ShapedFontCache.GetFont(fragment.Style.FontFamily, fragment.Style.FontSize, fragment.Style.Bold, fragment.Style.Italic);
						m_graphics.DrawText(fragment.Text, fragmentSkiaFont, fragment.Style.Color, currentX, currentBaselineY);
						currentX += ShapedTextMetrics.MeasureTotalWidthPoints(fragment.Text, fragment.Style.FontFamily, fragment.Style.FontSize, fragment.Style.Bold, fragment.Style.Italic, ShapedFontCache);
					}
				}
			}
		}

		private static float ComputeLineStartX(RPLFormat.TextAlignments alignment, float boxLeftPixels, float boxWidthPixels, float lineWidthPixels)
		{
			switch (alignment)
			{
			case RPLFormat.TextAlignments.Center:
				return boxLeftPixels + Math.Max(0f, (boxWidthPixels - lineWidthPixels) / 2f);
			case RPLFormat.TextAlignments.Right:
				return boxLeftPixels + Math.Max(0f, boxWidthPixels - lineWidthPixels);
			default:
				return boxLeftPixels;
			}
		}

		/// <summary>
		/// Adapts an ITextRunProps so FontSize reads in device pixels (dpi/72 * points)
		/// instead of RDL points - ShapedStyledTextWrapper/ShapedTextMetrics read FontSize
		/// directly off the style object with no seam to pass a separately-scaled size, unlike
		/// DrawWrappedText's single-style path (which calls ShapedTextWrapper.Wrap with an
		/// explicit fontSizePixels parameter instead).
		/// </summary>
		private sealed class PixelScaledTextRunProps : ITextRunProps
		{
			private readonly ITextRunProps m_inner;
			private readonly float m_dpi;

			internal PixelScaledTextRunProps(ITextRunProps inner, float dpi)
			{
				m_inner = inner;
				m_dpi = dpi;
			}

			public string FontFamily => m_inner.FontFamily;
			public float FontSize => m_inner.FontSize * m_dpi / 72f;
			public Color Color => m_inner.Color;
			public bool Bold => m_inner.Bold;
			public bool Italic => m_inner.Italic;
			public RPLFormat.TextDecorations TextDecoration => m_inner.TextDecoration;
			public int IndexInParagraph => m_inner.IndexInParagraph;
			public string FontKey { get => m_inner.FontKey; set => m_inner.FontKey = value; }
			public void AddSplitIndex(int index) => m_inner.AddSplitIndex(index);
		}

		internal override void EndPage()
		{
			m_graphics.ReleaseCachedHdc(releaseHdc: true);
			m_graphics.Save(m_outputStream, OutputFormat);
		}

		internal override void EndReport()
		{
			m_graphics.EndReport(OutputFormat);
			m_outputStream.Flush();
		}

		internal override void FillPolygon(Color color, PointF[] polygon)
		{
			if (OperatingSystem.IsWindows())
			{
				m_graphics.FillPolygon(GDIBrush.GetBrush(m_brushes, color), polygon);
			}
			else
			{
				m_graphics.FillPolygon(color, polygon);
			}
		}

		internal override void FillRectangle(Color color, RectangleF rectangle)
		{
			if (OperatingSystem.IsWindows())
			{
				m_graphics.FillRectangle(GDIBrush.GetBrush(m_brushes, color), rectangle);
			}
			else
			{
				m_graphics.FillRectangle(color, rectangle);
			}
		}

		private bool GetImage(string imageName, byte[] imageBytes, long imageDataOffset, bool dynamicImage, out PortableImage image)
		{
			image = null;
			if (dynamicImage || string.IsNullOrEmpty(imageName) || !m_cachedImages.TryGetValue(imageName, out image))
			{
				if (!SharedRenderer.GetImage(m_renderer.RplReport, ref imageBytes, imageDataOffset))
				{
					return false;
				}
				try
				{
					image = PortableImage.FromStream(new MemoryStream(imageBytes));
				}
				catch
				{
					return false;
				}
				AddImageToCache(image, dynamicImage, imageName);
			}
			if (!dynamicImage)
			{
				return !string.IsNullOrEmpty(imageName);
			}
			return false;
		}

		private bool GetImage(string imageName, Stream imageStream, long imageDataOffset, bool dynamicImage, out PortableImage image)
		{
			image = null;
			if (dynamicImage || string.IsNullOrEmpty(imageName) || !m_cachedImages.TryGetValue(imageName, out image))
			{
				if (imageStream == null)
				{
					imageStream = SharedRenderer.GetEmbeddedImageStream(m_renderer.RplReport, imageDataOffset, base.CreateAndRegisterStream, imageName);
					if (imageStream == null)
					{
						return false;
					}
				}
				if (imageStream.Position != 0L && imageStream.CanSeek)
				{
					imageStream.Position = 0L;
				}
				try
				{
					image = PortableImage.FromStream(imageStream);
				}
				catch
				{
					return false;
				}
				AddImageToCache(image, dynamicImage, imageName);
			}
			if (!dynamicImage)
			{
				return !string.IsNullOrEmpty(imageName);
			}
			return false;
		}

		private void AddImageToCache(PortableImage image, bool dynamicImage, string imageName)
		{
			SetResolution(image, dynamicImage);
			if (!dynamicImage && !string.IsNullOrEmpty(imageName))
			{
				m_cachedImages.Add(imageName, image);
			}
		}

		private void SetResolution(PortableImage image, bool dynamicImage)
		{
			int num = m_dpiX;
			int num2 = m_dpiY;
			if (dynamicImage)
			{
				if (DEFAULT_RESOLUTION_X == num)
				{
					num = DYNAMIC_IMAGE_MIN_RESOLUTION_X;
				}
				if (DEFAULT_RESOLUTION_Y == num2)
				{
					num2 = DYNAMIC_IMAGE_MIN_RESOLUTION_Y;
				}
			}
			image.SetResolution(num, num2);
		}

		internal override void ClipTextboxRectangle(Win32DCSafeHandle hdc, RectangleF position)
		{
			if (m_bodyRect.X != 0 || m_bodyRect.Y != 0)
			{
				if (!Microsoft.ReportingServices.Rendering.RichText.Win32.GetViewportOrgEx(hdc, out m_prevViewportOrg))
				{
					int lastWin32Error = Marshal.GetLastWin32Error();
					throw new Exception(string.Format(CultureInfo.InvariantCulture, ImageRendererRes.Win32ErrorInfo, "GetViewportOrgEx", lastWin32Error));
				}
				if (!Microsoft.ReportingServices.Rendering.RichText.Win32.SetViewportOrgEx(hdc, m_bodyRect.X, m_bodyRect.Y, Win32ObjectSafeHandle.Zero))
				{
					int lastWin32Error2 = Marshal.GetLastWin32Error();
					throw new Exception(string.Format(CultureInfo.InvariantCulture, ImageRendererRes.Win32ErrorInfo, "SetViewportOrgEx", lastWin32Error2));
				}
			}
			System.Drawing.Rectangle rectangle = new System.Drawing.Rectangle(SharedRenderer.ConvertToPixels(position.X, m_commonGraphics.DpiX), SharedRenderer.ConvertToPixels(position.Y, m_commonGraphics.DpiY), SharedRenderer.ConvertToPixels(position.Width, m_commonGraphics.DpiX), SharedRenderer.ConvertToPixels(position.Height, m_commonGraphics.DpiY));
			if (position.X < 0f)
			{
				rectangle.Width += rectangle.X;
				rectangle.X = 0;
			}
			if (position.Y < 0f)
			{
				rectangle.Height += rectangle.Y;
				rectangle.Y = 0;
			}
			rectangle.X += m_bodyRect.X;
			rectangle.Y += m_bodyRect.Y;
			if (rectangle.Right > m_bodyRect.Right)
			{
				rectangle.Width = m_bodyRect.Right - rectangle.Left;
			}
			if (rectangle.Bottom > m_bodyRect.Bottom)
			{
				rectangle.Height = m_bodyRect.Bottom - rectangle.Top;
			}
			Win32ObjectSafeHandle win32ObjectSafeHandle = Microsoft.ReportingServices.Rendering.RichText.Win32.CreateRectRgn(rectangle.X, rectangle.Y, rectangle.Right, rectangle.Bottom);
			if (win32ObjectSafeHandle.IsInvalid)
			{
				return;
			}
			try
			{
				if (Microsoft.ReportingServices.Rendering.RichText.Win32.SelectClipRgn(hdc, win32ObjectSafeHandle) == 0)
				{
					int lastWin32Error3 = Marshal.GetLastWin32Error();
					throw new Exception(string.Format(CultureInfo.InvariantCulture, ImageRendererRes.Win32ErrorInfo, "SelectClipRgn", lastWin32Error3));
				}
			}
			finally
			{
				win32ObjectSafeHandle.Close();
			}
		}

		internal override void UnClipTextboxRectangle(Win32DCSafeHandle hdc)
		{
			if (Microsoft.ReportingServices.Rendering.RichText.Win32.SelectClipRgn(hdc, Win32ObjectSafeHandle.Zero) == 0)
			{
				int lastWin32Error = Marshal.GetLastWin32Error();
				throw new Exception(string.Format(CultureInfo.InvariantCulture, ImageRendererRes.Win32ErrorInfo, "SelectClipRgn", lastWin32Error));
			}
			if ((m_bodyRect.X != 0 || m_bodyRect.Y != 0) && !Microsoft.ReportingServices.Rendering.RichText.Win32.SetViewportOrgEx(hdc, m_prevViewportOrg.x, m_prevViewportOrg.y, Win32ObjectSafeHandle.Zero))
			{
				int lastWin32Error2 = Marshal.GetLastWin32Error();
				throw new Exception(string.Format(CultureInfo.InvariantCulture, ImageRendererRes.Win32ErrorInfo, "SetViewportOrgEx", lastWin32Error2));
			}
		}

		internal static void GetScreenDpi(out int dpiX, out int dpiY)
		{
			// This queries the *screen's* DPI via a raw Win32 HDC (GetDeviceCaps) - meaningless
			// in a headless/server-side rendering context to begin with, and GDI+ cannot
			// construct any System.Drawing object at all on Linux under .NET 10, even with
			// libgdiplus installed (docs/platform-support.md's Phase 0 finding). Default to
			// 96 DPI on non-Windows - the same "no scaling needed" value DEFAULT_RESOLUTION_X/Y
			// already uses, and exactly what this method returns on a standard-DPI Windows
			// display anyway.
			if (!OperatingSystem.IsWindows())
			{
				dpiX = 96;
				dpiY = 96;
				return;
			}
			using (Bitmap image = new Bitmap(2, 2))
			{
				using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(image))
				{
					IntPtr hdc = graphics.GetHdc();
					try
					{
						int deviceCaps = Microsoft.ReportingServices.Rendering.RichText.Win32.GetDeviceCaps(hdc, 88);
						int deviceCaps2 = Microsoft.ReportingServices.Rendering.RichText.Win32.GetDeviceCaps(hdc, 90);
						int deviceCaps3 = Microsoft.ReportingServices.Rendering.RichText.Win32.GetDeviceCaps(hdc, 8);
						int deviceCaps4 = Microsoft.ReportingServices.Rendering.RichText.Win32.GetDeviceCaps(hdc, 10);
						int deviceCaps5 = Microsoft.ReportingServices.Rendering.RichText.Win32.GetDeviceCaps(hdc, 118);
						int deviceCaps6 = Microsoft.ReportingServices.Rendering.RichText.Win32.GetDeviceCaps(hdc, 117);
						dpiX = (int)Math.Floor(1.0 * (double)deviceCaps * (double)deviceCaps5 / (double)deviceCaps3);
						dpiY = (int)Math.Floor(1.0 * (double)deviceCaps2 * (double)deviceCaps6 / (double)deviceCaps4);
					}
					finally
					{
						graphics.ReleaseHdc(hdc);
					}
				}
			}
		}
	}
}
