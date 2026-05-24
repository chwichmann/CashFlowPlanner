using CashFlowPlanner.Core;

namespace CashFlowPlanner.BlazorWasm.Models;

public sealed class PlanDefaultsEditModel
{
    public Guid? DefaultPaymentAccountId { get; set; }

    public bool TreatWeekendsAsBankOffDays { get; set; } = true;

    public List<BankOffDayEditModel> BankOffDays { get; set; } = [];

    public static PlanDefaultsEditModel FromPlan(CashFlowPlan plan)
    {
        return new PlanDefaultsEditModel
        {
            DefaultPaymentAccountId = plan.DefaultPaymentAccountId,
            TreatWeekendsAsBankOffDays = plan.TreatWeekendsAsBankOffDays,
            BankOffDays = plan.BankOffDays
                .OrderBy(x => x.Date)
                .Select(BankOffDayEditModel.FromModel)
                .ToList()
        };
    }

    public List<BankOffDay> ToBankOffDays()
    {
        return BankOffDays
            .OrderBy(x => x.Date)
            .Select(x => x.ToModel())
            .ToList();
    }
}