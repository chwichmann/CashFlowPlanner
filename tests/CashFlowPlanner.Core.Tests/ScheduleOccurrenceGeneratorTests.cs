using CashFlowPlanner.Core;

namespace CashFlowPlanner.Core.Tests;

public sealed class ScheduleOccurrenceGeneratorTests
{
    [Fact]
    public void OnceSchedule_Should_GenerateSingleDate_WhenDateIsInsideRange()
    {
        // Arrange
        var generator = new ScheduleOccurrenceGenerator();

        var schedule = new Schedule
        {
            Frequency = ScheduleFrequency.Once,
            StartDate = new DateOnly(2026, 6, 15)
        };

        // Act
        var dates = generator.GenerateOccurrences(
            schedule,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30));

        // Assert
        Assert.Single(dates);
        Assert.Equal(new DateOnly(2026, 6, 15), dates[0]);
    }

    [Fact]
    public void OnceSchedule_Should_GenerateNoDate_WhenDateIsOutsideRange()
    {
        // Arrange
        var generator = new ScheduleOccurrenceGenerator();

        var schedule = new Schedule
        {
            Frequency = ScheduleFrequency.Once,
            StartDate = new DateOnly(2026, 7, 1)
        };

        // Act
        var dates = generator.GenerateOccurrences(
            schedule,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30));

        // Assert
        Assert.Empty(dates);
    }

    [Fact]
    public void MonthlySchedule_Should_GenerateExpectedMonthlyDates()
    {
        // Arrange
        var generator = new ScheduleOccurrenceGenerator();

        var schedule = new Schedule
        {
            Frequency = ScheduleFrequency.Monthly,
            StartDate = new DateOnly(2026, 1, 10),
            DayOfMonth = 10
        };

        // Act
        var dates = generator.GenerateOccurrences(
            schedule,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 3, 31));

        // Assert
        Assert.Equal(
        [
            new DateOnly(2026, 1, 10),
            new DateOnly(2026, 2, 10),
            new DateOnly(2026, 3, 10)
        ], dates);
    }

    [Fact]
    public void MonthlySchedule_Should_ClampDayOfMonth_WhenMonthHasFewerDays()
    {
        // Arrange
        var generator = new ScheduleOccurrenceGenerator();

        var schedule = new Schedule
        {
            Frequency = ScheduleFrequency.Monthly,
            StartDate = new DateOnly(2026, 1, 31),
            DayOfMonth = 31
        };

        // Act
        var dates = generator.GenerateOccurrences(
            schedule,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 3, 31));

        // Assert
        Assert.Equal(
        [
            new DateOnly(2026, 1, 31),
            new DateOnly(2026, 2, 28),
            new DateOnly(2026, 3, 31)
        ], dates);
    }

    [Fact]
    public void MonthlySchedule_Should_StopAtEndDate()
    {
        // Arrange
        var generator = new ScheduleOccurrenceGenerator();

        var schedule = new Schedule
        {
            Frequency = ScheduleFrequency.Monthly,
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 2, 15),
            DayOfMonth = 1
        };

        // Act
        var dates = generator.GenerateOccurrences(
            schedule,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));

        // Assert
        Assert.Equal(
        [
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 2, 1)
        ], dates);
    }

    [Fact]
    public void MonthlySchedule_WithNextBusinessDay_Should_MoveSaturdayToMonday()
    {
        // Arrange
        var generator = new ScheduleOccurrenceGenerator();

        // 2026-08-01 is Saturday
        var schedule = new Schedule
        {
            Frequency = ScheduleFrequency.Monthly,
            StartDate = new DateOnly(2026, 8, 1),
            DayOfMonth = 1,
            BusinessDayAdjustment = BusinessDayAdjustment.NextBusinessDay
        };

        // Act
        var dates = generator.GenerateOccurrences(
            schedule,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));

        // Assert
        Assert.Single(dates);
        Assert.Equal(new DateOnly(2026, 8, 3), dates[0]);
    }

    [Fact]
    public void MonthlySchedule_WithPreviousBusinessDay_Should_MoveSundayToFriday()
    {
        // Arrange
        var generator = new ScheduleOccurrenceGenerator();

        // 2026-02-01 is Sunday
        var schedule = new Schedule
        {
            Frequency = ScheduleFrequency.Monthly,
            StartDate = new DateOnly(2026, 2, 1),
            DayOfMonth = 1,
            BusinessDayAdjustment = BusinessDayAdjustment.PreviousBusinessDay
        };

        // Act
        var dates = generator.GenerateOccurrences(
            schedule,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 31));

        // Assert
        Assert.Single(dates);
        Assert.Equal(new DateOnly(2026, 1, 30), dates[0]);
    }

    [Fact]
    public void MonthlySchedule_WithPreviousBusinessDay_Should_GenerateAdjustedDatesAcrossMonths()
    {
        // Arrange
        var generator = new ScheduleOccurrenceGenerator();

        // 2026-02-01 is Sunday
        // 2026-03-01 is Sunday
        var schedule = new Schedule
        {
            Frequency = ScheduleFrequency.Monthly,
            StartDate = new DateOnly(2026, 2, 1),
            DayOfMonth = 1,
            BusinessDayAdjustment = BusinessDayAdjustment.PreviousBusinessDay
        };

        // Act
        var dates = generator.GenerateOccurrences(
            schedule,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 2, 28));

        // Assert
        Assert.Equal(
        [
            new DateOnly(2026, 1, 30),
        new DateOnly(2026, 2, 27)
        ], dates);
    }

    [Fact]
    public void MonthlySchedule_WithNextBusinessDayAndPlanHoliday_Should_MoveToNextBankBusinessDay()
    {
        // Arrange
        var generator = new ScheduleOccurrenceGenerator();

        // 2026-08-01 is Saturday.
        // 2026-08-03 is Monday, but configured as bank off day.
        // Expected adjusted date is Tuesday 2026-08-04.
        var schedule = new Schedule
        {
            Frequency = ScheduleFrequency.Monthly,
            StartDate = new DateOnly(2026, 8, 1),
            DayOfMonth = 1,
            BusinessDayAdjustment = BusinessDayAdjustment.NextBusinessDay
        };

        var plan = CreatePlanWithBankOffDays(
        [
            new BankOffDay
        {
            Date = new DateOnly(2026, 8, 3),
            Name = "Bank holiday"
        }
        ]);

        // Act
        var dates = generator.GenerateOccurrences(
            schedule,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31),
            plan);

        // Assert
        Assert.Single(dates);
        Assert.Equal(new DateOnly(2026, 8, 4), dates[0]);
    }

    [Fact]
    public void MonthlySchedule_WithPreviousBusinessDayAndPlanHoliday_Should_MoveToPreviousBankBusinessDay()
    {
        // Arrange
        var generator = new ScheduleOccurrenceGenerator();

        // 2026-02-01 is Sunday.
        // Previous weekend-only business day would be Friday 2026-01-30.
        // But 2026-01-30 is configured as bank off day.
        // Expected adjusted date is Thursday 2026-01-29.
        var schedule = new Schedule
        {
            Frequency = ScheduleFrequency.Monthly,
            StartDate = new DateOnly(2026, 2, 1),
            DayOfMonth = 1,
            BusinessDayAdjustment = BusinessDayAdjustment.PreviousBusinessDay
        };

        var plan = CreatePlanWithBankOffDays(
        [
            new BankOffDay
        {
            Date = new DateOnly(2026, 1, 30),
            Name = "Bank holiday"
        }
        ]);

        // Act
        var dates = generator.GenerateOccurrences(
            schedule,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 31),
            plan);

        // Assert
        Assert.Single(dates);
        Assert.Equal(new DateOnly(2026, 1, 29), dates[0]);
    }

    [Fact]
    public void MonthlySchedule_WithPlanAndWeekendsDisabled_Should_NotTreatWeekendAsBankOffDay()
    {
        // Arrange
        var generator = new ScheduleOccurrenceGenerator();

        // 2026-08-01 is Saturday.
        // Because TreatWeekendsAsBankOffDays = false, the date should stay 2026-08-01.
        var schedule = new Schedule
        {
            Frequency = ScheduleFrequency.Monthly,
            StartDate = new DateOnly(2026, 8, 1),
            DayOfMonth = 1,
            BusinessDayAdjustment = BusinessDayAdjustment.NextBusinessDay
        };

        var plan = new CashFlowPlan
        {
            Id = Guid.NewGuid(),
            Name = "Test Plan",
            BaseCurrency = "CHF",
            TreatWeekendsAsBankOffDays = false,
            BankOffDays = []
        };

        // Act
        var dates = generator.GenerateOccurrences(
            schedule,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31),
            plan);

        // Assert
        Assert.Single(dates);
        Assert.Equal(new DateOnly(2026, 8, 1), dates[0]);
    }

    [Fact]
    public void DailySchedule_Should_KeepEveryOccurrence_WhenBusinessDayAdjustmentCollides()
    {
        // Arrange
        // January 2026 has 31 nominal daily occurrences. NextBusinessDay maps each
        // weekend day onto the following Monday, so several nominal dates share one
        // business day. Those are collisions, not duplicates: a daily expense still
        // happens 31 times and every occurrence has to be kept.
        var generator = new ScheduleOccurrenceGenerator();

        var schedule = new Schedule
        {
            Frequency = ScheduleFrequency.Daily,
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 1, 31),
            Interval = 1,
            BusinessDayAdjustment = BusinessDayAdjustment.NextBusinessDay
        };

        // Act
        // The range reaches into February so that Saturday 2026-01-31, which moves
        // to Monday 2026-02-02, is not clipped away by the range instead.
        var dates = generator.GenerateOccurrences(
            schedule,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 2, 2));

        // Assert
        Assert.Equal(31, dates.Count);

        // Saturday 2026-01-03 and Sunday 2026-01-04 both move to Monday 2026-01-05,
        // which is itself a nominal occurrence: three occurrences on one day.
        Assert.Equal(3, dates.Count(x => x == new DateOnly(2026, 1, 5)));

        // Still ordered.
        Assert.Equal(dates.Order().ToList(), dates);
    }

    private static CashFlowPlan CreatePlanWithBankOffDays(
        List<BankOffDay> bankOffDays)
    {
        return new CashFlowPlan
        {
            Id = Guid.NewGuid(),
            Name = "Test Plan",
            BaseCurrency = "CHF",
            TreatWeekendsAsBankOffDays = true,
            BankOffDays = bankOffDays
        };
    }
}