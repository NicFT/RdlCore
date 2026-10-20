using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Numerics;
using Microsoft.Reporting.Chart.WebForms.Rendering;
using Microsoft.Reporting.Rendering;
using Microsoft.Reporting.Chart.WebForms.Rendering.Skia;
using SkiaSharp;

namespace Microsoft.Reporting.Chart.WebForms
{
	/// <summary>
	/// Spike prototype (tasks/chart-cross-platform-implementation.md Phase 0) —
	/// SkiaSharp-backed sibling of <see cref="GdiGraphics"/>. Only implements the
	/// Milestone A3 interface-typed (<c>Rendering.*</c>) subset of
	/// <see cref="IChartRenderingEngine"/> for real; the GDI+-typed members (needed to
	/// satisfy the interface, since <see cref="ChartGraphics"/> still calls them directly —
	/// see Milestone B1b blocker notes) are intentionally unreachable stubs. A production
	/// backend can't ship until B1b/B2/C1-C8 stop routing through the GDI+-typed overloads
	/// at all (see spike report).
	/// </summary>
	internal sealed class SkiaChartGraphics : IChartRenderingEngine
	{
		private SKCanvas canvas;

		internal SKCanvas Canvas
		{
			get => canvas;
			set => canvas = value;
		}

		private static SKPaint Native(IPen pen) => ((SkiaPen)pen).NativePaint;

		private static SKPaint Native(IBrush brush) => brush switch
		{
			SkiaSolidBrush b => b.NativePaint,
			SkiaTextureBrush b => b.NativePaint,
			SkiaLinearGradientBrush b => b.NativePaint,
			SkiaPathGradientBrush b => b.NativePaint,
			SkiaHatchBrush b => b.NativePaint,
			_ => throw new NotSupportedException($"Spike scope: unrecognized IBrush implementation: {brush.GetType()}"),
		};

		// Scaled to the bound surface's DPI, matching GDI+'s point-to-pixel conversion at the
		// target bitmap's resolution (see SkiaChartFont.GetNativeFont).
		private SKFont Native(IChartFont font) => ((SkiaChartFont)font).GetNativeFont(dpiX);

		private static SKPath Native(IGraphicsPath path) => ((SkiaGraphicsPath)path).NativePath;

		// --- Milestone A3 interface-typed members: the actual spike surface ---

		public void DrawLine(IPen pen, PointF pt1, PointF pt2) =>
			canvas.DrawLine(SkiaConvert.ToSKPoint(pt1), SkiaConvert.ToSKPoint(pt2), Native(pen));

		public void DrawLine(IPen pen, float x1, float y1, float x2, float y2) => canvas.DrawLine(x1, y1, x2, y2, Native(pen));

		public void DrawRectangle(IPen pen, int x, int y, int width, int height) => canvas.DrawRect(new SKRect(x, y, x + width, y + height), Native(pen));

		public void DrawRectangle(IPen pen, float x, float y, float width, float height) => canvas.DrawRect(new SKRect(x, y, x + width, y + height), Native(pen));

		public void DrawEllipse(IPen pen, float x, float y, float width, float height) => canvas.DrawOval(new SKRect(x, y, x + width, y + height), Native(pen));

		public void DrawEllipse(IPen pen, RectangleF rect) => canvas.DrawOval(SkiaConvert.ToSKRect(rect), Native(pen));

		public void DrawPolygon(IPen pen, PointF[] points)
		{
			using var path = ToClosedSKPath(points);
			canvas.DrawPath(path, Native(pen));
		}

		public void DrawPath(IPen pen, IGraphicsPath path) => canvas.DrawPath(Native(path), Native(pen));

		public void FillEllipse(IBrush brush, RectangleF rect) => canvas.DrawOval(SkiaConvert.ToSKRect(rect), Native(brush));

		public void FillPath(IBrush brush, IGraphicsPath path) => canvas.DrawPath(Native(path), Native(brush));

		public void FillRectangle(IBrush brush, RectangleF rect) => canvas.DrawRect(SkiaConvert.ToSKRect(rect), Native(brush));

		public void FillRectangle(IBrush brush, float x, float y, float width, float height) => canvas.DrawRect(new SKRect(x, y, x + width, y + height), Native(brush));

		public void FillPolygon(IBrush brush, PointF[] points)
		{
			using var path = ToClosedSKPath(points);
			canvas.DrawPath(path, Native(brush));
		}

		public void DrawString(string s, IChartFont font, IBrush brush, RectangleF layoutRectangle, ITextFormat format) =>
			DrawAlignedString(s, Native(font), Native(brush), layoutRectangle, format);

		public void DrawString(string s, IChartFont font, IBrush brush, PointF point, ITextFormat format) =>
			DrawAlignedString(s, Native(font), Native(brush), new RectangleF(point.X, point.Y, 0, 0), format);

		public SizeF MeasureString(string text, IChartFont font, SizeF layoutArea, ITextFormat stringFormat) => MeasureString(text, font);

		public SizeF MeasureString(string text, IChartFont font)
		{
			var nativeFont = Native(font);
			var width = nativeFont.MeasureText(text);
			nativeFont.GetFontMetrics(out var metrics);
			return new SizeF(width, metrics.Descent - metrics.Ascent);
		}

		public SizeF MeasureString(string text, IChartFont font, SizeF layoutArea, ITextFormat stringFormat, out int charactersFitted, out int linesFilled)
		{
			charactersFitted = text.Length;
			linesFilled = 1;
			return MeasureString(text, font);
		}

		public IClipRegion GetClipRegion() => new SkiaClipRegion();

		public void SetClipRegion(IClipRegion region)
		{
			// Spike scope: the sample scene never clips (see SkiaClipRegion); no-op.
		}

		/// <summary>
		/// Real (Milestone E2, 2026-07-23) — genuinely reachable (e.g. <c>ChartGraphics.DrawStringAbs</c>'s
		/// rotated-text path: <c>myMatrix = base.GetTransform().RotateAt(angle, absPosition); ...;
		/// base.SetTransform(myMatrix);</c>). Was a no-op pair on the theory the spike scene never rotates;
		/// real rotated-label/title scenes (<c>RotatedLabelsChart</c>) need this to actually apply. Backed by
		/// <see cref="SKCanvas.TotalMatrix"/>/<see cref="SKCanvas.SetMatrix"/>, same as the <see cref="Transform"/>
		/// property above (that property round-trips through GDI+'s concrete <c>Matrix</c> for a different,
		/// non-interface-typed set of callers; this pair is the Matrix3x2-typed sibling for interface-typed ones).
		/// </summary>
		public Matrix3x2 GetTransform()
		{
			SKMatrix m = canvas.TotalMatrix;
			return new Matrix3x2(m.ScaleX, m.SkewY, m.SkewX, m.ScaleY, m.TransX, m.TransY);
		}

		public void SetTransform(Matrix3x2 matrix) =>
			canvas.SetMatrix(new SKMatrix(matrix.M11, matrix.M21, matrix.M31, matrix.M12, matrix.M22, matrix.M32, 0, 0, 1));

		/// <summary>
		/// Real (Milestone E2, 2026-07-23), documented approximation for the <c>DirectionVertical</c> branch —
		/// genuinely reachable via <c>Title</c>/<c>Axis</c>'s <c>TextOrientation.Rotated90</c> path, which (on
		/// GDI+) sets <see cref="StringFormatFlags.DirectionVertical"/> rather than rotating a transform (only
		/// <c>Rotated270</c> additionally rotates 180° on top of it — see <c>Title.Paint</c>). This backend has
		/// no vertical-text-layout primitive, so it approximates the flag as "rotate this whole string 90°
		/// about its layout rectangle's center" instead of GDI+'s true per-glyph vertical stacking — visually
		/// close for the single-line Latin-text case every real caller here uses, not a faithful port of
		/// GDI+'s general vertical-text-layout engine.
		/// </summary>
		private void DrawAlignedString(string s, SKFont font, SKPaint paint, RectangleF layoutRectangle, ITextFormat format)
		{
			bool vertical = format != null && (format.FormatFlags & StringFormatFlags.DirectionVertical) != 0;
			RectangleF effectiveRect = layoutRectangle;
			float centerX = layoutRectangle.X + layoutRectangle.Width / 2f;
			float centerY = layoutRectangle.Y + layoutRectangle.Height / 2f;
			if (vertical)
			{
				effectiveRect = new RectangleF(centerX - layoutRectangle.Height / 2f, centerY - layoutRectangle.Width / 2f, layoutRectangle.Height, layoutRectangle.Width);
			}

			var width = font.MeasureText(s);
			font.GetFontMetrics(out var metrics);
			var textHeight = metrics.Descent - metrics.Ascent;

			var alignment = format?.Alignment ?? StringAlignment.Near;
			var lineAlignment = format?.LineAlignment ?? StringAlignment.Near;

			float x = alignment switch
			{
				StringAlignment.Center => effectiveRect.X + (effectiveRect.Width - width) / 2f,
				StringAlignment.Far => effectiveRect.X + effectiveRect.Width - width,
				_ => effectiveRect.X,
			};

			float top = lineAlignment switch
			{
				StringAlignment.Center => effectiveRect.Y + (effectiveRect.Height - textHeight) / 2f,
				StringAlignment.Far => effectiveRect.Y + effectiveRect.Height - textHeight,
				_ => effectiveRect.Y,
			};

			var baselineY = top - metrics.Ascent;

			if (vertical)
			{
				canvas.Save();
				canvas.RotateDegrees(90, centerX, centerY);
			}
			canvas.DrawText(s, x, baselineY, SKTextAlign.Left, font, paint);
			if (vertical)
			{
				canvas.Restore();
			}
		}

		private static SKPath ToClosedSKPath(PointF[] points)
		{
			var path = new SKPath();
			path.AddPoly(Array.ConvertAll(points, SkiaConvert.ToSKPoint), close: true);
			return path;
		}

		// --- The GDI+-typed members immediately below are intentionally unreachable in the
		// spike: ChartGraphics still allocates concrete GDI+ objects (new Pen/SolidBrush/Font/
		// GraphicsPath, see ChartGraphics.cs fields + the B1b blocker notes in
		// chart-gdi-type-abstraction.md) and calls the GDI+-typed IChartRenderingEngine
		// overloads, not the interface-typed ones above. A real backend can only retire these
		// once B1b/B2/C1-C8 land. The interface-typed (Rendering.*) overloads further down this
		// block (DrawImage(IChartImage,...)/DrawPie/DrawArc/DrawLines/FillPie/FillRegion/
		// DrawCurve(IPen,...)) are genuinely reachable from real E1-converted call paths and are
		// implemented for real (E1, 2026-07-22) rather than left as NotReachable() stubs.

		private static NotSupportedException NotReachable([System.Runtime.CompilerServices.CallerMemberName] string member = "") =>
			new($"{member}: unreachable in the spike — ChartGraphics still calls the GDI+-typed IChartRenderingEngine surface " +
				"directly (see B1b blocker in chart-gdi-type-abstraction.md); only the Rendering.*-typed overloads are implemented here.");

		/// <summary>
		/// Real (Milestone E2, 2026-07-23) — genuinely reachable (e.g. rotated-label/title painting in
		/// <c>ChartGraphics.cs</c>, which reads/writes <c>base.Transform</c> directly around a rotation).
		/// <see cref="System.Drawing.Drawing2D.Matrix"/> is pure math (no live GDI+ device context needed
		/// to construct one, unlike <see cref="Graphics"/>/<see cref="GraphicsState"/>), so this can
		/// genuinely round-trip through <see cref="SKCanvas.TotalMatrix"/>/<see cref="SKCanvas.SetMatrix"/>
		/// rather than being a thrown stub.
		/// </summary>
		public Matrix Transform
		{
			get
			{
				SKMatrix m = canvas.TotalMatrix;
				return new Matrix(m.ScaleX, m.SkewY, m.SkewX, m.ScaleY, m.TransX, m.TransY);
			}
			set
			{
				float[] e = value.Elements;
				canvas.SetMatrix(new SKMatrix(e[0], e[2], e[4], e[1], e[3], e[5], 0, 0, 1));
			}
		}

		private SmoothingMode smoothingMode;

		/// <summary>
		/// Real (Milestone E2, 2026-07-23) — genuinely reachable from the pervasive
		/// save-set-restore idiom throughout <c>ChartGraphics.cs</c> (e.g. <c>FillRectangleAbsResource</c>:
		/// <c>var s = base.SmoothingMode; base.SmoothingMode = X; ...; base.SmoothingMode = s;</c>). GDI+'s
		/// <see cref="System.Drawing.Drawing2D.SmoothingMode"/> has no Skia equivalent (antialiasing is
		/// controlled per-<see cref="SkiaSharp.SKPaint"/> in the Skia adapters, always on), so this just
		/// stores the value rather than acting on it — a plain property, not a thrown stub.
		/// </summary>
		public SmoothingMode SmoothingMode { get => smoothingMode; set => smoothingMode = value; }

		private TextRenderingHint textRenderingHint;

		/// <summary>
		/// Real (Milestone E2, 2026-07-23) — genuinely reachable from <c>ChartPicture.PaintCore</c>'s
		/// unconditional <c>chartGraph.TextRenderingHint = GetTextRenderingHint();</c>, for every backend.
		/// GDI+'s <see cref="System.Drawing.Text.TextRenderingHint"/> has no Skia equivalent (text
		/// antialiasing/hinting is controlled per-<see cref="SkiaSharp.SKFont"/> in the Skia adapters
		/// instead), so this just stores the value rather than acting on it — a plain property, not a
		/// thrown stub, since there is nothing invalid about setting it.
		/// </summary>
		public TextRenderingHint TextRenderingHint { get => textRenderingHint; set => textRenderingHint = value; }
		public Region Clip { get => throw NotReachable(); set => throw NotReachable(); }
		/// <summary>
		/// Real (Milestone E2, 2026-07-23) get-side only — <see cref="ChartGraphics.AntiAliasing"/>'s setter
		/// (genuinely reachable via <c>ChartPicture.PaintCore</c>) reads this to null-check before syncing
		/// <see cref="System.Drawing.Drawing2D.SmoothingMode"/>, a GDI+-only concept this backend has no
		/// equivalent for (antialiasing is already always-on per-<c>SKPaint</c> in the Skia adapters).
		/// Returning null (rather than throwing) makes that null-check behave the same as "no live Graphics
		/// bound yet" and skip the sync, rather than treating "this backend has none" as an error.
		/// </summary>
		public Graphics Graphics { get => null; set => throw NotReachable(); }
		/// <summary>Real (Milestone E2, 2026-07-23) — genuinely reachable (e.g. <c>AreaChart</c>/<c>RangeChart</c>'s shadow-drawing blocks check this before restoring). <see cref="SKCanvas.LocalClipBounds"/> is Skia's direct equivalent of GDI+'s clip region bookkeeping.</summary>
		public bool IsClipEmpty => canvas.LocalClipBounds.IsEmpty;
		public CompositingQuality CompositingQuality { get => throw NotReachable(); set => throw NotReachable(); }
		public InterpolationMode InterpolationMode { get => throw NotReachable(); set => throw NotReachable(); }
		private float dpiX = 96f;

		/// <summary>Real (Milestone E2, 2026-07-23) — genuinely reachable (e.g. <c>LegendCell.PaintCellSeriesSymbol</c>); returns the DPI captured from the bound <see cref="SkiaRenderSurface"/>.</summary>
		public float GetDpiX() => dpiX;

		/// <summary>Real (Milestone E2, 2026-07-23) — genuinely reachable via <c>ChartRenderingEngine.BindSurface</c> once <see cref="RenderingType.Skia"/> is selected.</summary>
		public void BindSurface(IRenderSurface surface)
		{
			SkiaRenderSurface skiaSurface = (SkiaRenderSurface)surface;
			canvas = skiaSurface.NativeSurface.Canvas;
			dpiX = skiaSurface.Dpi;
		}

		public void DrawLine(Pen pen, PointF pt1, PointF pt2) => throw NotReachable();
		public void DrawLine(Pen pen, float x1, float y1, float x2, float y2) => throw NotReachable();
		public void DrawImage(Image image, Rectangle destRect, int srcX, int srcY, int srcWidth, int srcHeight, GraphicsUnit srcUnit, ImageAttributes imageAttr) => throw NotReachable();
		public void DrawEllipse(Pen pen, float x, float y, float width, float height) => throw NotReachable();
		public void DrawCurve(Pen pen, PointF[] points, int offset, int numberOfSegments, float tension) => throw NotReachable();
		public void DrawRectangle(Pen pen, int x, int y, int width, int height) => throw NotReachable();
		public void DrawPolygon(Pen pen, PointF[] points) => throw NotReachable();
		public void DrawString(string s, Font font, Brush brush, RectangleF layoutRectangle, StringFormat format) => throw NotReachable();
		public void DrawString(string s, Font font, Brush brush, PointF point, StringFormat format) => throw NotReachable();
		public void DrawImage(Image image, Rectangle destRect, float srcX, float srcY, float srcWidth, float srcHeight, GraphicsUnit srcUnit, ImageAttributes imageAttrs) => throw NotReachable();
		public void DrawRectangle(Pen pen, float x, float y, float width, float height) => throw NotReachable();
		public void DrawPath(Pen pen, GraphicsPath path) => throw NotReachable();
		public void DrawPie(Pen pen, float x, float y, float width, float height, float startAngle, float sweepAngle) => throw NotReachable();
		public void DrawArc(Pen pen, float x, float y, float width, float height, float startAngle, float sweepAngle) => throw NotReachable();
		public void DrawImage(Image image, RectangleF rect) => throw NotReachable();
		public void DrawEllipse(Pen pen, RectangleF rect) => throw NotReachable();
		public void DrawLines(Pen pen, PointF[] points) => throw NotReachable();
		public void FillEllipse(Brush brush, RectangleF rect) => throw NotReachable();
		public void FillPath(Brush brush, GraphicsPath path) => throw NotReachable();
		public void FillRegion(Brush brush, Region region) => throw NotReachable();
		public void FillRectangle(Brush brush, RectangleF rect) => throw NotReachable();
		public void FillRectangle(Brush brush, float x, float y, float width, float height) => throw NotReachable();
		public void FillPolygon(Brush brush, PointF[] points) => throw NotReachable();
		public void FillPie(Brush brush, float x, float y, float width, float height, float startAngle, float sweepAngle) => throw NotReachable();
		/// <summary>
		/// Real (Milestone E2, 2026-07-23) — genuinely reachable from real callers that go through
		/// <c>ChartRenderingEngine.MeasureString(string, Font)</c> directly rather than via
		/// <see cref="ChartGraphics.MeasureStringRel"/>/<c>MeasureStringAbs</c>'s already-bridged
		/// <c>IChartFont</c>-typed siblings (e.g. <c>ChartArea.GetCircularLabelsSize</c>). Bridges the
		/// same way, wrapping the concrete <see cref="Font"/> into a <see cref="SkiaChartFont"/> inline —
		/// this backend has no <c>IDrawingResourceFactory</c> reference of its own to call
		/// <c>WrapFont</c> on, so it mirrors <c>SkiaResourceFactory.WrapFont</c>'s construction directly.
		/// </summary>
		public SizeF MeasureString(string text, Font font) => MeasureString(text, WrapFont(font));
		public SizeF MeasureString(string text, Font font, SizeF layoutArea, StringFormat stringFormat) =>
			MeasureString(text, WrapFont(font), layoutArea, WrapTextFormat(stringFormat));
		public SizeF MeasureString(string text, Font font, SizeF layoutArea, StringFormat stringFormat, out int charactersFitted, out int linesFilled) =>
			MeasureString(text, WrapFont(font), layoutArea, WrapTextFormat(stringFormat), out charactersFitted, out linesFilled);

		private static IChartFont WrapFont(Font font) => new SkiaChartFont(font.FontFamily.Name, font.Size, font.Style);

		private static ITextFormat WrapTextFormat(StringFormat stringFormat) => new SkiaTextFormat
		{
			Alignment = stringFormat.Alignment,
			LineAlignment = stringFormat.LineAlignment,
			FormatFlags = stringFormat.FormatFlags,
			Trimming = stringFormat.Trimming,
		};
		/// <summary>
		/// Real (Milestone E2, 2026-07-23) — genuinely reachable via <c>ChartGraphics.cs</c>'s pervasive
		/// <c>var gstate = Save(); ...; Restore(gstate);</c> idiom (every real caller in this codebase
		/// nests these properly — verified by inspection, none skip or reorder a Restore). <see cref="GraphicsState"/>
		/// is a sealed GDI+ type with no public constructor reachable without a live <see cref="Graphics"/>
		/// (which this backend has none of), so unlike <c>Transform</c> above there is no way to hand back
		/// a real one — instead this treats the token as opaque (always null) and pushes/pops
		/// <see cref="SKCanvas"/>'s own save stack directly, which already snapshots matrix + clip together,
		/// the same two things a GDI+ <see cref="GraphicsState"/> captures.
		/// </summary>
		public GraphicsState Save()
		{
			canvas.Save();
			return null;
		}

		public void Restore(GraphicsState gstate) => canvas.Restore();

		/// <summary>
		/// Real (Milestone E2, 2026-07-23) — genuinely reachable (e.g. <c>TreeMapChart</c>/<c>AreaChart</c>/
		/// <c>RangeChart</c>'s plot-area clip bracket: <c>SetClip(rect); ...; ResetClip();</c>, unpaired with
		/// any explicit <c>Save()</c>). Since <see cref="SetClip(RectangleF)"/> below pushes its own
		/// <see cref="SKCanvas.Save"/> frame, popping it back here is equivalent to GDI+'s
		/// <c>Graphics.ResetClip()</c> for every real caller (none nest a second clip inside the bracket
		/// this undoes), not a generic "reset to infinite clip regardless of nesting" — a documented,
		/// narrower approximation of the GDI+ semantics that holds for this codebase's actual call shape.
		/// </summary>
		public void ResetClip() => canvas.Restore();

		/// <summary>See <see cref="ResetClip"/> — paired with it via <see cref="SKCanvas"/>'s save stack rather than a real clip-region diff.</summary>
		public void SetClip(RectangleF rect)
		{
			canvas.Save();
			canvas.ClipRect(SkiaConvert.ToSKRect(rect));
		}

		public void SetClip(GraphicsPath path, CombineMode combineMode) => throw NotReachable();

		/// <summary>Real (Milestone E2, 2026-07-23) — genuinely reachable (e.g. shadow-drawing blocks in <c>AreaChart</c>/<c>RangeChart</c>). <see cref="SKCanvas.Translate(float, float)"/> composes onto the current matrix, matching GDI+'s <c>Graphics.TranslateTransform</c>.</summary>
		public void TranslateTransform(float dx, float dy) => canvas.Translate(dx, dy);
		public void BeginSelection(string hRef, string title) { }
		public void EndSelection() { }
		public void DrawImage(IChartImage image, Rectangle destRect, int srcX, int srcY, int srcWidth, int srcHeight, GraphicsUnit srcUnit, IImageDrawOptions imageAttr) =>
			DrawImageCore(image, destRect, srcX, srcY, srcWidth, srcHeight, imageAttr);
		public void DrawCurve(IPen pen, PointF[] points, int offset, int numberOfSegments, float tension)
		{
			using SKPath path = BuildCurvePath(points, offset, numberOfSegments, tension);
			canvas.DrawPath(path, Native(pen));
		}
		public void DrawImage(IChartImage image, Rectangle destRect, float srcX, float srcY, float srcWidth, float srcHeight, GraphicsUnit srcUnit, IImageDrawOptions imageAttrs) =>
			DrawImageCore(image, destRect, srcX, srcY, srcWidth, srcHeight, imageAttrs);
		public void DrawPie(IPen pen, float x, float y, float width, float height, float startAngle, float sweepAngle) =>
			canvas.DrawArc(new SKRect(x, y, x + width, y + height), startAngle, sweepAngle, useCenter: true, Native(pen));
		public void DrawArc(IPen pen, float x, float y, float width, float height, float startAngle, float sweepAngle) =>
			canvas.DrawArc(new SKRect(x, y, x + width, y + height), startAngle, sweepAngle, useCenter: false, Native(pen));
		public void DrawImage(IChartImage image, RectangleF rect) => canvas.DrawBitmap(((SkiaChartImage)image).NativeBitmap, SkiaConvert.ToSKRect(rect));
		public void DrawLines(IPen pen, PointF[] points)
		{
			using SKPath path = new SKPath();
			path.AddPoly(Array.ConvertAll(points, SkiaConvert.ToSKPoint), close: false);
			canvas.DrawPath(path, Native(pen));
		}
		public void FillRegion(IBrush brush, IClipRegion region)
		{
			using SKPath path = ((SkiaClipRegion)region).ToDrawablePath();
			canvas.DrawPath(path, Native(brush));
		}
		public void FillPie(IBrush brush, float x, float y, float width, float height, float startAngle, float sweepAngle) =>
			canvas.DrawArc(new SKRect(x, y, x + width, y + height), startAngle, sweepAngle, useCenter: true, Native(brush));

		// Mirrors the no-op GDI+-typed SetClip(GraphicsPath, CombineMode) above and SvgChartGraphics's
		// identical no-op — real callers of this overload are the Gauge/Map engines' own separate
		// GDI+-coupled pipelines (out of scope here); wiring SkiaClipRegion into the canvas's own clip
		// stack (Save/Restore parity included) is the still-open follow-up noted on SkiaClipRegion/
		// SetClipRegion above, not something this pass attempts.
		public void SetClip(IGraphicsPath path, CombineMode combineMode) { }

		/// <summary>Shared by both int/float <c>DrawImage(IChartImage,...)</c> overloads — GDI+'s <c>Graphics.DrawImage(Image, Rectangle, float×4, GraphicsUnit, ImageAttributes)</c> equivalent. <paramref name="srcUnit"/> is ignored (every real caller passes <see cref="GraphicsUnit.Pixel"/>).</summary>
		private void DrawImageCore(IChartImage image, RectangleF destRect, float srcX, float srcY, float srcWidth, float srcHeight, IImageDrawOptions imageAttr)
		{
			SKBitmap bitmap = ((SkiaChartImage)image).NativeBitmap;
			SkiaImageDrawOptions options = imageAttr as SkiaImageDrawOptions;
			SKBitmap ownedBitmap = null;
			if (options?.TransparentColor is Color transparentColor)
			{
				bitmap = ownedBitmap = ApplyColorKey(bitmap, SkiaConvert.ToSKColor(transparentColor));
			}
			try
			{
				SKRect srcRect = new SKRect(srcX, srcY, srcX + srcWidth, srcY + srcHeight);
				canvas.DrawBitmap(bitmap, srcRect, SkiaConvert.ToSKRect(destRect));
			}
			finally
			{
				ownedBitmap?.Dispose();
			}
		}

		private static SKBitmap ApplyColorKey(SKBitmap source, SKColor keyColor)
		{
			SKBitmap result = source.Copy();
			for (int y = 0; y < result.Height; y++)
			{
				for (int x = 0; x < result.Width; x++)
				{
					SKColor pixel = result.GetPixel(x, y);
					if (pixel.Red == keyColor.Red && pixel.Green == keyColor.Green && pixel.Blue == keyColor.Blue)
					{
						result.SetPixel(x, y, SKColors.Transparent);
					}
				}
			}
			return result;
		}

		/// <summary>Cardinal-spline-to-Bezier conversion matching GDI+'s <c>Graphics.DrawCurve</c> tension convention (each segment's control points scaled by <paramref name="tension"/>/3) — SkiaSharp has no native cardinal-spline primitive.</summary>
		private static SKPath BuildCurvePath(PointF[] points, int offset, int numberOfSegments, float tension)
		{
			SKPath path = new SKPath();
			path.MoveTo(SkiaConvert.ToSKPoint(points[offset]));
			float t = tension / 3f;
			for (int i = 0; i < numberOfSegments; i++)
			{
				int i0 = offset + i;
				PointF p0 = i0 > 0 ? points[i0 - 1] : points[i0];
				PointF p1 = points[i0];
				PointF p2 = points[i0 + 1];
				PointF p3 = i0 + 2 < points.Length ? points[i0 + 2] : points[i0 + 1];
				SKPoint c1 = new SKPoint(p1.X + (p2.X - p0.X) * t, p1.Y + (p2.Y - p0.Y) * t);
				SKPoint c2 = new SKPoint(p2.X - (p3.X - p1.X) * t, p2.Y - (p3.Y - p1.Y) * t);
				path.CubicTo(c1, c2, SkiaConvert.ToSKPoint(p2));
			}
			return path;
		}
	}
}
