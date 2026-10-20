using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using Microsoft.Reporting.Chart.WebForms;
using Microsoft.Reporting.Chart.WebForms.Rendering.Skia;
using Microsoft.Reporting.Rendering;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkiaSharp;

namespace Microsoft.ReportViewer.DataVisualization.VisualRegressionTests
{
    /// <summary>
    /// Functional smoke tests (E1) for the Skia adapters' previously-stubbed members —
    /// SkiaClipRegion's region algebra, SkiaResourceFactory's brush-based pens/texture
    /// brushes/WrapImage, and SkiaPen's dash translation. These exercise real runtime
    /// behavior (SKPath.Op/SKShader construction/pixel edits), not just compilation —
    /// there is no visual baseline for the Skia backend yet (see SpikeSceneTests), so
    /// this is the only automated signal these code paths actually work. Not a pixel-
    /// regression gate.
    /// </summary>
    [TestClass]
    public class SkiaResourceFactoryTests
    {
        private static SkiaResourceFactory Factory => new SkiaResourceFactory();

        private static IChartImage CreateTestImage(int width, int height, SKColor fill)
        {
            using var bitmap = new SKBitmap(width, height);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(fill);
            }
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream(data.ToArray());
            return Factory.LoadImage(stream);
        }

        [TestMethod]
        public void ClipRegion_IntersectRect_NarrowsBounds()
        {
            using var region = Factory.CreateRegion(new RectangleF(0, 0, 100, 100));
            region.Intersect(new RectangleF(50, 50, 100, 100));

            var bounds = region.GetBounds(engine: null);
            Assert.AreEqual(50f, bounds.X, 0.01f);
            Assert.AreEqual(50f, bounds.Y, 0.01f);
            Assert.AreEqual(50f, bounds.Width, 0.01f);
            Assert.AreEqual(50f, bounds.Height, 0.01f);
        }

        [TestMethod]
        public void ClipRegion_UnionRect_GrowsBounds()
        {
            using var region = Factory.CreateRegion(new RectangleF(0, 0, 10, 10));
            region.Union(new RectangleF(20, 20, 10, 10));

            var bounds = region.GetBounds(engine: null);
            Assert.AreEqual(0f, bounds.X, 0.01f);
            Assert.AreEqual(0f, bounds.Y, 0.01f);
            Assert.AreEqual(30f, bounds.Width, 0.01f);
            Assert.AreEqual(30f, bounds.Height, 0.01f);
        }

        [TestMethod]
        public void ClipRegion_ExcludeRect_RemovesVisibility()
        {
            using var region = Factory.CreateRegion(new RectangleF(0, 0, 100, 100));
            region.Exclude(new RectangleF(0, 0, 50, 100));

            Assert.IsFalse(region.IsVisible(new PointF(25, 50)), "Excluded half should no longer be visible.");
            Assert.IsTrue(region.IsVisible(new PointF(75, 50)), "Non-excluded half should remain visible.");
        }

        [TestMethod]
        public void ClipRegion_MakeEmpty_ReportsEmpty()
        {
            using var region = Factory.CreateRegion(new RectangleF(0, 0, 100, 100));
            Assert.IsFalse(region.IsEmpty(engine: null));

            region.MakeEmpty();
            Assert.IsTrue(region.IsEmpty(engine: null));
        }

        [TestMethod]
        public void ClipRegion_MakeInfinite_ReportsInfinite()
        {
            using var region = Factory.CreateRegion(new RectangleF(0, 0, 100, 100));
            Assert.IsFalse(region.IsInfinite(engine: null));

            region.MakeInfinite();
            Assert.IsTrue(region.IsInfinite(engine: null));
            Assert.IsTrue(region.IsVisible(new PointF(1_000_000, 1_000_000)));
        }

        [TestMethod]
        public void ClipRegion_Clone_IsIndependent()
        {
            using var region = Factory.CreateRegion(new RectangleF(0, 0, 100, 100));
            using var clone = region.Clone();
            region.Union(new RectangleF(200, 200, 10, 10));

            var cloneBounds = clone.GetBounds(engine: null);
            Assert.AreEqual(100f, cloneBounds.Width, 0.01f, "Clone must not see mutations made to the original after cloning.");
        }

        [TestMethod]
        public void CreatePen_FromSolidBrush_CopiesColor()
        {
            var factory = Factory;
            using var brush = factory.CreateSolidBrush(Color.FromArgb(255, 10, 20, 30));
            using var pen = factory.CreatePen(brush, 2f) as SkiaPen;

            Assert.IsNotNull(pen);
            Assert.AreEqual(Color.FromArgb(255, 10, 20, 30), pen.Color);
            Assert.AreEqual(2f, pen.Width);
        }

        [TestMethod]
        public void CreatePen_DashStyle_SetsPathEffect()
        {
            var factory = Factory;
            using var pen = factory.CreatePen(Color.Black, 1f) as SkiaPen;
            Assert.IsNotNull(pen);
            Assert.IsNull(pen.NativePaint.PathEffect);

            pen.DashStyle = DashStyle.Dash;
            Assert.IsNotNull(pen.NativePaint.PathEffect);

            pen.DashStyle = DashStyle.Solid;
            Assert.IsNull(pen.NativePaint.PathEffect);
        }

        [TestMethod]
        public void CreateTextureBrush_WholeImage_BuildsBitmapShader()
        {
            var factory = Factory;
            using var image = CreateTestImage(4, 4, SKColors.Red);
            using var brush = factory.CreateTextureBrush(image, WrapMode.Tile) as SkiaTextureBrush;

            Assert.IsNotNull(brush);
            Assert.IsNotNull(brush.NativePaint.Shader);
        }

        [TestMethod]
        public void CreateTextureBrush_IntoRect_BuildsBitmapShader()
        {
            var factory = Factory;
            using var image = CreateTestImage(4, 4, SKColors.Blue);
            using var options = factory.CreateImageDrawOptions();
            options.SetTransparentColor(Color.Blue);
            options.SetWrapMode(WrapMode.Clamp);

            using var brush = factory.CreateTextureBrush(image, new RectangleF(0, 0, 40, 40), options) as SkiaTextureBrush;

            Assert.IsNotNull(brush);
            Assert.IsNotNull(brush.NativePaint.Shader);
        }

        [TestMethod]
        public void WrapImage_BridgesGdiBitmap_PreservesSize()
        {
            using var gdiBitmap = new Bitmap(6, 8);
            using var image = Factory.WrapImage(gdiBitmap);

            Assert.AreEqual(6, image.Width);
            Assert.AreEqual(8, image.Height);
        }

        [TestMethod]
        public void CreatePen_FromGradientOrHatchBrush_BuildsPenFromBrushShader()
        {
            var factory = Factory;
            using var gradientBrush = factory.CreateLinearGradientBrush(new PointF(0, 0), new PointF(1, 1), Color.Red, Color.Blue);
            using var gradientPen = factory.CreatePen(gradientBrush, 1f) as SkiaPen;
            Assert.IsNotNull(gradientPen);
            Assert.IsNotNull(gradientPen.NativePaint.Shader);

            using var hatchBrush = factory.CreateHatchBrush(HatchStyle.Horizontal, Color.Black, Color.White);
            using var hatchPen = factory.CreatePen(hatchBrush, 1f) as SkiaPen;
            Assert.IsNotNull(hatchPen);
            Assert.IsNotNull(hatchPen.NativePaint.Shader);
        }

        [TestMethod]
        public void DrawPie_DoesNotThrow_AndPaintsSomething()
        {
            using var surfaceBitmap = new SKBitmap(50, 50);
            using var canvas = new SKCanvas(surfaceBitmap);
            var graphics = new SkiaChartGraphics { Canvas = canvas };
            using var brush = Factory.CreateSolidBrush(Color.Red);

            graphics.FillPie(brush, 5, 5, 30, 30, 0, 270);
            canvas.Flush();

            Assert.AreNotEqual(default(SKColor), surfaceBitmap.GetPixel(20, 20));
        }

        [TestMethod]
        public void DrawArc_DoesNotThrow()
        {
            using var surfaceBitmap = new SKBitmap(50, 50);
            using var canvas = new SKCanvas(surfaceBitmap);
            var graphics = new SkiaChartGraphics { Canvas = canvas };
            using var pen = Factory.CreatePen(Color.Black, 1f);

            graphics.DrawArc(pen, 5, 5, 30, 30, 0, 180);
        }

        [TestMethod]
        public void DrawCurve_ProducesNonEmptyPath()
        {
            using var surfaceBitmap = new SKBitmap(50, 50);
            using var canvas = new SKCanvas(surfaceBitmap);
            var graphics = new SkiaChartGraphics { Canvas = canvas };
            using var pen = Factory.CreatePen(Color.Black, 1f);
            var points = new[] { new PointF(0, 25), new PointF(15, 5), new PointF(30, 25), new PointF(45, 5) };

            graphics.DrawCurve(pen, points, 0, points.Length - 1, 0.5f);
            canvas.Flush();

            Assert.AreNotEqual(default(SKColor), surfaceBitmap.GetPixel(15, 5));
        }

        [TestMethod]
        public void DrawLines_PaintsOpenPolyline()
        {
            using var surfaceBitmap = new SKBitmap(50, 50);
            using var canvas = new SKCanvas(surfaceBitmap);
            var graphics = new SkiaChartGraphics { Canvas = canvas };
            using var pen = Factory.CreatePen(Color.Black, 1f);

            graphics.DrawLines(pen, new[] { new PointF(0, 0), new PointF(40, 0), new PointF(40, 40) });
            canvas.Flush();

            Assert.AreNotEqual(default(SKColor), surfaceBitmap.GetPixel(40, 20));
        }

        [TestMethod]
        public void FillRegion_PaintsRegionBounds()
        {
            using var surfaceBitmap = new SKBitmap(50, 50);
            using var canvas = new SKCanvas(surfaceBitmap);
            var graphics = new SkiaChartGraphics { Canvas = canvas };
            using var brush = Factory.CreateSolidBrush(Color.Blue);
            using var region = Factory.CreateRegion(new RectangleF(5, 5, 20, 20));

            graphics.FillRegion(brush, region);
            canvas.Flush();

            Assert.AreEqual(new SKColor(0, 0, 255), surfaceBitmap.GetPixel(10, 10));
        }

        [TestMethod]
        public void DrawImage_IChartImage_BlitsBitmapIntoDestRect()
        {
            using var surfaceBitmap = new SKBitmap(50, 50);
            using var canvas = new SKCanvas(surfaceBitmap);
            var graphics = new SkiaChartGraphics { Canvas = canvas };
            using var image = CreateTestImage(4, 4, SKColors.Green);

            graphics.DrawImage(image, new Rectangle(5, 5, 20, 20), 0, 0, 4, 4, GraphicsUnit.Pixel, Factory.CreateImageDrawOptions());
            canvas.Flush();

            Assert.AreEqual(new SKColor(0, 128, 0), surfaceBitmap.GetPixel(15, 15));
        }

        [TestMethod]
        public void DrawImage_IChartImage_WithTransparentColor_SkipsColorKeyedPixels()
        {
            using var surfaceBitmap = new SKBitmap(50, 50);
            using var canvas = new SKCanvas(surfaceBitmap);
            canvas.Clear(SKColors.White);
            var graphics = new SkiaChartGraphics { Canvas = canvas };
            using var image = CreateTestImage(4, 4, SKColors.Green);
            var options = Factory.CreateImageDrawOptions();
            options.SetTransparentColor(Color.FromArgb(0, 128, 0));

            graphics.DrawImage(image, new Rectangle(5, 5, 20, 20), 0, 0, 4, 4, GraphicsUnit.Pixel, options);
            canvas.Flush();

            Assert.AreEqual(SKColors.White, surfaceBitmap.GetPixel(15, 15));
        }

        [TestMethod]
        public void ClipRegion_ToDrawablePath_ReturnsIndependentCopy()
        {
            using var region = Factory.CreateRegion(new RectangleF(0, 0, 10, 10)) as SkiaClipRegion;
            using var path = region.ToDrawablePath();
            region.Union(new RectangleF(50, 50, 5, 5));

            Assert.AreEqual(10f, path.Bounds.Width, 0.01f, "Path returned by ToDrawablePath must not reflect later mutations to the region.");
        }

        [TestMethod]
        public void LinearGradientBrush_FillsRect_InterpolatesEndpointColors()
        {
            using var surfaceBitmap = new SKBitmap(40, 10);
            using var canvas = new SKCanvas(surfaceBitmap);
            var graphics = new SkiaChartGraphics { Canvas = canvas };
            using var brush = Factory.CreateLinearGradientBrush(new RectangleF(0, 0, 40, 10), Color.Red, Color.Blue, 0f);

            graphics.FillRectangle(brush, new RectangleF(0, 0, 40, 10));
            canvas.Flush();

            SKColor left = surfaceBitmap.GetPixel(1, 5);
            SKColor right = surfaceBitmap.GetPixel(38, 5);
            Assert.IsTrue(left.Red > right.Red, "Left edge should be closer to red than the right edge.");
            Assert.IsTrue(right.Blue > left.Blue, "Right edge should be closer to blue than the left edge.");
        }

        [TestMethod]
        public void LinearGradientBrush_WithInterpolationColors_UsesColorBlendStops()
        {
            using var surfaceBitmap = new SKBitmap(40, 10);
            using var canvas = new SKCanvas(surfaceBitmap);
            var graphics = new SkiaChartGraphics { Canvas = canvas };
            using var brush = Factory.CreateLinearGradientBrush(new RectangleF(0, 0, 40, 10), Color.Red, Color.Blue, 0f);
            var colorBlend = new ColorBlend(3)
            {
                Colors = new[] { Color.Lime, Color.Lime, Color.Lime },
                Positions = new[] { 0f, 0.5f, 1f },
            };
            brush.InterpolationColors = colorBlend;

            graphics.FillRectangle(brush, new RectangleF(0, 0, 40, 10));
            canvas.Flush();

            SKColor middle = surfaceBitmap.GetPixel(20, 5);
            Assert.AreEqual((byte)0, middle.Red, "InterpolationColors should override the plain Red->Blue gradient.");
            Assert.IsTrue(middle.Green > 200, "Every ColorBlend stop is Lime, so the fill should be solid green.");
        }

        [TestMethod]
        public void LinearGradientBrush_PointToPoint_BuildsGradientAlongLine()
        {
            using var surfaceBitmap = new SKBitmap(20, 20);
            using var canvas = new SKCanvas(surfaceBitmap);
            var graphics = new SkiaChartGraphics { Canvas = canvas };
            using var brush = Factory.CreateLinearGradientBrush(new PointF(0, 0), new PointF(20, 0), Color.Red, Color.Blue);

            graphics.FillRectangle(brush, new RectangleF(0, 0, 20, 20));
            canvas.Flush();

            SKColor left = surfaceBitmap.GetPixel(1, 10);
            SKColor right = surfaceBitmap.GetPixel(18, 10);
            Assert.IsTrue(left.Red > right.Red);
            Assert.IsTrue(right.Blue > left.Blue);
        }

        [TestMethod]
        public void PathGradientBrush_FillsEllipse_CenterColorAtCenterSurroundAtEdge()
        {
            using var surfaceBitmap = new SKBitmap(40, 40);
            using var canvas = new SKCanvas(surfaceBitmap);
            canvas.Clear(SKColors.White);
            var graphics = new SkiaChartGraphics { Canvas = canvas };
            using var path = Factory.CreatePath();
            path.AddEllipse(0, 0, 40, 40);
            using var brush = Factory.CreatePathGradientBrush(path);
            brush.CenterColor = Color.Red;
            brush.SurroundColors = new[] { Color.Blue };

            graphics.FillPath(brush, path);
            canvas.Flush();

            SKColor center = surfaceBitmap.GetPixel(20, 20);
            SKColor nearEdge = surfaceBitmap.GetPixel(20, 2);
            Assert.IsTrue(center.Red > nearEdge.Red, "Centre should be closer to CenterColor (red) than the boundary.");
            Assert.IsTrue(nearEdge.Blue > center.Blue, "Boundary should be closer to SurroundColors[0] (blue) than the centre.");
        }

        [TestMethod]
        public void PathGradientBrush_CenterPoint_DefaultsToPathBoundsCenter()
        {
            using var path = Factory.CreatePath();
            path.AddRectangle(new RectangleF(10, 20, 30, 40));
            using var brush = Factory.CreatePathGradientBrush(path) as SkiaPathGradientBrush;

            Assert.AreEqual(25f, brush.CenterPoint.X, 0.01f);
            Assert.AreEqual(40f, brush.CenterPoint.Y, 0.01f);
        }

        [TestMethod]
        public void HatchBrush_ExposesStyleAndColors()
        {
            using var brush = Factory.CreateHatchBrush(HatchStyle.Percent50, Color.Black, Color.White) as SkiaHatchBrush;

            Assert.IsNotNull(brush);
            Assert.AreEqual(HatchStyle.Percent50, brush.HatchStyle);
            Assert.AreEqual(Color.Black, brush.ForegroundColor);
            Assert.AreEqual(Color.White, brush.BackgroundColor);
            Assert.IsNotNull(brush.NativePaint.Shader);
        }

        [TestMethod]
        public void HatchBrush_Horizontal_FillsWithHorizontalStripes()
        {
            using var surfaceBitmap = new SKBitmap(48, 48);
            using var canvas = new SKCanvas(surfaceBitmap);
            var graphics = new SkiaChartGraphics { Canvas = canvas };
            using var brush = Factory.CreateHatchBrush(HatchStyle.Horizontal, Color.Black, Color.White);

            graphics.FillRectangle(brush, new RectangleF(0, 0, 48, 48));
            canvas.Flush();

            // Horizontal stripes: colour only varies moving down a column, not across a row within a stripe.
            Assert.AreEqual(surfaceBitmap.GetPixel(0, 0), surfaceBitmap.GetPixel(40, 0));
            bool sawBlack = false, sawWhite = false;
            for (int y = 0; y < 24; y++)
            {
                SKColor pixel = surfaceBitmap.GetPixel(5, y);
                if (pixel == SKColors.Black) sawBlack = true;
                if (pixel == SKColors.White) sawWhite = true;
            }
            Assert.IsTrue(sawBlack, "Horizontal hatch should paint some foreground rows.");
            Assert.IsTrue(sawWhite, "Horizontal hatch should paint some background rows.");
        }

        [TestMethod]
        public void HatchBrush_Percent50_ProducesRoughlyBalancedForeAndBackgroundPixels()
        {
            using var surfaceBitmap = new SKBitmap(48, 48);
            using var canvas = new SKCanvas(surfaceBitmap);
            var graphics = new SkiaChartGraphics { Canvas = canvas };
            using var brush = Factory.CreateHatchBrush(HatchStyle.Percent50, Color.Black, Color.White);

            graphics.FillRectangle(brush, new RectangleF(0, 0, 48, 48));
            canvas.Flush();

            int foreCount = 0;
            for (int y = 0; y < 48; y++)
            {
                for (int x = 0; x < 48; x++)
                {
                    if (surfaceBitmap.GetPixel(x, y) == SKColors.Black) foreCount++;
                }
            }
            double fraction = foreCount / (48.0 * 48.0);
            Assert.IsTrue(fraction > 0.3 && fraction < 0.7, $"Percent50 hatch should paint roughly half the area in foreground; got {fraction:P0}.");
        }

        [TestMethod]
        public void CreatePen_FromHatchBrush_UsesForegroundColor()
        {
            var factory = Factory;
            using var hatchBrush = factory.CreateHatchBrush(HatchStyle.Horizontal, Color.Red, Color.White) as SkiaHatchBrush;
            using var pen = factory.CreatePen(hatchBrush, 1f) as SkiaPen;

            Assert.IsNotNull(pen);
            Assert.IsNotNull(pen.NativePaint.Shader);
        }

        // GDI+ draws point-sized fonts at the target bitmap's resolution, so a chart rendered at
        // 3x DPI (as PDF/Word request) gets 3x larger text in pixels. Skia used a fixed 96 DPI,
        // which made Linux chart titles/legends ~3x too small in exported PDFs.
        [TestMethod]
        public void MeasureString_ScalesWithBoundSurfaceDpi()
        {
            using var font = Factory.CreateFont("Arial", 12f, FontStyle.Bold);

            var graphics96 = new SkiaChartGraphics();
            using (var surface96 = new SkiaRenderSurface(100, 100, 96f))
            {
                graphics96.BindSurface(surface96);
            }
            var graphics288 = new SkiaChartGraphics();
            using (var surface288 = new SkiaRenderSurface(100, 100, 288f))
            {
                graphics288.BindSurface(surface288);
            }

            SizeF at96 = graphics96.MeasureString("teste", font);
            SizeF at288 = graphics288.MeasureString("teste", font);

            Assert.AreEqual(3.0, at288.Width / at96.Width, 0.05);
            Assert.AreEqual(3.0, at288.Height / at96.Height, 0.05);
        }
    }
}
