using CashFlowPlanner.Core.RealEstate;

namespace CashFlowPlanner.BlazorWasm.Models;

/// <summary>
/// One property, as the real-estate editor edits it.
///
/// Every optional field here is optional in the domain too and defaults to the behaviour a plan
/// had before the field existed: no growth rate holds the value flat, and no acquisition or
/// disposal date means the household owns it for the whole horizon.
/// </summary>
public sealed class RealEstateAssetEditModel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public RealEstateType Type { get; set; } = RealEstateType.House;

    public decimal CurrentEstimatedValue { get; set; }

    public DateOnly? ValuationDate { get; set; }

    /// <summary>
    /// Zero unless the user says otherwise. Any other default would be this app inventing a
    /// forecast of the Swiss property market and quietly attributing it to the household.
    /// </summary>
    public decimal AnnualValueGrowthPercent { get; set; }

    public decimal Pillar2BvgUsedAmount { get; set; }

    public DateOnly? AcquisitionDate { get; set; }

    public DateOnly? DisposalDate { get; set; }

    public List<Guid> LinkedMortgageIds { get; set; } = [];

    public string? Notes { get; set; }

    public static RealEstateAssetEditModel FromAsset(RealEstateAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        return new RealEstateAssetEditModel
        {
            Id = asset.Id,
            Name = asset.Name,
            Type = asset.Type,
            CurrentEstimatedValue = asset.CurrentEstimatedValue,
            ValuationDate = asset.ValuationDate,
            AnnualValueGrowthPercent = asset.AnnualValueGrowthPercent,
            Pillar2BvgUsedAmount = asset.Pillar2BvgUsedAmount,
            AcquisitionDate = asset.AcquisitionDate,
            DisposalDate = asset.DisposalDate,
            LinkedMortgageIds = [.. asset.LinkedMortgageIds],
            Notes = asset.Notes
        };
    }

    public RealEstateAsset ToAsset()
    {
        return new RealEstateAsset
        {
            Id = Id,
            Name = Name.Trim(),
            Type = Type,
            CurrentEstimatedValue = CurrentEstimatedValue,
            ValuationDate = ValuationDate,
            AnnualValueGrowthPercent = AnnualValueGrowthPercent,
            Pillar2BvgUsedAmount = Pillar2BvgUsedAmount,
            AcquisitionDate = AcquisitionDate,
            DisposalDate = DisposalDate,
            LinkedMortgageIds = [.. LinkedMortgageIds],
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim()
        };
    }

    public void ToggleMortgage(Guid mortgageId, bool linked)
    {
        if (linked)
        {
            if (!LinkedMortgageIds.Contains(mortgageId))
            {
                LinkedMortgageIds.Add(mortgageId);
            }

            return;
        }

        LinkedMortgageIds.Remove(mortgageId);
    }
}
