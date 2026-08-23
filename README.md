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
- **Thermal**: heatmap palette (blue→cyan→green→yellow→red) mapped onto image luminance with adjustable intensity and an optional invert (cold = bright)
- **Oil Paint**: flat dominant colours in brush-sized neighbourhoods (brush size + detail levels) for a painted look
- **Halftone**: renders the image as dots on white whose size follows local brightness, with a selectable dot color and optional invert
- New tools are registered **data-driven**: one factory registration in `App.xaml.cs` (metadata + session factory via the shared `ToolDefinition` / `StrategyToolDefinition` classes), a session view model, a data-templated view, an icon and unit tests — the palette and tab dispatch pick them up automatically. The modal tool-session chrome (badge + title + Cancel/Apply bar) is the shared `ToolSessionHeader` control, color fields reuse the shared `ColorPickerField` control (swatch + popup picker), and every labelled slider row is the shared `SliderField` control (caption + formatted value + slider) instead of per-tool boilerplate; tool sessions also share a single base `Reset` command (each tool only overrides `OnReset` with its defaults). The whole session layout (bottom status bar + right settings panel with title/description/reset + preview area) is the shared `ToolSessionPanel` control — views only supply their controls and preview. Every brush/freehand tool session (Blur, Sharpen, Mosaic, Dodge/Burn, Hue/Sat, Noise, Heal, Retouch, Pen, Lasso, Clone Stamp) implements the `IBrushStrokeSession` contract (`BrushRadius` + `OnStrokeStart/Move/End`); their views inherit the stroke handlers from `BrushStrokeSessionViewBase` and only wire `OnStrokeStart/Move/End` in XAML. Mask-paint tools derive from `MaskToolSessionViewModelBase`, which owns the whole-image / painted-mask / unchanged branching in a single `BuildResult` + `RefreshResult` (tools only implement `ApplyEffect`, optionally `ApplyEffectToRegion`, and their `OnResetToolDefaults`), and the shared `Reset` command lives on the session base with per-tool `OnReset` overrides. Brush tools that build their result from an independent BGR working copy (Heal, Retouch) derive from `WorkingCopyToolSessionViewModelBase`, which hosts the shared `BuildResult` + `RefreshResult` preview/apply template. Service code shares `ImageProcessingUtility` (LUTs, channel ops, HSV saturation, CLAHE via `ApplyClahe`, alpha compositing, and `BlendInPlace` for the repeated "apply effect + AddWeighted + dispose input" pattern), which the `ImageProcessingHelper.ApplyAdjustments` pipeline reuses for saturation, clarity, dehaze, auto-enhance and posterize instead of re-implementing them (the 6 blend-toward-effect sites in the pipeline are one-liners via `BlendInPlace`); the `PreviewToolSessionViewModelBase` reset command now runs a shared preview refresh after a per-tool `OnResetDefaults` hook (19 tools migrated, so no tool can forget to repaint on Reset), and the shared `GetWorkingAlpha` helper is reused by Frame/Overlay instead of re-rolling the opaque-alpha fallback; the preview/bitmap reconstruction flows are shared via two `MatExtensions` helpers — `ToPreviewBitmap(previewBgr, fullAlpha)` collapses the repeated `isCutout ? BuildPreviewWithAlpha : ToBitmapSource` ternary across the document load/duplicate/project/rotate/uncrop flows and the Background Remover session init, and `ToResultBitmap(bgr, alpha)` is the guarded "reconstruct the result bitmap from the working pair" used by the document's `RefreshResultBitmapFromWorking` and by every tool-session base (`PreviewToolSessionViewModelBase`, `MaskToolSessionViewModelBase`, `WorkingCopyToolSessionViewModelBase`) — with new `MatExtensionsTests` (STA-sampled alpha/format) pinning the opaque-vs-meaningful-transparency branching; the document's "working state changed" ceremony (dirty flag, cutout recompute, undo/redo availability, result re-render, display notification, optional export/has-result re-evaluation and status line) is a single private `FinalizeWorkingState(markDirty, notifyCommandAvailability, status)` shared by `ReplaceWorkingState`, `FinalizeHistoryRestore`, `AdoptLoadedCutout` and `SetWorkingResult` — the four callers previously re-rolled the same tail (the strategy-result sites even re-notified Undo/Redo right after `RefreshUndoRedoState`, which already does it); the image preview controls (ImagePreviewControl, ZoomableImageControl, CompareImageControl) share the `PanGesture` state machine (middle/right/Ctrl+left-drag pan with capture and leave-cancellation) and the `ZoomController` (fit view, wheel-zoom toward the cursor, Ctrl+Plus/Minus/0/1 shortcuts, actual-pixels 1:1) instead of each owning their own pan/zoom fields and handlers — the per-control differences (host element, image availability, pixel scale, zoom limits) are injected as delegates; the shared `ScribbleManager.Start/Move` overloads take an `InteractionMode` and internally branch to stroke vs eraser, so the two VMs that own scribbles no longer repeat the erase/foreground branching; the shared `ModelManager` owns the ONNX/SAM download orchestration (`EnsureOnnxReadyAsync`/`EnsureSamReadyAsync`), the SAM embedding computation and the SAM prompt-point reset used by both `DocumentViewModel` and the Background Remover tool tab — the strategy view models, full-res image source, embedding error reporter and preview refresh are injected so the two hosts keep their exact behavior (the doc VM's extra ready-hook and the session VM's embedding error path stay local); `ModelManager.OnSamPrimaryPointClicked`/`OnSamAdditionalPointClicked` unify the SAM point-click handlers that the two VMs duplicated, with the guard difference parametrized (the doc host rejects clicks while the embedding is missing with a status message, the tool tab silently ignores clicks while SAM is not the active strategy — injected as `canHandleSamClick` + `onSamClickRejected`) and the point storage staying on each host (it feeds context building and project serialization); the shared `PreviewRunner` owns the whole preview flow (`RequestPreviewDebounced` debounce scheduling, the strategy readiness guards, scribble/preview snapshotting, cancellation-token lifecycle, `RunPreviewAsync` and error handling) that was previously duplicated in `DocumentViewModel.Strategies.cs` and the Background Remover session's `Preview.cs` — the preview Mat, strategy registry, readiness predicate, scribble manager, context builder and result/status setters are injected (the doc VM's `IsImageLoaded && ResultMode == None` scheduling gate is injected too, and `CancelInFlight()` replaces the ad-hoc `_previewCts?.Cancel()` before loading a new image/project), and the runner owns the debounce timer, preview CTS and last-result disposal that the VMs used to keep as fields; `TransformService.Pad` delegates to the shared `EditingGuard.ExpandCanvas` (identical canvas-padding logic); `EditingGuard` centralizes guards, strength clamping and odd kernel sizes (`EnsureOdd`); service tests share `ServiceTestHelper` for the repeated size/type, pixels-changed and no-change assertions; view-model tests reuse the shared `TestDoubles` (fake shell, services, sized image loader and document factory) instead of duplicating private fakes per file — 8 test files migrated, ~890 lines of duplicated fakes removed (dialog and uncrop fakes are virtual so test-specific variants can derive from them). The `TestImageLoader` itself is now the single configurable image loader for view-model tests: size, solid background color, optional uniform alpha channel (`alphaValue: 0` for a fully transparent cutout) and an optional shape-painting callback (subject rectangle + blur, as the GrabCut fixtures need) — the per-file `AlphaImageLoader`/`PlainImageLoader`/`SubjectImageLoader`/`DummyImageLoaderService` classes (8 copies, ~170 lines) are gone. `MaskToolPaintModeTests` pins the unified mask-tool semantics (effect inside the painted mask, unchanged image outside, whole-image and no-flag branches), and `WorkingCopyToolSessionViewModelBaseTests` pins the shared working-copy template used by Heal/Retouch (preview rebuilt from the working copy, result built from it, and Apply pushing that result into the parent document).

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
