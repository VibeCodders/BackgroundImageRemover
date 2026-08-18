namespace BackgroundImageRemover.Models;

/// <summary>Reflection border method used by Uncrop Mirror fill mode.</summary>
public enum UncropMirrorType
{
    /// <summary>Reflects without duplicating the edge pixels (fedcba|abcdef|fedcba).</summary>
    Reflect101,

    /// <summary>Reflects with edge pixels duplicated (gfedcb|abcdef|fedcba).</summary>
    Reflect
}
