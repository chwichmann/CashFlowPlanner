namespace CashFlowPlanner.Core.Mortgages;

public sealed class MortgageRateCurve
{
    private readonly IReadOnlyList<MortgageInterestRatePoint> _points;

    public MortgageRateCurve(IEnumerable<MortgageInterestRatePoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        _points = points
            .OrderBy(x => x.Date)
            .ToList();
    }

    public decimal GetRatePercent(DateOnly date)
    {
        if (_points.Count == 0)
        {
            return 0m;
        }

        if (date <= _points[0].Date)
        {
            return _points[0].RatePercent;
        }

        if (date >= _points[^1].Date)
        {
            return _points[^1].RatePercent;
        }

        for (var i = 0; i < _points.Count - 1; i++)
        {
            var left = _points[i];
            var right = _points[i + 1];

            if (date < left.Date || date > right.Date)
            {
                continue;
            }

            var totalDays = right.Date.DayNumber - left.Date.DayNumber;

            if (totalDays <= 0)
            {
                return left.RatePercent;
            }

            var offsetDays = date.DayNumber - left.Date.DayNumber;
            var ratio = offsetDays / (decimal)totalDays;

            return left.RatePercent + ((right.RatePercent - left.RatePercent) * ratio);
        }

        return _points[^1].RatePercent;
    }
}