using Microsoft.ReportingServices.Common;
using Microsoft.ReportingServices.ReportProcessing;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace Microsoft.ReportingServices.OnDemandReportRendering
{
	internal class MapperBase : IDisposable
	{
		internal class FontCache : IDisposable
		{
			internal class TestAgent
			{
				private FontCache _host;

				public Dictionary<string, FontFamily> FontFamilies => _host.m_fontFamilies;

				public TestAgent(FontCache fontCache)
				{
					_host = fontCache;
				}

				public FontFamily TryCreateFont(string familyName)
				{
					return _host.TryCreateFontFamily(familyName);
				}

				public FontFamily RegisterFontFamilyWithFallback(string familyName)
				{
					return _host.RegisterFontFamilyWithFallback(familyName);
				}

				public FontFamily GetFontFamily(string familyName)
				{
					return _host.GetFontFamily(familyName);
				}
			}

			private class KeyInfo
			{
				public class EqualityComparer : IEqualityComparer<KeyInfo>
				{
					public bool Equals(KeyInfo x, KeyInfo y)
					{
						if (x.m_id == y.m_id && x.m_size == y.m_size && x.m_style == y.m_style)
						{
							return string.Compare(x.m_fontFamily, y.m_fontFamily, StringComparison.Ordinal) == 0;
						}
						return false;
					}

					public int GetHashCode(KeyInfo obj)
					{
						return obj.m_fontFamily.GetHashCode() ^ obj.m_size.GetHashCode() ^ obj.m_id.GetHashCode() ^ obj.m_style.GetHashCode();
					}
				}

				private int m_id;

				private string m_fontFamily;

				private float m_size;

				private FontStyle m_style;

				public KeyInfo(int id, string family, float size, FontStyle style)
				{
					m_id = id;
					m_fontFamily = family;
					m_size = size;
					m_style = style;
				}
			}

			private string m_defaultFontFamily;

			private static float m_defaultFontSize = 10f;

			private Dictionary<KeyInfo, Font> m_cachedFonts = new Dictionary<KeyInfo, Font>(new KeyInfo.EqualityComparer());

			private List<Font> m_fonts = new List<Font>();

			private Dictionary<string, FontFamily> m_fontFamilies = new Dictionary<string, FontFamily>();

			internal FontCache(string defaultFontFamily)
			{
				m_defaultFontFamily = defaultFontFamily;
			}

			// Family-name-only accessor for callers that just need a fallback font family string
			// (e.g. to feed into further style-resolution logic) - unlike GetDefaultFont(), this
			// never constructs a System.Drawing.Font, so it's safe to call even where GDI+ isn't
			// available at all (Linux under .NET 10 - see docs/platform-support.md).
			internal string DefaultFontFamily => m_defaultFontFamily;

			internal float DefaultFontSize => m_defaultFontSize;

			public Font GetFontFromCache(int id, string familyName, float size, FontStyle style)
			{
				KeyInfo key = new KeyInfo(id, familyName, size, style);
				if (!m_cachedFonts.ContainsKey(key))
				{
					// System.Drawing.Font construction is impossible on Linux under .NET 10
					// (GDI+ cannot construct anything at all there, even with libgdiplus
					// installed - see docs/platform-support.md). Cache a null rather than
					// letting the exception propagate and abort the whole RDL-to-model
					// mapping pass; callers that assign this onto a model's Font property
					// leave it at its own (already-lazy) default, which several Chart-engine
					// render paths already tolerate (Legend/Axis - see
					// tasks/chart-default-font-cross-platform.md).
					Font font;
					try
					{
						font = CreateSafeFont(familyName, size, style);
					}
					catch (Exception ex) when (!AsynchronousExceptionDetection.IsStoppingException(ex))
					{
						font = null;
					}
					m_cachedFonts.Add(key, font);
				}
				return m_cachedFonts[key];
			}

			public Font GetDefaultFontFromCache(int id)
			{
				return GetFontFromCache(id, m_defaultFontFamily, m_defaultFontSize, MappingHelper.GetStyleFontStyle(FontStyles.Normal, FontWeights.Normal, TextDecorations.None));
			}

			public Font GetFont(string familyName, float size, FontStyle style)
			{
				Font font = null;
				try
				{
					font = CreateSafeFont(familyName, size, style);
					m_fonts.Add(font);
					return font;
				}
				catch (Exception ex)
				{
					if (font != null && !m_fonts.Contains(font))
					{
						font.Dispose();
						font = null;
					}
					// See the comment in GetFontFromCache: Font construction is impossible on
					// Linux under .NET 10, no matter what family/size/style is requested. Return
					// null instead of throwing so a caller assigning this onto a model's Font
					// property just leaves it unset, rather than aborting the whole mapping pass.
					if (AsynchronousExceptionDetection.IsStoppingException(ex))
					{
						throw;
					}
					return null;
				}
			}

			public Font GetDefaultFont()
			{
				return GetFont(m_defaultFontFamily, m_defaultFontSize, MappingHelper.GetStyleFontStyle(FontStyles.Normal, FontWeights.Normal, TextDecorations.None));
			}

			private FontFamily TryCreateFontFamily(string familyName)
			{
				try
				{
					return new FontFamily(familyName);
				}
				catch
				{
				}
				return null;
			}

			private FontFamily RegisterFontFamilyWithFallback(string familyName)
			{
				FontFamily fontFamily = TryCreateFontFamily(familyName);
				if (fontFamily == null && familyName != m_defaultFontFamily)
				{
					fontFamily = TryCreateFontFamily(m_defaultFontFamily);
				}
				if (fontFamily == null && m_defaultFontFamily != "Arial")
				{
					fontFamily = TryCreateFontFamily("Arial");
				}
				if (fontFamily != null)
				{
					m_fontFamilies.Add(familyName, fontFamily);
				}
				return fontFamily;
			}

			private FontFamily GetFontFamily(string familyName)
			{
				FontFamily value = null;
				if (!m_fontFamilies.TryGetValue(familyName, out value))
				{
					value = RegisterFontFamilyWithFallback(familyName);
				}
				return value;
			}

			private Font CreateSafeFont(string familyName, float size, FontStyle fontStyle)
			{
				FontFamily fontFamily = GetFontFamily(familyName);
				if (fontFamily == null)
				{
					return new Font(familyName, size, fontStyle);
				}
				familyName = fontFamily.Name;
				if (fontFamily.IsStyleAvailable(fontStyle))
				{
					return new Font(familyName, size, fontStyle);
				}
				FontStyle fontStyle2 = FontStyle.Regular;
				foreach (FontStyle value in Enum.GetValues(typeof(FontStyle)))
				{
					if (fontFamily.IsStyleAvailable(value))
					{
						fontStyle2 = value;
						break;
					}
				}
				if (fontFamily.IsStyleAvailable(fontStyle | fontStyle2))
				{
					return new Font(familyName, size, fontStyle | fontStyle2);
				}
				return new Font(familyName, size, fontStyle2);
			}

			public void Dispose()
			{
				foreach (FontFamily value in m_fontFamilies.Values)
				{
					value.Dispose();
				}
				foreach (Font value2 in m_cachedFonts.Values)
				{
					value2?.Dispose();
				}
				m_cachedFonts.Clear();
				foreach (Font font in m_fonts)
				{
					font.Dispose();
				}
				m_fonts.Clear();
				GC.SuppressFinalize(this);
			}
		}

		private FontCache m_fontCache;

		private float m_dpiX = 96f;

		private float m_dpiY = 96f;

		private int? m_widthOverrideInPixels;

		private int? m_heightOverrideInPixels;

		public float DpiX
		{
			internal get
			{
				return m_dpiX;
			}
			set
			{
				m_dpiX = value;
			}
		}

		public float DpiY
		{
			protected get
			{
				return m_dpiX;
			}
			set
			{
				m_dpiY = value;
			}
		}

		protected int? WidthOverrideInPixels => m_widthOverrideInPixels;

		protected int? HeightOverrideInPixels => m_heightOverrideInPixels;

		public double? WidthOverride
		{
			set
			{
				if (value.HasValue)
				{
					ValidatePositiveValue(value.Value);
					int value2 = MappingHelper.ToIntPixels(value.Value, Unit.Millimeter, DpiX);
					ValidatePositiveValue(value2);
					m_widthOverrideInPixels = value2;
				}
				else
				{
					m_widthOverrideInPixels = null;
				}
			}
		}

		public double? HeightOverride
		{
			set
			{
				if (value.HasValue)
				{
					ValidatePositiveValue(value.Value);
					int value2 = MappingHelper.ToIntPixels(value.Value, Unit.Millimeter, DpiY);
					ValidatePositiveValue(value2);
					m_heightOverrideInPixels = value2;
				}
				else
				{
					m_heightOverrideInPixels = null;
				}
			}
		}

		internal MapperBase(string defaultFontFamily)
		{
			m_fontCache = new FontCache(string.IsNullOrEmpty(defaultFontFamily) ? MappingHelper.DefaultFontFamily : defaultFontFamily);
		}

		internal Font GetDefaultFont()
		{
			return m_fontCache.GetDefaultFont();
		}

		internal Font GetDefaultFontFromCache(int id)
		{
			return m_fontCache.GetDefaultFontFromCache(id);
		}

		internal Font GetFontFromCache(int id, string fontFamily, float fontSize, FontStyles fontStyle, FontWeights fontWeight, TextDecorations textDecoration)
		{
			return m_fontCache.GetFontFromCache(id, fontFamily, fontSize, MappingHelper.GetStyleFontStyle(fontStyle, fontWeight, textDecoration));
		}

		internal Font GetFontFromCache(int id, Style style, StyleInstance styleInstance)
		{
			return GetFontFromCache(id, MappingHelper.GetStyleFontFamily(style, styleInstance, m_fontCache.DefaultFontFamily), MappingHelper.GetStyleFontSize(style, styleInstance), MappingHelper.GetStyleFontStyle(style, styleInstance), MappingHelper.GetStyleFontWeight(style, styleInstance), MappingHelper.GetStyleFontTextDecoration(style, styleInstance));
		}

		protected Font GetFont(Style style, StyleInstance styleInstance)
		{
			return m_fontCache.GetFont(MappingHelper.GetStyleFontFamily(style, styleInstance, m_fontCache.DefaultFontFamily), MappingHelper.GetStyleFontSize(style, styleInstance), MappingHelper.GetStyleFontStyle(MappingHelper.GetStyleFontStyle(style, styleInstance), MappingHelper.GetStyleFontWeight(style, styleInstance), MappingHelper.GetStyleFontTextDecoration(style, styleInstance)));
		}

		// Resolves the same family/size/style GetFont/GetDefaultFont would build, without
		// constructing a System.Drawing.Font - for platforms where that construction fails
		// (Linux under .NET 10), so the RDL font can still reach a Skia-backed renderer.
		protected void GetFontDescriptor(Style style, StyleInstance styleInstance, out string familyName, out float size, out FontStyle fontStyle)
		{
			if (style == null)
			{
				familyName = m_fontCache.DefaultFontFamily;
				size = m_fontCache.DefaultFontSize;
				fontStyle = MappingHelper.GetStyleFontStyle(FontStyles.Normal, FontWeights.Normal, TextDecorations.None);
				return;
			}
			familyName = MappingHelper.GetStyleFontFamily(style, styleInstance, m_fontCache.DefaultFontFamily);
			size = MappingHelper.GetStyleFontSize(style, styleInstance);
			fontStyle = MappingHelper.GetStyleFontStyle(MappingHelper.GetStyleFontStyle(style, styleInstance), MappingHelper.GetStyleFontWeight(style, styleInstance), MappingHelper.GetStyleFontTextDecoration(style, styleInstance));
		}

		private static void ValidatePositiveValue(double value)
		{
			if (double.IsNaN(value) || value <= 0.0 || value > double.MaxValue)
			{
				throw new RenderingObjectModelException(ProcessingErrorCode.rsInvalidParameterRange, value);
			}
		}

		private static void ValidatePositiveValue(int value)
		{
			if (value <= 0 || value > int.MaxValue)
			{
				throw new RenderingObjectModelException(ProcessingErrorCode.rsInvalidParameterRange, value);
			}
		}

		public virtual void Dispose()
		{
			m_fontCache.Dispose();
			GC.SuppressFinalize(this);
		}
	}
}
