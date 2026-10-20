using System.IO;
using Microsoft.Reporting.Chart.WebForms;
using Microsoft.Reporting.Chart.WebForms.Rendering.Skia;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ReportViewer.DataVisualization.VisualRegressionTests
{
    /// <summary>
    /// Milestone E2 (2026-07-23) — renders real <see cref="Chart"/> object-model scenes (the same
    /// ones <see cref="SampleCharts"/>'s <c>Build*</c> methods feed the GDI+ regression suite)
    /// end to end through the Skia backend, against committed Skia-rendered baselines. Deliberately
    /// bypasses <see cref="ChartImage.GetImage"/>/<see cref="ChartImage.SaveImage"/> (Milestone F's
    /// <c>ChartRenderingBackendFactory</c> is what selects Skia for those production entry points —
    /// see chart-gdi-type-abstraction.md's Milestone F notes) by driving
    /// <c>ChartPicture.Paint(IRenderSurface, bool)</c> directly with a <see cref="SkiaRenderSurface"/>
    /// after selecting <see cref="RenderingType.Skia"/> on the chart's <c>chartGraph</c> — this keeps
    /// every scene here reachable regardless of the platform the test suite actually runs on.
    ///
    /// Originally scoped to 2D scenes only, since the LineChart/AreaChart-family Draw3DSurface/
    /// Draw3DPolygon virtual dispatch chain was still GDI+-typed. Milestone D3-real converted that
    /// chain to IGraphicsPath, and Milestone B1b then closed the one remaining exclusion (PieChart's
    /// 3D label path) — every 3D scene now renders here too (see the 3D scenes below). Gradient/hatch-
    /// heavy scenes render (E1 covers every brush kind for real) but are Skia-vs-its-own-baseline only,
    /// not GDI+-vs-Skia parity — text/gradient rasterization differs between backends regardless of how
    /// faithful the port is.
    /// </summary>
    [TestClass]
    public class SkiaChartRenderingTests
    {
        private static byte[] RenderViaSkia(Chart chart)
        {
            // ChartGraphics.resourceFactory is fixed at construction (readonly), so selecting the
            // Skia backend means swapping the whole ChartGraphics instance, not just
            // ActiveRenderingType — otherwise brush/font/path resources built via the (still-Gdi)
            // resourceFactory would be fed into Skia's IChartRenderingEngine members and throw on
            // the first cross-backend cast.
            chart.chartPicture.chartGraph = new ChartGraphics(chart.chartPicture.common, new SkiaResourceFactory())
            {
                ActiveRenderingType = RenderingType.Skia,
            };
            using var renderSurface = new SkiaRenderSurface(chart.Width, chart.Height, 96f);
            chart.chartPicture.Paint(renderSurface, paintTopLevelElementOnly: false);

            using var stream = new MemoryStream();
            renderSurface.Encode(stream, ChartImageFormat.Png);
            return stream.ToArray();
        }

        private static void AssertMatchesBaseline(Chart chart, string baselineFileName, int maxDiffPixels = 0)
        {
            using (chart)
            {
                var actual = RenderViaSkia(chart);
                var result = ImageComparer.CompareToBaseline(actual, baselineFileName, maxDiffPixels);
                Assert.IsTrue(result.Matches, result.Message);
            }
        }

        [TestMethod]
        public void SimpleBarChart_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildSimpleBarChart(), "SimpleBarChart.Skia.png");

        [TestMethod]
        public void SimpleLineChart_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildSimpleLineChart(), "SimpleLineChart.Skia.png");

        [TestMethod]
        public void RotatedLabelsChart_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildRotatedLabelsChart(), "RotatedLabelsChart.Skia.png");

        [TestMethod]
        public void EmbossBorderChart_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildEmbossBorderChart(), "EmbossBorderChart.Skia.png");

        [TestMethod]
        public void SunkenBorderChart_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildSunkenBorderChart(), "SunkenBorderChart.Skia.png");

        [TestMethod]
        public void AreaChartWithShadow_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildAreaChartWithShadow(), "AreaChartWithShadow.Skia.png");

        [TestMethod]
        public void StockChartWithTriangleMarks_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildStockChartWithTriangleMarks(), "StockChartWithTriangleMarks.Skia.png");

        [TestMethod]
        public void FastPointChartWithMarkers_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildFastPointChartWithMarkers(), "FastPointChartWithMarkers.Skia.png");

        [TestMethod]
        public void FastLineChart_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildFastLineChart(), "FastLineChart.Skia.png");

        [TestMethod]
        public void LineChartWithShadow_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildLineChartWithShadow(), "LineChartWithShadow.Skia.png");

        [TestMethod]
        public void PointChartWithLabels_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildPointChartWithLabels(), "PointChartWithLabels.Skia.png");

        [TestMethod]
        public void BarChartWithLabels_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildBarChartWithLabels(), "BarChartWithLabels.Skia.png");

        [TestMethod]
        public void ErrorBarChartWithLabels_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildErrorBarChartWithLabels(), "ErrorBarChartWithLabels.Skia.png");

        [TestMethod]
        public void BoxPlotChartWithLabels_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildBoxPlotChartWithLabels(), "BoxPlotChartWithLabels.Skia.png");

        [TestMethod]
        public void StockChartWithLabels_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildStockChartWithLabels(), "StockChartWithLabels.Skia.png");

        [TestMethod]
        public void TreeMapChartWithLabels_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildTreeMapChartWithLabels(), "TreeMapChartWithLabels.Skia.png");

        [TestMethod]
        public void RangeChartWithShadow_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildRangeChartWithShadow(), "RangeChartWithShadow.Skia.png");

        [TestMethod]
        public void RangeChartWithHatch_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildRangeChartWithHatch(), "RangeChartWithHatch.Skia.png");

        [TestMethod]
        public void RangeChartWithGradient_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildRangeChartWithGradient(), "RangeChartWithGradient.Skia.png");

        [TestMethod]
        public void CalloutRoundedRectangle_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildCalloutRoundedRectangle(), "CalloutRoundedRectangle.Skia.png");

        [TestMethod]
        public void CalloutEllipse_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildCalloutEllipse(), "CalloutEllipse.Skia.png");

        [TestMethod]
        public void CalloutRectangle_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildCalloutRectangle(), "CalloutRectangle.Skia.png");

        [TestMethod]
        public void CalloutCloud_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildCalloutCloud(), "CalloutCloud.Skia.png");

        [TestMethod]
        public void CalloutPerspective_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildCalloutPerspective(), "CalloutPerspective.Skia.png");

        [TestMethod]
        public void CalloutBorderline_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildCalloutBorderline(), "CalloutBorderline.Skia.png");

        [TestMethod]
        public void CalloutSimpleLine_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildCalloutSimpleLine(), "CalloutSimpleLine.Skia.png");

        [TestMethod]
        public void ChartWithAxisTitles_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildChartWithAxisTitles(), "ChartWithAxisTitles.Skia.png");

        [TestMethod]
        public void TextAnnotationDefault_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildTextAnnotationDefault(), "TextAnnotationDefault.Skia.png");

        [TestMethod]
        public void TextAnnotationFrame_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildTextAnnotationFrame(), "TextAnnotationFrame.Skia.png");

        [TestMethod]
        public void TextAnnotationEmbed_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildTextAnnotationEmbed(), "TextAnnotationEmbed.Skia.png");

        [TestMethod]
        public void TextAnnotationEmboss_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildTextAnnotationEmboss(), "TextAnnotationEmboss.Skia.png");

        [TestMethod]
        public void TextAnnotationShadow_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildTextAnnotationShadow(), "TextAnnotationShadow.Skia.png");

        [TestMethod]
        public void TextAnnotationEllipse_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildTextAnnotationEllipse(), "TextAnnotationEllipse.Skia.png");

        [TestMethod]
        public void RadarChartWithAxisLabels_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildRadarChartWithAxisLabels(), "RadarChartWithAxisLabels.Skia.png");

        [TestMethod]
        public void StripLineWithTitle_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildStripLineWithTitle(), "StripLineWithTitle.Skia.png");

        [TestMethod]
        public void TitleFrame_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildTitleFrame(), "TitleFrame.Skia.png");

        [TestMethod]
        public void TitleEmbed_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildTitleEmbed(), "TitleEmbed.Skia.png");

        [TestMethod]
        public void TitleEmboss_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildTitleEmboss(), "TitleEmboss.Skia.png");

        [TestMethod]
        public void TitleShadow_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildTitleShadow(), "TitleShadow.Skia.png");

        [TestMethod]
        public void LegendWithTitleAndHeader_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildLegendWithTitleAndHeader(), "LegendWithTitleAndHeader.Skia.png");

        // 3D scenes (Milestone D3-real — see chart-gdi-type-abstraction.md): the LineChart/AreaChart-family
        // Draw3DSurface/Draw3DPolygon virtual dispatch chain now compiles against IGraphicsPath, so these
        // scenes (previously excluded from E2) render through Skia for the first time. New Skia-only
        // baselines — no GDI+ byte-for-byte parity expected, same as every other scene in this class.
        [TestMethod]
        public void BarChart3DWithLabels_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildBarChart3DWithLabels(), "BarChart3DWithLabels.Skia.png");

        [TestMethod]
        public void LineChart3D_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildLineChart3D(), "LineChart3D.Skia.png");

        [TestMethod]
        public void Chart3DWithAxisTitles_RendersViaSkia_MatchesBaseline() =>
            // maxDiffPixels: 2 — same rotated-text anti-aliasing nondeterminism documented on
            // RadarChartWithAxisLabels (see ImageComparer.CompareToBaseline's remarks); this scene's
            // axis titles hit the same edge-pixel drift, just on the Skia backend instead of GDI+.
            AssertMatchesBaseline(SampleCharts.BuildChart3DWithAxisTitles(), "Chart3DWithAxisTitles.Skia.png", maxDiffPixels: 2);

        // Pie3D/Doughnut3D (Milestone B1b, closed): PieChart.Draw3DInsideLabels previously called the
        // concrete DrawPointLabelStringRel(Font,Brush,...) overload, hitting SkiaChartGraphics.DrawString's
        // unreachable stub. Bridged to the interface-typed IChartFont/IBrush/ITextFormat overload (same
        // pattern as this file's other 3 DrawPointLabelStringRel call sites), so these two scenes — the
        // only ones D3-real had to leave out — now render through Skia too.
        [TestMethod]
        public void Pie3DChart_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildPie3DChart(), "Pie3DChart.Skia.png");

        [TestMethod]
        public void Doughnut3DChart_RendersViaSkia_MatchesBaseline() =>
            AssertMatchesBaseline(SampleCharts.BuildDoughnut3DChart(), "Doughnut3DChart.Skia.png");

        // A chart Title whose font was never set (or was only described via SetFontDescriptor,
        // as ChartMapper does where GDI+ can't build the RDL font) must measure/draw without
        // ever reading Title.Font - that getter lazily constructs a GDI+ Font, which threw
        // DllNotFoundException(gdiplus.dll) on Linux and made the chart vanish from PDF/Word.
        private static Chart BuildTitledPieChart(out Title title)
        {
            var chart = new Chart { Width = 400, Height = 300 };
            title = chart.Titles.Add("teste");
            chart.ChartAreas.Add("Default");
            var series = chart.Series.Add("Estados");
            series.ChartType = SeriesChartType.Pie;
            series.Points.AddXY("SP", 12);
            series.Points.AddXY("RJ", 7);
            series.Points.AddXY("MG", 5);
            return chart;
        }

        private static object GetTitleFontField(Title title) =>
            typeof(Title).GetField("font", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(title);

        [TestMethod]
        public void TitleWithoutFont_RendersViaSkia_WithoutConstructingGdiFont()
        {
            using var chart = BuildTitledPieChart(out var title);
            var png = RenderViaSkia(chart);
            Assert.IsTrue(png.Length > 0);
            Assert.IsNull(GetTitleFontField(title), "Title.Font getter was read during Skia rendering (would construct a GDI+ Font).");
        }

        [TestMethod]
        public void TitleWithFontDescriptor_RendersViaSkia_UsingDescribedFont()
        {
            using var defaultChart = BuildTitledPieChart(out _);
            var defaultPng = RenderViaSkia(defaultChart);

            using var describedChart = BuildTitledPieChart(out var title);
            title.SetFontDescriptor("Arial", 20f, System.Drawing.FontStyle.Bold);
            var describedPng = RenderViaSkia(describedChart);

            Assert.IsNull(GetTitleFontField(title), "Title.Font getter was read during Skia rendering (would construct a GDI+ Font).");
            CollectionAssert.AreNotEqual(defaultPng, describedPng, "The described 20pt bold font had no effect on the rendered title.");
        }
    }
}
