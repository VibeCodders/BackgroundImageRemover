namespace BackgroundImageRemover.Models;

/// <summary>Which OpenCV inpainting algorithm to use for <see cref="UncropFillMode.Inpaint"/>.</summary>
public enum UncropInpaintMethod
{
    Telea,
    NavierStokes
}
