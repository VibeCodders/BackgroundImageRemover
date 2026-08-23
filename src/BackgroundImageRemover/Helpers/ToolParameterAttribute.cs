using System;

namespace BackgroundImageRemover.Helpers;

/// <summary>
/// Marks an <c>[ObservableProperty]</c> backing field (or a manual property) as a tool parameter:
/// whenever its value changes, <see cref="ViewModels.ToolSessionViewModelBase"/> automatically
/// routes the change into the tool's parameter-refresh pipeline (the debounced live preview).
///
/// This replaces the repetitive <c>partial void OnXxxChanged(...) => RequestRefresh();</c>
/// boilerplate that was copy-pasted once per parameter in every tool view model, so tools only
/// declare their parameters and the shared base takes care of refreshing the preview.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class ToolParameterAttribute : Attribute
{
}
