using PitWall.Domain;
using PitWall.Strategy.Rules;

namespace PitWall.Tests.Strategy.Rules;

public sealed class RulesEngineConstraintTests
{
    private readonly RulesEngine _engine = new(new RulesOptions());

    [Fact]
    public void EvaluateConstraints_NeutralDryRace_OnlyDiscardsWet()
    {
        var constraints = _engine.EvaluateConstraints(RuleFeaturesFactory.Neutral());

        var constraint = Assert.Single(constraints);
        Assert.Equal(ConstraintRules.DryTrack, constraint.Rule);
    }

    [Fact]
    public void EvaluateConstraints_RedFlag_ForbidsBox()
    {
        var features = RuleFeaturesFactory.Neutral() with { Track = new TrackStatus("RED", false, false) };

        var constraints = _engine.EvaluateConstraints(features);

        var constraint = Assert.Single(constraints, c => c.Rule == ConstraintRules.RedFlag);
        Assert.Equal(new[] { PitCall.Box }, constraint.ForbiddenCalls);
        Assert.False(RulesEngine.IsAllowed(constraints, PitCall.Box, Compound.Soft));
        Assert.True(RulesEngine.IsAllowed(constraints, PitCall.StayOut, null));
    }

    [Fact]
    public void EvaluateConstraints_PitLaneClosed_ForbidsBox()
    {
        var features = RuleFeaturesFactory.Neutral() with { PitLaneClosed = true };

        var constraints = _engine.EvaluateConstraints(features);

        var constraint = Assert.Single(constraints, c => c.Rule == ConstraintRules.PitLaneClosed);
        Assert.Equal(new[] { PitCall.Box }, constraint.ForbiddenCalls);
        Assert.False(RulesEngine.IsAllowed(constraints, PitCall.Box, Compound.Medium));
        Assert.True(RulesEngine.IsAllowed(constraints, PitCall.StayOut, null));
    }

    [Fact]
    public void EvaluateConstraints_DryRaceWithSingleDryCompound_RequiresSecondDryCompound()
    {
        var features = RuleFeaturesFactory.Neutral() with { UsedCompounds = [Compound.Hard] };

        var constraints = _engine.EvaluateConstraints(features);

        var constraint = Assert.Single(constraints, c => c.Rule == ConstraintRules.MandatoryDryCompounds);
        Assert.True(constraint.RequiresSecondDryCompound);
    }

    [Fact]
    public void EvaluateConstraints_WetCompoundUsed_DoesNotRequireSecondDryCompound()
    {
        var features = RuleFeaturesFactory.Neutral() with { UsedCompounds = [Compound.Intermediate, Compound.Hard] };

        var constraints = _engine.EvaluateConstraints(features);

        Assert.DoesNotContain(constraints, c => c.Rule == ConstraintRules.MandatoryDryCompounds);
    }

    [Fact]
    public void EvaluateConstraints_HeavyRain_DiscardsDryCompounds()
    {
        var features = RuleFeaturesFactory.Neutral() with { Rain = RainIntensity.Heavy, TrackWet = true };

        var constraints = _engine.EvaluateConstraints(features);

        var constraint = Assert.Single(constraints, c => c.Rule == ConstraintRules.HeavyRain);
        Assert.Equal(new[] { Compound.Soft, Compound.Medium, Compound.Hard }, constraint.ForbiddenCompounds);
        Assert.False(RulesEngine.IsAllowed(constraints, PitCall.Box, Compound.Soft));
        Assert.True(RulesEngine.IsAllowed(constraints, PitCall.Box, Compound.Wet));
    }

    [Fact]
    public void EvaluateConstraints_LightRain_KeepsDryCompounds()
    {
        var features = RuleFeaturesFactory.Neutral() with { Rain = RainIntensity.Light };

        var constraints = _engine.EvaluateConstraints(features);

        Assert.DoesNotContain(constraints, c => c.Rule == ConstraintRules.HeavyRain);
    }

    [Fact]
    public void EvaluateConstraints_DryTrackWithoutForecast_DiscardsWet()
    {
        var constraints = _engine.EvaluateConstraints(RuleFeaturesFactory.Neutral());

        var constraint = Assert.Single(constraints, c => c.Rule == ConstraintRules.DryTrack);
        Assert.Equal(new[] { Compound.Wet }, constraint.ForbiddenCompounds);
        Assert.False(RulesEngine.IsAllowed(constraints, PitCall.Box, Compound.Wet));
        Assert.True(RulesEngine.IsAllowed(constraints, PitCall.Box, Compound.Intermediate));
    }

    [Fact]
    public void EvaluateConstraints_DryTrackWithRainForecast_KeepsWet()
    {
        var features = RuleFeaturesFactory.Neutral() with { RainForecast = true };

        var constraints = _engine.EvaluateConstraints(features);

        Assert.DoesNotContain(constraints, c => c.Rule == ConstraintRules.DryTrack);
    }

    [Fact]
    public void EvaluateConstraints_Puncture_ForcesBox()
    {
        var features = RuleFeaturesFactory.Neutral() with { Puncture = true };

        var constraints = _engine.EvaluateConstraints(features);

        var constraint = Assert.Single(constraints, c => c.Rule == ConstraintRules.Puncture);
        Assert.Equal(PitCall.Box, constraint.ForcedCall);
        Assert.False(RulesEngine.IsAllowed(constraints, PitCall.StayOut, null));
        Assert.False(RulesEngine.IsAllowed(constraints, PitCall.BoxNextLap, Compound.Medium));
        Assert.True(RulesEngine.IsAllowed(constraints, PitCall.Box, Compound.Medium));
    }

    [Fact]
    public void EvaluateConstraints_MajorWingDamage_ForcesBox()
    {
        var features = RuleFeaturesFactory.Neutral() with { WingDamage = DamageSeverity.Major };

        var constraints = _engine.EvaluateConstraints(features);

        var constraint = Assert.Single(constraints, c => c.Rule == ConstraintRules.WingDamage);
        Assert.Equal(PitCall.Box, constraint.ForcedCall);
        Assert.False(RulesEngine.IsAllowed(constraints, PitCall.StayOut, null));
    }

    [Fact]
    public void EvaluateConstraints_MinorWingDamage_DoesNotForceBox()
    {
        var features = RuleFeaturesFactory.Neutral() with { WingDamage = DamageSeverity.Minor };

        var constraints = _engine.EvaluateConstraints(features);

        Assert.DoesNotContain(constraints, c => c.Rule == ConstraintRules.WingDamage);
    }

    [Fact]
    public void IsAllowed_PunctureUnderRedFlag_BoxForbiddenAndStayOutAllowed()
    {
        var features = RuleFeaturesFactory.Neutral() with { Track = new TrackStatus("RED", false, false), Puncture = true };

        var constraints = _engine.EvaluateConstraints(features);

        Assert.False(RulesEngine.IsAllowed(constraints, PitCall.Box, Compound.Medium));
        Assert.True(RulesEngine.IsAllowed(constraints, PitCall.StayOut, null));
    }

    [Fact]
    public void IsPitLaneClosed_ClosedThenReopened_ReturnsFalse()
    {
        var start = new DateTimeOffset(2026, 11, 8, 17, 0, 0, TimeSpan.Zero);
        var messages = new[]
        {
            new RaceControlMessage(start.AddMinutes(5), 12, "Other", null, null, null, "PIT EXIT OPEN"),
            new RaceControlMessage(start, 11, "Other", null, null, null, "PIT EXIT CLOSED"),
        };

        Assert.False(RaceControlSignals.IsPitLaneClosed(messages));
        Assert.True(RaceControlSignals.IsPitLaneClosed(messages[1..]));
        Assert.False(RaceControlSignals.IsPitLaneClosed([]));
    }
}
