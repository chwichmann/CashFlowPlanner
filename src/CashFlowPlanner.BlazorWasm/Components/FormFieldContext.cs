namespace CashFlowPlanner.BlazorWasm.Components;

/// <summary>
/// What a <see cref="FormField"/> hands to the control inside it.
///
/// The control has to put <see cref="Id"/> on itself, which is the one link the component cannot
/// forge on the consumer's behalf - Blazor has no way to reach into a child fragment's markup and
/// add an attribute. What it does guarantee is that the <c>for</c> on the label and the
/// <see cref="Id"/> offered here are the same string, so the association can only be missed by
/// ignoring the context outright rather than by forgetting to invent an id.
/// </summary>
/// <param name="Id">The id the control must carry, and the value of the label's <c>for</c>.</param>
/// <param name="DescribedBy">
/// Space-separated ids of the hint and validation text, for the control's <c>aria-describedby</c>,
/// or null when the field has neither.
/// </param>
public sealed record FormFieldContext(string Id, string? DescribedBy);

/// <summary>How a <see cref="FormField"/> arranges its label and control.</summary>
public enum FormFieldLayout
{
    /// <summary>Label above the control. Text boxes, selects, dates, amounts.</summary>
    Stacked = 0,

    /// <summary>
    /// Control first, label beside it, inside a Bootstrap <c>.form-check</c>. Three pages spelled
    /// this out three different ways - one of them putting a second, unassociated
    /// <c>.form-label</c> above the whole thing - which is why it is a layout here rather than
    /// something each page arranges for itself.
    /// </summary>
    Checkbox = 1,
}
