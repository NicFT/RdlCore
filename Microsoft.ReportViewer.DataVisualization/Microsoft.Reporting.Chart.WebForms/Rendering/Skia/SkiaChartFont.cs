using System.Drawing;
using SkiaSharp;
using Microsoft.Reporting.Rendering;

namespace Microsoft.Reporting.Chart.WebForms.Rendering.Skia
{
	/// <summary>
	/// Spike adapter — wraps an <see cref="SKFont"/>/<see cref="SKTypeface"/> pair behind
	/// <see cref="IChartFont"/>. GDI+ measures font size in points at 96 DPI by convention
	/// (<see cref="GraphicsUnit.Point"/>); Skia's <see cref="SKFont.Size"/> is in raw pixels,
	/// so sizes are converted 1pt = 96/72 px on construction (see spike report — text-metric
	/// parity note).
	/// </summary>
	internal sealed class SkiaChartFont : IChartFont
	{
		private const float PointsToPixels = 96f / 72f;

		internal SKFont NativeFont { get; }

		public string FontFamilyName { get; }

		public float SizeInPoints { get; }

		public FontStyle Style { get; }

		public GraphicsUnit Unit => GraphicsUnit.Point;

		internal SkiaChartFont(string familyName, float sizeInPoints, FontStyle style = FontStyle.Regular)
		{
			FontFamilyName = familyName;
			SizeInPoints = sizeInPoints;
			Style = style;

			var weight = (style & FontStyle.Bold) != 0 ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
			var slant = (style & FontStyle.Italic) != 0 ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
			var typeface = SKTypeface.FromFamilyName(familyName, weight, SKFontStyleWidth.Normal, slant)
				?? SKTypeface.Default;

			NativeFont = new SKFont(typeface, sizeInPoints * PointsToPixels);
		}

		private SKFont scaledFont;

		private float scaledFontDpi;

		/// <summary>
		/// The native font sized for a surface rendered at <paramref name="dpi"/>. GDI+ draws
		/// point-sized fonts at the target bitmap's resolution (<c>Bitmap.SetResolution</c>), so a
		/// chart rendered at e.g. 300 DPI for PDF gets proportionally larger text in pixels;
		/// <see cref="NativeFont"/> alone is fixed at 96 DPI and made chart text ~3x too small there.
		/// </summary>
		internal SKFont GetNativeFont(float dpi)
		{
			if (dpi <= 0f || dpi == 96f)
			{
				return NativeFont;
			}
			if (scaledFont == null || scaledFontDpi != dpi)
			{
				scaledFont?.Dispose();
				scaledFont = new SKFont(NativeFont.Typeface, SizeInPoints * dpi / 72f);
				scaledFontDpi = dpi;
			}
			return scaledFont;
		}

		public void Dispose()
		{
			scaledFont?.Dispose();
			NativeFont.Dispose();
		}
	}
}
