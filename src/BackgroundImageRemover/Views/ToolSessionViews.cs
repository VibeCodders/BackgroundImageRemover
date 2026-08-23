using System.Windows.Controls;

namespace BackgroundImageRemover.Views;

/// <summary>
/// Consolidated code-behinds for the tool-session views whose only logic is the standard
/// <c>InitializeComponent()</c> constructor. Each partial class pairs with the XAML file of the
/// same name (the XAML compiler generates the rest); keeping them in one file removes the
/// ~36 near-identical <c>*.xaml.cs</c> boilerplate files that used to sit next to the views.
/// Views with real code-behind logic (stroke handling, click sampling, overlays) keep their
/// own files next to their XAML.
/// </summary>
public partial class AdjustmentsToolSessionView : UserControl
{
    public AdjustmentsToolSessionView() => InitializeComponent();
}

public partial class BlurToolSessionView : BrushStrokeSessionViewBase
{
    public BlurToolSessionView() => InitializeComponent();
}

public partial class BokehToolSessionView : UserControl
{
    public BokehToolSessionView() => InitializeComponent();
}

public partial class CartoonToolSessionView : UserControl
{
    public CartoonToolSessionView() => InitializeComponent();
}

public partial class CloneStampToolSessionView : BrushStrokeSessionViewBase
{
    public CloneStampToolSessionView() => InitializeComponent();
}

public partial class ComposeToolSessionView : UserControl
{
    public ComposeToolSessionView() => InitializeComponent();
}

public partial class DodgeBurnToolSessionView : BrushStrokeSessionViewBase
{
    public DodgeBurnToolSessionView() => InitializeComponent();
}

public partial class DuotoneToolSessionView : UserControl
{
    public DuotoneToolSessionView() => InitializeComponent();
}

public partial class EmbossToolSessionView : UserControl
{
    public EmbossToolSessionView() => InitializeComponent();
}

public partial class EmojiToolSessionView : UserControl
{
    public EmojiToolSessionView() => InitializeComponent();
}

public partial class FiltersToolSessionView : UserControl
{
    public FiltersToolSessionView() => InitializeComponent();
}

public partial class FrameToolSessionView : UserControl
{
    public FrameToolSessionView() => InitializeComponent();
}

public partial class FxToolSessionView : UserControl
{
    public FxToolSessionView() => InitializeComponent();
}

public partial class GlowToolSessionView : UserControl
{
    public GlowToolSessionView() => InitializeComponent();
}

public partial class GradientToolSessionView : UserControl
{
    public GradientToolSessionView() => InitializeComponent();
}

public partial class HalftoneToolSessionView : UserControl
{
    public HalftoneToolSessionView() => InitializeComponent();
}

public partial class HealToolSessionView : BrushStrokeSessionViewBase
{
    public HealToolSessionView() => InitializeComponent();
}

public partial class HueSatToolSessionView : BrushStrokeSessionViewBase
{
    public HueSatToolSessionView() => InitializeComponent();
}

public partial class LassoSelectToolSessionView : BrushStrokeSessionViewBase
{
    public LassoSelectToolSessionView() => InitializeComponent();
}

public partial class LevelsToolSessionView : UserControl
{
    public LevelsToolSessionView() => InitializeComponent();
}

public partial class LiquifyToolSessionView : UserControl
{
    public LiquifyToolSessionView() => InitializeComponent();
}

public partial class NoiseToolSessionView : BrushStrokeSessionViewBase
{
    public NoiseToolSessionView() => InitializeComponent();
}

public partial class OilPaintToolSessionView : UserControl
{
    public OilPaintToolSessionView() => InitializeComponent();
}

public partial class OverlayToolSessionView : UserControl
{
    public OverlayToolSessionView() => InitializeComponent();
}

public partial class PenToolSessionView : BrushStrokeSessionViewBase
{
    public PenToolSessionView() => InitializeComponent();
}

public partial class PerspectiveToolSessionView : UserControl
{
    public PerspectiveToolSessionView() => InitializeComponent();
}

public partial class ResizeToolSessionView : UserControl
{
    public ResizeToolSessionView() => InitializeComponent();
}

public partial class RotateToolSessionView : UserControl
{
    public RotateToolSessionView() => InitializeComponent();
}

public partial class SharpenToolSessionView : BrushStrokeSessionViewBase
{
    public SharpenToolSessionView() => InitializeComponent();
}

public partial class SketchToolSessionView : UserControl
{
    public SketchToolSessionView() => InitializeComponent();
}

public partial class TextToolSessionView : UserControl
{
    public TextToolSessionView() => InitializeComponent();
}

public partial class ThermalToolSessionView : UserControl
{
    public ThermalToolSessionView() => InitializeComponent();
}

public partial class TiltShiftToolSessionView : UserControl
{
    public TiltShiftToolSessionView() => InitializeComponent();
}

public partial class TransformToolSessionView : UserControl
{
    public TransformToolSessionView() => InitializeComponent();
}

public partial class VignetteToolSessionView : UserControl
{
    public VignetteToolSessionView() => InitializeComponent();
}

public partial class WaveToolSessionView : UserControl
{
    public WaveToolSessionView() => InitializeComponent();
}
