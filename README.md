# RdlCore
> **Internal tool. Not distributed externally.** This is a fork maintained for our own use, not a public open-source project. See the licensing notice below and the [License](#license) section before using it.

# RdlCore Edition

This project is a **fork of RdlCore**, based on the work and modifications made by **ShadowedMists**, which itself is a fork of **lkosson/reportviewercore**.

The goal of this fork is to provide a fully functional RDLC/RDL reporting engine for **.NET 10**, supporting both **Windows** and **Linux** environments, while removing legacy GDI+ dependencies and fixing multiple issues discovered during the migration to a true cross-platform architecture.

---

# Main Features and Improvements

## Linux Compatibility

Several modifications were implemented to allow report generation on Linux without relying on Windows-only APIs.

Key improvements include:

- Removal of remaining GDI+ dependencies in document generation paths.
- Migration of image processing to **SkiaSharp**.
- Cross-platform image loading and metadata handling.
- HarfBuzz integration and native library support.
- Full PDF and Word document generation on Linux.
- Improved font handling using SkiaSharp typefaces.

---

## Word Renderer Fixes

A major issue prevented Word (.docx) generation on Linux due to indirect usage of GDI+ APIs.

### Fixes

- Replaced image metadata extraction based on System.Drawing.Image.
- Introduced platform-aware image processing through:
  - ImageProviderFactory
  - CrossPlatformImageProvider
- Eliminated Image.FromStream() dependency in Linux execution paths.

### Result

Word document generation now works correctly on both Windows and Linux.

---

## PDF Renderer Fixes

### HarfBuzz Support

- Added support for libHarfBuzzSharp.so.
- Corrected package compatibility between SkiaSharp, SkiaSharp.HarfBuzz and HarfBuzzSharp.

### Text Rendering Fixes

A critical issue was discovered where generated PDFs displayed all characters overlapping each other.

#### Root Cause

Glyph advances were written to the PDF using pixel units rather than the 1000-units-per-em coordinate space required by the PDF specification.

#### Fix

Implemented proper normalization of glyph widths generated through the SkiaSharp/HarfBuzz text rendering pipeline.

#### Result

- Correct character spacing.
- Proper text layout.
- Visual output matching the Windows implementation.

---

## Font Handling Improvements

- Migration to SKTypeface font resolution.
- Cross-platform font fallback support.
- Improved PDF font embedding.
- Better Unicode and complex text shaping support.

---

# Building the Project

## Important

**Do not use the Visual Studio Build command to create distributable binaries.**

Always use the .NET CLI.

### Publish (Recommended)

```bash
dotnet publish -c Release
```

For Linux:

```bash
dotnet publish -c Release -r linux-x64 --self-contained false
```

This guarantees:

- Proper dependency resolution.
- Inclusion of SkiaSharp native libraries.
- Inclusion of HarfBuzz native libraries.
- Correct runtime asset generation.
- Correct runtimes directory structure.

---

# Linux Requirements

```bash
sudo apt update

sudo apt install -y \
    fontconfig \
    libfontconfig1
```

---

# Windows Fonts on Linux

## Highly Recommended

To achieve visual consistency between reports generated on Windows and Linux, Microsoft fonts should be installed on the Linux server.

Commonly used fonts include:

- Arial
- Calibri
- Tahoma
- Segoe UI
- Times New Roman
- Verdana

Without these fonts installed, SkiaSharp will use fallback fonts which may lead to:

- Layout differences
- Text alignment differences
- Different line wrapping
- Different text sizes
- Visual differences between Windows and Linux generated documents

### Installing Fonts

```bash
sudo mkdir -p /usr/share/fonts/truetype/msfonts
```

Copy the desired .ttf files into that directory and rebuild the cache:

```bash
sudo fc-cache -f -v
```

Verify installed fonts:

```bash
fc-list
```

---

# Validated Environment

- .NET 10
- Windows 11
- Ubuntu Server
- SkiaSharp 3.119.1
- SkiaSharp.HarfBuzz 3.119.1
- HarfBuzzSharp 8.3.1.2

---

# Credits

This project is based on the excellent work provided by:

- Microsoft ReportViewer
- lkosson/reportviewercore
- ShadowedMists/RdlCore

Additional Linux compatibility fixes, PDF rendering improvements, Word rendering improvements, and cross-platform adaptations were implemented by **NicFT** for use with modern .NET 10 environments.
