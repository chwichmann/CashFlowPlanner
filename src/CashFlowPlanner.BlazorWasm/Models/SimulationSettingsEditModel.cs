using CashFlowPlanner.Core;

namespace CashFlowPlanner.BlazorWasm.Models;

public sealed class SimulationSettingsEditModel
{
    public SimulationDateMode DateMode { get; set; } = SimulationDateMode.RollingHorizon;

    public SimulationStartAnchor StartAnchor { get; set; } = SimulationStartAnchor.FirstDayOfCurrentMonth;

    public int HorizonMonths { get; set; } = 12;

    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddYears(1));

    public SimulationGranularity Granularity { get; set; } = SimulationGranularity.Daily;

    public bool IncludeInactiveAccounts { get; set; }

    public bool WarnOnNegativeBankBalance { get; set; } = true;

    public static SimulationSettingsEditModel FromSettings(SimulationSettings settings)
    {
        return new SimulationSettingsEditModel
        {
            DateMode = settings.DateMode,
            StartAnchor = settings.StartAnchor,
            HorizonMonths = settings.HorizonMonths,
            StartDate = settings.StartDate,
            EndDate = settings.EndDate,
            Granularity = settings.Granularity,
            IncludeInactiveAccounts = settings.IncludeInactiveAccounts,
            WarnOnNegativeBankBalance = settings.WarnOnNegativeBankBalance
        };
    }

    public SimulationSettings ToSettings()
    {
        return new SimulationSettings
        {
            DateMode = DateMode,
            StartAnchor = StartAnchor,
            HorizonMonths = HorizonMonths,
            StartDate = StartDate,
            EndDate = EndDate,
            Granularity = Granularity,
            IncludeInactiveAccounts = IncludeInactiveAccounts,
            WarnOnNegativeBankBalance = WarnOnNegativeBankBalance
        };
    }
}