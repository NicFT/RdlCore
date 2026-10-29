using System;
using SkiaSharp;

namespace Microsoft.ReportingServices.Rendering.RichText
{
	/// <summary>
	/// Cross-platform counterpart to <see cref="TextBox.DrawTextRun"/>'s Win32
	/// <c>ScriptTextOut</c> call: draws an already-shaped <see cref="TextRun"/>'s real
	/// glyphs (from <see cref="TextRun.GlyphData"/>, produced by
	/// <see cref="HarfBuzzTextShaper"/> via <see cref="TextRun.ShapeAndPlaceCrossPlatform"/>)
	/// onto a real <see cref="SKCanvas"/> - the first actual pixel-producing drawing path
	/// in the cross-platform RichText pipeline (tasks/pdf-text-shaping-abstraction.md's
	/// "no actual glyph drawing" gap).
	///
	/// Draws exactly the glyph ids/positions shaping already computed (via
	/// <see cref="SKTextBlobBuilder.AllocatePositionedRun"/>) rather than re-shaping
	/// <see cref="TextRun.Text"/> through Skia's own string-drawing entry points - the
	/// same "draw what was measured" invariant <c>ScriptTextOut</c> itself preserves
	/// (it draws the glyph array <c>ScriptShape</c>/<c>ScriptPlace</c> already produced,
	/// not the original string).
	///
	/// Scope, honestly: this is the per-run glyph-drawing primitive only - it does not
	/// reproduce <see cref="TextBox.RenderParagraph"/>'s surrounding orchestration
	/// (paragraph indent/alignment, underline/strikethrough rectangles, run highlighting,
	/// prefix/bullet drawing). Wiring those in, and deciding whether real
	/// <see cref="TextBox.Render"/> should route through this or `PDFWriter`'s existing
	/// `WriteCompositeText`, is separate follow-up work.
	/// </summary>
	internal static class SkiaTextRunRasterizer
	{
		/// <summary>
		/// Draws <paramref name="run"/>'s shaped glyphs onto <paramref name="canvas"/> at
		/// baseline position (<paramref name="x"/>, <paramref name="baselineY"/>) - the
		/// same origin convention <see cref="TextBox.DrawTextRun"/> uses for its own `x`/
		/// `baselineY` parameters. <paramref name="run"/> must already be shaped (i.e.
		/// <see cref="TextRun.GlyphData"/> and <see cref="TextRun.CachedFont"/> non-null,
		/// with a <see cref="CachedFont.SkiaFont"/>) - normally the result of a prior
		/// <see cref="TextRun.ShapeAndPlaceCrossPlatform"/> call, whether directly (tests)
		/// or via <see cref="TextRun.GetWidth(Win32DCSafeHandle, FontCache)"/>/<see cref="TextRun.GetGlyphData"/>
		/// having already triggered it through the platform-gated <see cref="TextRun.ShapeAndPlace"/>.
		/// </summary>
		internal static void Draw(SKCanvas canvas, TextRun run, float x, float baselineY)
		{
			if (canvas == null)
			{
				throw new ArgumentNullException(nameof(canvas));
			}
			if (run == null)
			{
				throw new ArgumentNullException(nameof(run));
			}
			GlyphData glyphData = run.GlyphData;
			CachedFont cachedFont = run.CachedFont;
			if (glyphData == null || cachedFont?.SkiaFont == null)
			{
				throw new InvalidOperationException("TextRun must already be shaped via ShapeAndPlaceCrossPlatform (with a SkiaCachedFont-backed CachedFont) before it can be rasterized.");
			}
			GlyphShapeData shape = glyphData.GlyphScriptShapeData;
			int glyphCount = shape.GlyphCount;
			if (glyphCount == 0)
			{
				return;
			}
			System.Drawing.Color color = run.TextRunProperties.Color;
			using SKPaint paint = new SKPaint
			{
				Color = new SKColor(color.R, color.G, color.B, color.A),
				IsAntialias = true
			};
			using SKTextBlobBuilder builder = new SKTextBlobBuilder();
			SKPositionedRunBuffer buffer = builder.AllocatePositionedRun(cachedFont.SkiaFont.Font, glyphCount);
			Span<ushort> glyphs = buffer.Glyphs;
			Span<SKPoint> positions = buffer.Positions;
			int[] advances = glyphData.Advances;
			GOFFSET[] offsets = glyphData.GOffsets;
			float penX = x;
			for (int i = 0; i < glyphCount; i++)
			{
				glyphs[i] = unchecked((ushort)shape.Glyphs[i]);
				positions[i] = new SKPoint(penX + offsets[i].du, baselineY - offsets[i].dv);
				penX += advances[i];
			}
			using SKTextBlob blob = builder.Build();
			canvas.DrawText(blob, 0f, 0f, paint);
		}
	}
}
