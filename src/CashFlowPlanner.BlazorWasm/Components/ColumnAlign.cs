namespace CashFlowPlanner.BlazorWasm.Components;

/// <summary>
/// How a <see cref="Column{TItem}"/>'s cells line up.
/// </summary>
public enum ColumnAlign
{
    /// <summary>Left-aligned prose: names, categories, dates written out.</summary>
    Text = 0,

    /// <summary>
    /// Right-aligned, tabular figures. Emits the <c>num</c> class on the header, every body cell
    /// and the footer cell alike, so a column cannot end up with a right-aligned total under
    /// left-aligned amounts.
    /// </summary>
    Number = 1,
}
