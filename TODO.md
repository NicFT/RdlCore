# TODO

## Project Status Summary

Infrastructure complete. Excel Phases 4-5 complete. **Chart engine's GDI+→interface abstraction is now fully done**, including the construction-time crash chase discovered 2026-07-28 (Chart/Gauge model objects couldn't even be constructed on Linux at all): all three GDI+ construction walls (Font, GraphicsPath/HotRegionsList, StringFormat) are fixed and their full caller-migration sweeps are complete, WSL-verified with a real rendered PNG (a Sunburst chart with a Legend, nested rings, and per-node labels — not just a simple bar chart). **Word and IMAGE (JPEG/PNG/BMP) renderers are fully cross-platform now too** (TIFF/EMF remain permanently Windows-only, no portable equivalent). Gauge engine GDI+→interface abstraction is complete (no Skia backend yet — not started, low priority). PDF cross-platform rendering is complete. **Map engine is mid-migration**: Milestone A (interface + adapters) is done, Milestone B (real call-site conversion) is at 10 of ~20 painter files; separately, a real functional-correctness bug (not a cross-platform gap) was found and partially fixed — RDL-defined Map layers never actually populated any spatial elements at all, on any platform, until a 2026-07-31 fix for the static/embedded-point case. Full detail on all of these lives in `docs/`, not here — see the Documentation index below.

### Current Priorities

| Priority | Phase | Status | Risk |
|----------|-------|--------|------|
| 🔴 **HIGH** | Excel Phase 4: ImageFormatType Enum | ✅ COMPLETE | LOW |
| 🔴 **HIGH** | Excel Phase 5: IImageProvider Abstraction | ✅ COMPLETE | MEDIUM |
| 🟡 **HIGH** | Chart engine: GDI+ → interface abstraction | ✅ COMPLETE (2026-07-30) — Font, HotRegionsList/GraphicsPath, and StringFormat sweeps all finished; WSL-verified a real complex chart (Sunburst) renders correctly on Linux; see `docs/rendering-abstractions.md`, `docs/platform-support.md` | LOW |
| 🟡 **HIGH** | Gauge engine: GDI+ → interface abstraction | ✅ COMPLETE (no Skia backend — not started, low priority) | HIGH |
| 🔴 **HIGH** | Chart/Gauge: model classes' default Font crashed Linux at *construction* time (not just rendering) | ✅ DONE (2026-07-31) — full chase complete, see `tasks/chart-default-font-cross-platform.md` | LOW |
| 🔴 **HIGH** | Chart: `HotRegionsList`/`HotRegion` constructed raw `System.Drawing.Drawing2D.GraphicsPath` for tooltip hit-testing | ✅ DONE — internals and the ~30-file caller migration both complete; see `tasks/chart-hotregion-graphicspath-cross-platform.md` | LOW |
| 🟢 **MEDIUM** | Chart: `Label.Paint`/`Paint3D` constructed raw `System.Drawing.StringFormat` directly | ✅ DONE (2026-07-30) — `Label.cs` plus the full ~19-file/~64-site sweep complete; only the documented `SmartLabels`/`GetLabelPosition` permanent boundaries remain concrete, by design; see `tasks/chart-stringformat-cross-platform.md` | LOW |
| 🔵 **LOW** | PDF: cross-platform rendering | ✅ COMPLETE — see `docs/platform-support.md`'s "PDF (RDL engine)" section | LOW |
| 🔵 **LOW** | WebRequest → HttpClient migration (SYSLIB0014) | ✅ DONE except 3 Map-engine sites, deliberately deferred with the rest of the Map engine; see `tasks/webrequest-httpclient-migration.md` | LOW |
| 🔵 **LOW** | XmlValidatingReader → XmlReader migration (CS0618) | ✅ DONE (2026-07-27); see `docs/decisions.md`'s `RDLValidatingReader` entry | LOW |
| 🟡 **HIGH** | Map engine: GDI+ → interface abstraction | 🔄 Milestone A done (2026-07-28); Milestone B at 10 of ~20 painter files (2026-07-31) — remaining files are blocked on one of three now-documented permanent-boundary patterns (`DrawPathAbs` shared-mutable-field entanglement, gradient-brush `.Transform` manipulation, or missing `MapGraphics`-only interface overloads), not a plain file-at-a-time sweep; Milestones C (render-surface abstraction)/D (Skia backend)/E (EMF guard) not started; see `tasks/map-engine-cross-platform.md` | HIGH |
| 🔴 **HIGH** | Map engine: spatial-element population was a no-op (functional bug, all platforms) | 🔄 Static/embedded-point case fixed and verified end-to-end (2026-07-31, incl. 5 unrelated pre-existing `NullReferenceException` bugs found and fixed along the way, plus a real color-scale-legend rendering bug); dataset-bound population (`SpatialDataSetMapper.ProcessRow`) and line/polygon WKT geometry remain unimplemented; see `tasks/map-spatial-data-population-gap.md` | HIGH |
| 🔵 **LOW** | Remaining CS0649 dead-field warnings (~52 tracked + ~16 found later) | ✅ DONE (2026-07-30); see `tasks/remaining-warning-cleanup.md` | LOW |
| 🔵 **LOW** | Word (WORD/WORDOPENXML) renderer: cross-platform support | ✅ DONE (2026-07-30) — both the image-decode gap and the WORD-binary CFBF-writer gap are fixed and WSL-verified; see `tasks/word-renderer-cross-platform.md` | LOW |
| 🔵 **LOW** | IMAGE (TIFF/EMF) renderer: cross-platform support | ✅ DONE (2026-07-31) for BMP/JPEG/PNG, including Chart/Gauge/Map embedding, WSL-verified with a real rendered PNG; TIFF/EMF remain permanently Windows-only (no portable equivalent); see `tasks/image-renderer-cross-platform.md` | LOW |
| 🔵 **LOW** | RDL expression compiler: sandboxing + single-file deployment | ✅ Sandboxing documented as permanent limitation; single-file deployment fixed and verified end-to-end (2026-07-27, published-single-file harness); see `tasks/expression-compiler-modernization.md` | LOW |
| 🔴 **HIGH** | Excel renderer crashes found validating 903 production reports (2026-10-05) | ✅ FIXED, uncommitted — (1) `ImageInformation.CalculateMetrics`/`ImageFormatTypeHelper.DetectFromStream`: `SKCodec.Create(Stream)` disposed the shared image stream → `ObjectDisposedException` on repeated images; now wrapped in a non-owning `SKManagedStream`. (2) `LayoutEngine.GetInvalidImage`: Skia cannot encode BMP (`Encode(Bmp)` returns null) → `NullReferenceException` whenever an image fails to load; now writes `InvalidImage.ImageData` (already BMP) directly | LOW |
| 🔵 **LOW** | Memory investigation (Linux production, 2026-10-05) | 🔄 Windows: 1,880 renders of 16 real reports (PDF/Word/Excel) → no native leak (private-minus-GC-committed flat ~33–37 MB), collectible ALCs unload (count stays 1), managed growth converges (Roslyn bounded caches). Linux run pending on the user's server | LOW |
| 🔴 **HIGH** | RDL 2005 reports fail to compile on Linux: `DllNotFoundException: gdiplus.dll` (2026-10-19) | ✅ FIXED, uncommitted — `ReportSize.DotsPerInch` probed DPI through a GDI+ `Bitmap`/`Graphics`, reached by `UpgradeImpl2005.AdjustBodyWhitespace` on every 2005-schema report; now a constant 96. Verified on Windows: `gdiplus.dll` no longer loads during compile, PDF/XLSX output identical to before. 🔲 Open: `UpgradeImpl2005.FontFromString` (`FontConverter`/`new Font`) still needs GDI+ for 2005 reports containing Dundas chart/gauge CRIs — see `docs/platform-support.md` | LOW |
| 🔴 **HIGH** | Chart missing from PDF/Word on Linux, no error (2026-10-20) | ✅ FIXED, uncommitted, 🔲 pending verification on the user's Linux server — root cause (captured from the server's stack trace): a chart **Title** read `Title.Font`, whose getter lazily constructs a GDI+ `Font` → `DllNotFoundException(gdiplus.dll)`. Fix: `Title.GetFontResource` builds the font via the active backend (same pattern as `Legend.GetBaseFontResource`), and `ChartMapper.RenderTitleFont` passes the RDL family/size/style through `Title.SetFontDescriptor` when GDI+ can't build it, so the title keeps its report font (e.g. 12pt bold). The masking bug is fixed too: `DynamicImageInstance.CreateExceptionImage` now draws with SkiaSharp on non-Windows, so a future chart failure shows its error in the report instead of vanishing. Temporary stderr diagnostic removed. Follow-up found on the server: chart text rendered ~3x too small, because Skia fonts used a fixed 96 DPI while PDF/Word render the chart at higher DPI; now scaled by the bound surface's DPI (`SkiaChartFont.GetNativeFont`), with a new test. Tests: 2 new Skia title tests; DataVisualization 137/139 (2 BoxPlot 1–2 px failures pre-exist), Chart.Rdl 189/189 | LOW |
| 🔵 **LOW** | Test coverage: HTML/CSV/XML/WORD/IMAGE/Gauge/Map have none | ✅ DONE (2026-07-31) — HTML/CSV/XML/WORD/Gauge/IMAGE(TIFF/BMP)/Map all have smoke-test coverage; Map additionally now has a color-scale-legend fixture proven to render correctly (not just non-crashing); see `tasks/test-coverage-gaps.md` | LOW |

### Long-Term Vision

The architecture should support Windows, Linux, and macOS rendering, third-party rendering engines, and future rendering technologies — the rendering system should become a platform rather than a collection of built-in renderers. See `AGENTS.md`'s Mission statement for the one-line version; this file (not AGENTS.md) tracks how close each renderer actually is to that goal.

---

## Documentation index

- `docs/rendering-abstractions.md` — Excel/PDF renderer factory design + Chart/Gauge Ports & Adapters architecture (interfaces, namespaces, recurring patterns)
- `docs/platform-support.md` — current Windows/Linux/macOS support matrix and known gaps (Map's remaining migration, PDF)
- `docs/decisions.md` — architecture decisions and why (OxyPlot retraction, SkiaSharp re-target, per-method vs. per-type conversion, PDF font-embedding/shaping choices, etc.)
- `docs/troubleshooting.md` — common issues and known quirks found during the migration
- `docs/coding-standards.md` — engineering conventions, including ones established during the Chart/Gauge migration
- `docs/architecture-map.md` / `docs/build-and-test.md` / `docs/renderer-extension-guide.md` / `docs/examples.md` — supporting reference docs
- `tasks/adapter-layer-refactor.md` — broader adapter-layer scope and README compatibility-gap follow-ups (mostly resolved; a small residual list remains)
- `tasks/webrequest-httpclient-migration.md` — SYSLIB0014 migration scope (done except 3 Map-engine sites, deliberately deferred)
- `tasks/remaining-warning-cleanup.md` — CS0649 dead-field warnings (done)
- `tasks/word-renderer-cross-platform.md` — Word 97/WordOpenXml renderer cross-platform gap (done)
- `tasks/image-renderer-cross-platform.md` — IMAGE renderer cross-platform gap (done for BMP/JPEG/PNG incl. Chart/Gauge/Map embedding; TIFF/EMF permanently Windows-only)
- `tasks/chart-default-font-cross-platform.md` — Chart/Gauge model classes' default-Font gap (done) and the `Label.Paint`/`StringFormat` chase that followed from it
- `tasks/chart-hotregion-graphicspath-cross-platform.md` — HotRegionsList/HotRegion GraphicsPath gap (done)
- `tasks/chart-stringformat-cross-platform.md` — Chart engine's StringFormat gap (done)
- `tasks/map-engine-cross-platform.md` — Map engine GDI+→interface migration plan (Milestone A done; Milestone B at 10 of ~20 painter files, remaining files each blocked on a documented permanent-boundary pattern; Milestones C/D/E not started)
- `tasks/map-spatial-data-population-gap.md` — Map's RDL-to-spatial-element population was a no-op in 3 places (a real functional bug, not cross-platform-specific); static/embedded-point case fixed 2026-07-31; dataset-bound path and line/polygon geometry remain
- `tasks/expression-compiler-modernization.md` — RDL expression compiler sandboxing (permanent limitation) + single-file-deployment gaps (fixed)
- `tasks/test-coverage-gaps.md` — rendering-format test coverage (done — all formats now have smoke-test coverage)
- `tasks/upstream-issue-triage.md` — review of upstream `lkosson/reportviewercore` open issues against this fork's own tracking

## Notes

- Update this file's status table as milestones complete; keep any still-open scope in the linked `tasks/*.md` files, not here.
- Durable architecture facts, decisions, and known gaps belong in `docs/`, not in task-tracking narrative.
- Once a `tasks/*.md` file's work is complete, migrate anything durable into `docs/` and delete the file — don't leave a completed migration's narrative log behind as a duplicate record.
