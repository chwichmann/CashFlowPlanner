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

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var sequence = 0;

        builder.OpenElement(sequence++, "text");
        builder.AddAttribute(sequence++, "x", X);
        builder.AddAttribute(sequence++, "y", Y);
        builder.AddAttribute(sequence++, "font-size", FontSize);

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
}