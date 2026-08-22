using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace CashFlowPlanner.BlazorWasm.Components.Charts;

public sealed class SvgText : ComponentBase
{
    [Parameter]
    public double X { get; set; }

    [Parameter]
    public double Y { get; set; }

    [Parameter]
    public string? TextAnchor { get; set; }

    [Parameter]
    public string? Fill { get; set; }

    [Parameter]
    public int FontSize { get; set; } = 11;

    /// <summary>An SVG transform, e.g. a rotation for an axis label.</summary>
    [Parameter]
    public string? Transform { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var sequence = 0;

        builder.OpenElement(sequence++, "text");

        // Invariant, never the ambient culture. Blazor renders a boxed double through
        // CultureInfo.CurrentCulture, so under de-DE - one of the four regions Settings offers -
        // y="123.5" became y="123,5", which SVG rejects as a length and silently drops the label
        // to the origin. SvgLineChart already routes its own coordinates through an invariant
        // formatter for exactly this reason; passing them through this component skipped it.
        builder.AddAttribute(sequence++, "x", Format(X));
        builder.AddAttribute(sequence++, "y", Format(Y));
        builder.AddAttribute(sequence++, "font-size", FontSize.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(Transform))
        {
            builder.AddAttribute(sequence++, "transform", Transform);
        }

        if (!string.IsNullOrWhiteSpace(TextAnchor))
        {
            builder.AddAttribute(sequence++, "text-anchor", TextAnchor);
        }

        if (!string.IsNullOrWhiteSpace(Fill))
        {
            builder.AddAttribute(sequence++, "fill", Fill);
        }

        builder.AddContent(sequence++, ChildContent);

        builder.CloseElement();
    }

    private static string Format(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);
}