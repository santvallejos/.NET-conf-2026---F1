using PitWall.Strategy.Timing;

namespace PitWall.Tests.Strategy.Timing;

public sealed class SessionLapTimingTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 8, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public void At_SectorsAndLaps_ClassifiedAgainstBestsWhenSet()
    {
        var timing = new SessionLapTiming(
        [
            Lap(1, 1, 0, 90, 30, 40, 20),
            Lap(2, 1, 1, 91, 31, 39, 21),
            Lap(1, 2, 90, 91, 30.5, 41, 19.5),
            Lap(2, 2, 92, 90.8, 30.8, 39.5, 20.5),
        ]);

        var snapshot = timing.At(At(500));

        var first = snapshot.Drivers[1].Laps;
        AssertClasses(first[0], TimingClass.SessionBest, TimingClass.SessionBest, TimingClass.SessionBest, TimingClass.SessionBest);
        AssertClasses(first[1], TimingClass.NoImprovement, TimingClass.NoImprovement, TimingClass.SessionBest, TimingClass.NoImprovement);
        var second = snapshot.Drivers[2].Laps;
        AssertClasses(second[0], TimingClass.PersonalBest, TimingClass.SessionBest, TimingClass.PersonalBest, TimingClass.PersonalBest);
        AssertClasses(second[1], TimingClass.PersonalBest, TimingClass.NoImprovement, TimingClass.PersonalBest, TimingClass.PersonalBest);
        Assert.Equal(2, snapshot.Drivers[2].LastLap.LapNumber);
    }

    [Fact]
    public void At_LapNotFinishedAtClock_IsExcluded()
    {
        var timing = new SessionLapTiming(
        [
            Lap(1, 1, 0, 90, 30, 40, 20),
            Lap(1, 2, 90, 85, 28, 38, 19),
        ]);

        Assert.Equal(0, timing.At(At(89.999)).CompletedLaps);
        Assert.Empty(timing.At(At(89.999)).Drivers);

        var atFirstEnd = timing.At(At(90));
        Assert.Equal(1, atFirstEnd.CompletedLaps);
        Assert.Equal(1, atFirstEnd.Drivers[1].LastLap.LapNumber);
        Assert.Equal(90, atFirstEnd.Drivers[1].BestLap);
        Assert.Equal(30, atFirstEnd.Drivers[1].BestS1);

        var atSecondEnd = timing.At(At(175));
        Assert.Equal(2, atSecondEnd.Drivers[1].LastLap.LapNumber);
        Assert.Equal(85, atSecondEnd.Drivers[1].BestLap);
    }

    [Fact]
    public void At_NullSectorsAndDuration_DoNotCountAsBest()
    {
        var timing = new SessionLapTiming(
        [
            Lap(1, 1, 0, 90, null, 40, 20),
            Lap(1, 2, 90, 92, 31, null, 21),
            Lap(1, 3, 182, null, 25, 30, 15),
        ]);

        var driver = timing.At(At(10_000)).Drivers[1];

        Assert.Null(driver.Laps[0].S1);
        Assert.Null(driver.Laps[1].S2);
        Assert.Equal(TimingClass.SessionBest, driver.Laps[1].S1!.Class);
        Assert.Equal(2, driver.Laps.Count);
        Assert.Equal(31, driver.BestS1);
        Assert.Equal(40, driver.BestS2);
        Assert.Equal(20, driver.BestS3);
        Assert.Equal(90, driver.BestLap);
    }

    [Fact]
    public void At_IdealLap_IsSumOfOwnBestSectorsOrNullWhenOneIsMissing()
    {
        var timing = new SessionLapTiming(
        [
            Lap(1, 1, 0, 90.5, 30.1, 40.2, 20.2),
            Lap(1, 2, 91, 90.4, 30.3, 39.9, 20.2),
            Lap(1, 3, 182, 90.3, 30.2, 40.0, 20.1),
            Lap(2, 1, 0, 91, null, 40, 21),
            Lap(2, 2, 91, 90, null, 39, 20),
        ]);

        var snapshot = timing.At(At(10_000));

        Assert.Equal(30.1 + 39.9 + 20.1, snapshot.Drivers[1].IdealLap!.Value, 3);
        Assert.Null(snapshot.Drivers[2].IdealLap);
    }

    [Fact]
    public void At_Rankings_OrderedAscendingAndOnlyDriversWithValue()
    {
        var timing = new SessionLapTiming(
        [
            Lap(1, 1, 0, 92, 31, 40, 21),
            Lap(2, 1, 0, 90, 30, 41, 19),
            Lap(3, 1, 0, 91, null, 39, 20),
            Lap(4, 1, 0, 91, 32, 42, 22),
        ]);

        var rankings = timing.At(At(10_000)).Rankings;

        Assert.Equal([2, 1, 4], rankings.BestS1.Select(entry => entry.DriverNumber));
        Assert.Equal([3, 1, 2, 4], rankings.BestS2.Select(entry => entry.DriverNumber));
        Assert.Equal([2, 3, 1, 4], rankings.BestS3.Select(entry => entry.DriverNumber));
        Assert.Equal([2, 3, 4, 1], rankings.BestLap.Select(entry => entry.DriverNumber));
        Assert.Equal([90, 91, 91, 92], rankings.BestLap.Select(entry => entry.Seconds));
        Assert.Equal([2, 1, 4], rankings.IdealLap.Select(entry => entry.DriverNumber));
    }

    private static TimingLap Lap(int driver, int lap, double start, double? duration, double? s1, double? s2, double? s3) =>
        new(driver, lap, At(start), duration, s1, s2, s3, false, false);

    private static DateTimeOffset At(double seconds) => Start + TimeSpan.FromSeconds(seconds);

    private static void AssertClasses(TimedLap lap, TimingClass s1, TimingClass s2, TimingClass s3, TimingClass time)
    {
        Assert.Equal(s1, lap.S1!.Class);
        Assert.Equal(s2, lap.S2!.Class);
        Assert.Equal(s3, lap.S3!.Class);
        Assert.Equal(time, lap.Lap.Class);
    }
}
