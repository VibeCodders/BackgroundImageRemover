## Background Image Remover

A WPF desktop application for removing backgrounds from images using AI (ONNX U2Net/Silueta/Rmbg) and classical algorithms (GrabCut, SAM, Chroma Key, Magic Wand, KMeans, Flood Fill, Otsu).

### Features

- **AI-powered background removal** via ONNX models (U2Net, U2Netp, Silueta, Rmbg14, BriaRm)
- **Click-to-select segmentation** with MobileSAM (MobileSAM encoder/decoder)
- **Multi-point SAM refinement**: add multiple foreground click points to refine the SAM selection — the primary click plus any additional points all feed the decoder together
- **Classical algorithms**: GrabCut, Chroma Key, Magic Wand, KMeans, Flood Fill, Otsu
- **Interactive refinement**: brush-based foreground/background scribbles, mask adjustments (feather, expand, blur, gamma, threshold, despeckle, fill holes, smooth edges, CLAHE, median/bilateral filtering)
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
- **Shape**: draw a rectangle, ellipse, line or arrow with stroke color/width and a semi-transparent fill, positioned/sized as a percentage of the image
- **Gradient**: overlay a linear (angle-selectable) or radial two-color gradient onto the image
- **Color Replace**: replace a target color (and close hues via tolerance/softness) with another color, optionally preserving the original luminance
- **Duotone**: map image brightness onto a two-color palette with adjustable midpoint and strength
- New tools follow the existing `IToolDefinition` convention: one definition class, a session view model, a data-templated view, an icon, DI registration and unit tests — the palette and tab dispatch pick them up automatically

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
