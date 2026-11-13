using PitWall.Ingestion;

namespace PitWall.Tests.Ingestion;

public sealed class IngestionArgumentsTests
{
    [Fact]
    public void Parse_DocumentedCommand_ReadsEveryOption()
    {
        var options = IngestionArguments.Parse(
            ["--session", "11412", "--quali", "11408", "--drivers", "43,44,12,3", "--db", "data/pitwall.db", "--radio", "data/radio"]);

        Assert.Equal(11412, options.SessionKey);
        Assert.Equal(11408, options.QualiSessionKey);
        Assert.Equal([43, 44, 12, 3], options.Drivers);
        Assert.Equal("data/pitwall.db", options.DatabasePath);
        Assert.Equal("data/radio", options.RadioPath);
        Assert.Equal(TimeSpan.FromMilliseconds(500), options.RequestDelay);
        Assert.Null(options.FixturesPath);
    }

    [Fact]
    public void Parse_OnlySession_UsesDefaults()
    {
        var options = IngestionArguments.Parse(["--session", "11412"]);

        Assert.Null(options.QualiSessionKey);
        Assert.Empty(options.Drivers);
        Assert.Equal(IngestionOptions.DefaultDatabasePath, options.DatabasePath);
        Assert.Equal(IngestionOptions.DefaultRadioPath, options.RadioPath);
    }

    [Fact]
    public void Parse_RecordFixturesWithoutFolder_UsesDefaultFolder()
    {
        var options = IngestionArguments.Parse(["--record-fixtures", "--session", "11412", "--delay-ms", "0"]);

        Assert.Equal(IngestionOptions.DefaultFixturesPath, options.FixturesPath);
        Assert.Equal(TimeSpan.Zero, options.RequestDelay);
    }

    [Fact]
    public void Parse_RecordFixturesWithFolder_UsesThatFolder()
    {
        var options = IngestionArguments.Parse(["--session", "11412", "--record-fixtures", "out/fixtures"]);

        Assert.Equal("out/fixtures", options.FixturesPath);
    }

    [Theory]
    [InlineData("--quali 11408")]
    [InlineData("--session")]
    [InlineData("--session abc")]
    [InlineData("--session 11412 --drivers 43,x")]
    [InlineData("--session 11412 --unknown 1")]
    public void Parse_InvalidArguments_ThrowsArgumentException(string commandLine)
    {
        Assert.Throws<ArgumentException>(() => IngestionArguments.Parse(commandLine.Split(' ')));
    }
}
