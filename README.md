## Background Image Remover

A WPF desktop application for removing backgrounds from images using AI (ONNX U2Net/Silueta/Rmbg) and classical algorithms (GrabCut, SAM, Chroma Key, Magic Wand, KMeans, Flood Fill, Otsu).

### Features

- **AI-powered background removal** via ONNX models (U2Net, U2Netp, Silueta, Rmbg14, BriaRm)
- **Click-to-select segmentation** with MobileSAM (MobileSAM encoder/decoder)
- **Multi-point SAM refinement**: add multiple foreground click points to refine the SAM selection — the primary click plus any additional points all feed the decoder together
- **Classical algorithms**: GrabCut, Chroma Key, Magic Wand, KMeans, Flood Fill, Otsu
- **Interactive refinement**: brush-based foreground/background scribbles, mask adjustments (feather, expand, blur, gamma, threshold, despeckle, fill holes, smooth edges, CLAHE, median/bilateral filtering)
- **GrabCut rect handles**: after drawing the initial GrabCut rectangle, corner handles appear so you can move or resize it without redrawing; an "Edit rect" button is also available in the strategy panel
- **Edge refinement**: alpha matting, color decontamination, hole filling
- **Export**: PNG (transparent/solid color/blurred/gradient), JPEG, WebP
- **Batch processing** with configurable export options
- **Non-destructive history**: undo/redo timeline with step restoration
- **Project save/load** (.ibrproj): preserves all settings including SAM prompt points
- **Theme support**: light/dark/system with persistent settings

### Recent Improvements

#### Multi-point SAM (new)
- Add multiple foreground click points in SAM mode to refine the selection
- New `ClearSamPointsCommand` to reset all prompt points
- SAM prompt points are saved/restored in `.ibrproj` project files
- Additional points count displayed in the UI

#### New drawing & color tools (new)
- **Shape**: draw a rectangle, ellipse, line, arrow, **polygon** or **star** with stroke color/width and a semi-transparent fill — drag directly on the interactive preview to place it, then drag inside to **move** it, its **corner handles** to **resize** it, or its **rotation handle** (above the shape) to **tilt** it freely (hold **Shift** to snap rotation to 5° steps for precise alignment); percentage sliders plus sides/points, star-ratio and rotation controls allow fine-tuning
- **Pen**: freehand drawing (brush/pen) — drag on the image to draw strokes in a chosen color/width with rounded caps
- **Gradient**: overlay a linear (angle-selectable) or radial two-color gradient onto the image
- **Color Replace**: replace a target color (and close hues via tolerance/softness) with another color, optionally preserving the original luminance; click the preview to pick the target color
- **Duotone**: map image brightness onto a two-color palette with adjustable midpoint and strength, plus ready-made preset palettes (Mono, Black & Gold, Navy & Amber, Violet & Cyan, ...)
- **Sketch**: convert the image into a pencil sketch (luminance divided by its own blur) with adjustable softness and an optional negative inversion
- **Emboss**: relief filter with a light direction that snaps to 45° steps, adjustable strength, and a classic grayscale mode (or per-channel color emboss)
- **Bokeh**: scatter soft, blurred circles over the image with a chosen color, radius, count, opacity and edge blur
- **Cartoon**: cartoon look — bilateral smoothing for flat colors, per-channel quantization and dark adaptive-threshold outlines (adjustable smoothness, levels and edge strength)
- **Glow**: bloom around bright areas (luminance threshold, blur radius and strength) — dim images keep a neutral preview until the threshold is lowered
- **Wave**: sinusoidal ripple distortion with amplitude, wavelength and ridge angle
- New tools are registered **data-driven**: one factory registration in `App.xaml.cs` (metadata + session factory via the shared `ToolDefinition` / `StrategyToolDefinition` classes), a session view model, a data-templated view, an icon and unit tests — the palette and tab dispatch pick them up automatically. The modal tool-session chrome (badge + title + Cancel/Apply bar) is the shared `ToolSessionHeader` control, and color fields reuse the shared `ColorPickerField` control (swatch + popup picker) instead of per-tool picker boilerplate

#### Logging improvements (new)
- `FileLogService` now supports severity levels: `Debug`, `Info`, `Warning`, `Error`
- Log files are pruned automatically after 30 days
- Best-effort logging: failures never crash the application

#### Bug fixes
- `ApplyAsync` now guards against overlapping runs via the `IsBusy` flag, preventing race conditions on the shared strategy cache
- Adjustment parameters are now clamped to valid ranges in `DocumentViewModel.Adjustments.cs` to prevent crashes from out-of-range values

### Build

```powershell
dotnet build src/BackgroundImageRemover/BackgroundImageRemover.csproj
```

### Test

```powershell
dotnet test tests/BackgroundImageRemover.Tests/BackgroundImageRemover.Tests.csproj
```
