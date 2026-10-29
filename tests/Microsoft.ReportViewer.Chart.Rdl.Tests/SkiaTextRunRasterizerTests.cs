using System;
using System.Drawing;
using Microsoft.ReportingServices.Rendering.RichText;
using Microsoft.ReportingServices.Rendering.RPLProcessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkiaSharp;

namespace Microsoft.ReportViewer.Chart.Rdl.Tests
{
    /// <summary>
    /// Exercises SkiaTextRunRasterizer - the first actual pixel-producing drawing path in
    /// the cross-platform RichText pipeline (tasks/pdf-text-shaping-abstraction.md's "no
    /// actual glyph drawing" gap), the cross-platform counterpart to TextBox.DrawTextRun's
    /// Win32 ScriptTextOut call.
    ///
    /// Uses behavioral (ink-presence) verification rather than a checked-in golden-image
    /// PNG diff, for the same reasons RdlCore.LinuxRenderers.Tests' own
    /// TextRasterAssertions class already documented: AGENTS.md's Testing Philosophy
    /// prefers behavioral tests, and SKTypeface.Default/family resolution is not
    /// guaranteed identical between this Windows dev box and a fontconfig-less Linux CI
    /// image, so a pixel-exact baseline generated here would not be reproducible there.
    /// </summary>
    [TestClass]
    public class SkiaTextRunRasterizerTests
    {
        private const byte InkLuminanceThreshold = 250;

        private sealed class FakeTextRunProps : ITextRunProps
        {
            public string FontFamily { get; set; } = "Arial";
            public float FontSize { get; set; } = 24f;
            public Color Color { get; set; } = Color.Black;
            public bool Bold { get; set; }
            public bool Italic { get; set; }
            public RPLFormat.TextDecorations TextDecoration { get; set; } = RPLFormat.TextDecorations.None;
            public int IndexInParagraph { get; set; }
            public string FontKey { get; set; }

            public void AddSplitIndex(int index)
            {
            }
        }

        private static SKBitmap DrawToBitmap(TextRun run, out float baselineY)
        {
            FontCache fontCache = new FontCache(96f);
            run.ShapeAndPlaceCrossPlatform(fontCache);
            int width = Math.Max(run.GetWidth(Win32DCSafeHandle.Zero, fontCache) + 20, 1);
            int height = Math.Max(run.GetHeight(Win32DCSafeHandle.Zero, fontCache) + 20, 1);
            baselineY = 10f + run.GetAscent(Win32DCSafeHandle.Zero, fontCache);

            SKBitmap bitmap = new SKBitmap(width, height);
            using (SKCanvas canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.White);
                SkiaTextRunRasterizer.Draw(canvas, run, 10f, baselineY);
            }
            return bitmap;
        }

        private static bool HasInk(SKBitmap bitmap, int x0, int x1, int y0, int y1)
        {
            x0 = Math.Max(0, x0);
            x1 = Math.Min(bitmap.Width - 1, x1);
            y0 = Math.Max(0, y0);
            y1 = Math.Min(bitmap.Height - 1, y1);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    SKColor pixel = bitmap.GetPixel(x, y);
                    if (pixel.Red < InkLuminanceThreshold || pixel.Green < InkLuminanceThreshold || pixel.Blue < InkLuminanceThreshold)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        [TestMethod]
        public void Draw_PlainLatinText_ProducesInkSomewhereInTheBitmap()
        {
            TextRun run = new TextRun("Hello", new FakeTextRunProps());
            SKBitmap bitmap = DrawToBitmap(run, out _);

            Assert.IsTrue(HasInk(bitmap, 0, bitmap.Width - 1, 0, bitmap.Height - 1), "Drawing real text should produce some non-white pixel");
        }

        [TestMethod]
        public void Draw_EachCharacter_ProducesInkInItsOwnShapedColumn()
        {
            const string text = "Report";
            TextRun run = new TextRun(text, new FakeTextRunProps());
            FontCache fontCache = new FontCache(96f);
            run.ShapeAndPlaceCrossPlatform(fontCache);
            int[] logicalWidths = run.GetLogicalWidthsCrossPlatform(run.GlyphData);
            int height = run.GetHeight(Win32DCSafeHandle.Zero, fontCache);
            int ascent = run.GetAscent(Win32DCSafeHandle.Zero, fontCache);

            int width = run.GetWidth(Win32DCSafeHandle.Zero, fontCache) + 20;
            SKBitmap bitmap = new SKBitmap(Math.Max(width, 1), Math.Max(height + 20, 1));
            using (SKCanvas canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.White);
                SkiaTextRunRasterizer.Draw(canvas, run, 10f, 10f + ascent);
            }

            int columnX = 10;
            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsWhiteSpace(text[i]))
                {
                    Assert.IsTrue(HasInk(bitmap, columnX - 1, columnX + logicalWidths[i] + 1, 5, height + 15),
                        $"Character '{text[i]}' at column [{columnX},{columnX + logicalWidths[i]}] should have ink");
                }
                columnX += logicalWidths[i];
            }
        }

        [TestMethod]
        public void Draw_EmptyRun_DoesNotThrowAndProducesNoInk()
        {
            TextRun run = new TextRun(string.Empty, new FakeTextRunProps());
            SKBitmap bitmap = DrawToBitmap(run, out _);

            Assert.IsFalse(HasInk(bitmap, 0, bitmap.Width - 1, 0, bitmap.Height - 1), "An empty run should draw nothing");
        }

        [TestMethod]
        public void Draw_UnshapedRun_ThrowsInvalidOperationException()
        {
            TextRun run = new TextRun("Hello", new FakeTextRunProps());
            using SKBitmap bitmap = new SKBitmap(100, 100);
            using SKCanvas canvas = new SKCanvas(bitmap);

            Assert.ThrowsExactly<InvalidOperationException>(() => SkiaTextRunRasterizer.Draw(canvas, run, 0f, 0f));
        }
    }
}
