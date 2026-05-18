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
}